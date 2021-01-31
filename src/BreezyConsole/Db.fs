module BreezyConsole.Db

open System.Threading.Tasks
open BreezyConsole.Application
open Npgsql
open Npgsql.FSharp.Tasks
open FSharp.Control.Tasks.V2

module DiscriminatedUnion =
    open Microsoft.FSharp.Reflection

    let toString (x:'a) =
        let (case, _ ) = FSharpValue.GetUnionFields(x, typeof<'a>)
        case.Name

    let fromString<'a> (s:string) =
        match FSharpType.GetUnionCases typeof<'a> |> Array.filter (fun case -> case.Name = s) with
        |[|case|] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'a)
        |_ -> None

let connectionString = "Host=localhost; Database=postgres; Port=5433; Username=postgres; Password=postgres;"

let savePositions (applications: Application list) (transaction: NpgsqlTransaction): Task<Map<Position, int>> =
    let positions =
        applications
        |> Seq.map (fun application -> application.position)
        |> Seq.distinct

    let savePosition (position: Position) =
        task {
            let! id =
                Sql.transaction transaction
                |> Sql.query "INSERT INTO positions (breezy_id, name) VALUES (@breezy_id, @name) RETURNING position_id"
                |> Sql.parameters [
                    "@breezy_id", Sql.text position.breezyId
                    "@name", Sql.text position.name
                ]
                |> Sql.executeRowAsync (fun read -> read.int "position_id")
            return (position, id)
     }

    task {
        let! positionIds = positions |> Seq.map savePosition |> Task.runSequentially
        return positionIds |> Map.ofSeq
    }

let saveInterviewers (interviews: Interview seq) (transaction: NpgsqlTransaction): Task<Map<string, int>> =
    let interviewers =
        interviews
        |> Seq.map (fun interview -> interview.interviewer)
        |> Seq.distinct

    let saveInterviewer interviewer =
        task {
            let! id =
                Sql.transaction transaction
                |> Sql.query "INSERT INTO interviewers (name) VALUES (@value) RETURNING interviewer_id"
                |> Sql.parameters [ "@value", Sql.text interviewer ]
                |> Sql.executeRowAsync (fun read -> read.int "interviewer_id")
            return (interviewer, id)
     }

    task {
        let! interviewerIds = interviewers |> Seq.map saveInterviewer |> Task.runSequentially
        return interviewerIds |> Map.ofSeq
    }

let saveSkills (interviews: Interview seq) (transaction: NpgsqlTransaction): Task<Map<string, int>> =
    let skills =
        interviews
        |> Seq.collect (fun interview -> interview.skills)
        |> Seq.map (fun skill -> skill.skill)
        |> Seq.distinct
        |> Seq.toList

    let saveSkill skill =
        task {
            let! id =
                Sql.transaction transaction
                |> Sql.query "INSERT INTO skills (title) VALUES (@value) RETURNING skill_id"
                |> Sql.parameters [ "@value", Sql.text skill ]
                |> Sql.executeRowAsync (fun read -> read.int "skill_id")
            return (skill, id)
     }

    task {
        let! skillIds = skills |> Seq.map saveSkill |> Task.runSequentially
        return skillIds |> Map.ofSeq
    }

let saveSkillRating
    (skills: Map<string, int>)
    (interviewId: int)
    (transaction: NpgsqlTransaction)
    (skillRating: SkillRating) =
        Sql.transaction transaction
        |> Sql.query "INSERT INTO skill_ratings (interview_id, skill_id, rating) VALUES (@interview_id, @skill_id, @rating)"
        |> Sql.parameters [
            ("@interview_id", Sql.int interviewId)
            ("@skill_id", Sql.int (skills |> Map.find skillRating.skill))
            ("@rating", Sql.text (skillRating.rating |> DiscriminatedUnion.toString))
        ]
        |> Sql.executeNonQueryAsync

let saveInterview
    (interviewers: Map<string, int>)
    (skills: Map<string, int>)
    (applicationId: int)
    (transaction: NpgsqlTransaction)
    (interview: Interview): Task<unit> =
    task {
        let! interviewId =
            Sql.transaction transaction
            |> Sql.query "INSERT INTO interviews (application_id, interviewer_id, rating, stage) VALUES (@application_id, @interviewer_id, @rating, @stage) RETURNING interview_id"
            |> Sql.parameters [
                ("@application_id", Sql.int applicationId)
                ("@interviewer_id", Sql.int (interviewers |> Map.find interview.interviewer))
                ("@rating", Sql.text (interview.rating |> DiscriminatedUnion.toString))
                ("@stage", Sql.text (interview.stage |> DiscriminatedUnion.toString))
            ]
            |> Sql.executeRowAsync (fun read -> read.int "interview_id")

        do! _ =
            interview.skills
            |> List.toSeq
            |> Seq.map (saveSkillRating skills interviewId transaction)
            |> Task.runSequentially
    }

let saveApplication
    (positions: Map<Position, int>)
    (interviewers: Map<string, int>)
    (skills: Map<string, int>)
    (transaction: NpgsqlTransaction)
    (application: Application): Task<unit> =
    task {
        let leftPipelineStage =
            match application.status with
            | Withdrawn stage | TBNed stage -> Some (stage |> DiscriminatedUnion.toString)
            | _ -> None

        let! applicationId =
            Sql.transaction transaction
            |> Sql.query "INSERT INTO applications (position_id, breezy_id, date, university, university_course, status, left_pipeline_stage)
            VALUES (@position_id, @breezy_id, @date, @university, @university_course, @status, @left_pipeline_stage)
            RETURNING application_id"
            |> Sql.parameters [
                ("@position_id", Sql.int (positions |> Map.find application.position))
                ("@breezy_id", Sql.text application.breezyId)
                ("@date", Sql.timestamp application.applied.UtcDateTime)
                ("@university", Sql.textOrNone application.university)
                ("@university_course", Sql.textOrNone application.universityCourse)
                ("@status", Sql.text (application.status |> DiscriminatedUnion.toString))
                ("@left_pipeline_stage", Sql.textOrNone leftPipelineStage)
            ]
            |> Sql.executeRowAsync (fun read -> read.int "application_id")

        do! _ =
            application.interviews
            |> List.toSeq
            |> Seq.map (saveInterview interviewers skills applicationId transaction)
            |> Task.runSequentially
    }

let savePosition (position: Position) (transaction: NpgsqlTransaction): Task<int> =
    Sql.transaction transaction
    |> Sql.query "INSERT INTO positions (breezy_id, name) VALUES (@breezy_id, @name) RETURNING position_id"
    |> Sql.parameters [
        ("@breezy_id", Sql.text position.breezyId)
        ("@name_id", Sql.text position.name)
    ]
    |> Sql.executeRowAsync (fun read -> read.int "position_id")

let saveApplications (applications: Application list) =
    let interviews =
        applications
        |> Seq.ofList
        |> Seq.collect (fun application -> application.interviews)
    task {
        use connection = new NpgsqlConnection(connectionString)
        connection.Open()
        use transaction = connection.BeginTransaction()

        let! positions = savePositions applications transaction
        let! interviewers = saveInterviewers interviews transaction
        let! skills = saveSkills interviews transaction

        let! _ = applications |> Seq.map (saveApplication positions interviewers skills transaction) |> Task.runSequentially

        do! transaction.CommitAsync()
    }
