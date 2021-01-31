module BreezyConsole.Application

open System
open BreezyConsole.Http

type Position = {
    breezyId: string
    name: string
}

type InterviewStage =
    | Phone
    | Second
    | Unknown

type Rating =
    | VeryPoor
    | Poor
    | Neutral
    | Good
    | VeryGood

type SkillRating = {
    skill: string
    rating: Rating
}

type Interview = {
    stage: InterviewStage
    interviewer: string
    rating: Rating
    skills: SkillRating list
}

type LeavePipelineStage =
    | AfterCv
    | AfterPhone
    | AfterSecond

type ApplicationStatus =
    | Offered
    | TBNed of LeavePipelineStage
    | Withdrawn of LeavePipelineStage
    | InProgress

type Application = {
    position: Position
    breezyId: string
    applied: DateTimeOffset
    status: ApplicationStatus
    interviews: Interview list
}

type Transition =
    | InviteToPhone
    | InviteToSecond
    | Offer
    | TBN
    | Withdraw

let parseTransition (stream: Breezy.CandidateStream): Transition option =
    match stream.``type`` with
    | "candidateStatusUpdated" ->
        let json = stream.object.GetRawText()
        let content: Breezy.CandidateStatusStreamContent = deserializeJsonString json

        match content.stage.name with
        | "Invited to Second" -> Some InviteToSecond
        | "Invited to Phone" -> Some InviteToPhone
        | "Offer Made" -> Some Offer
        | "TBN'd" -> Some TBN
        | "Withdrawn" -> Some Withdraw
        | _ -> None

    | _ -> None

let tryFindPhoneInterviewer (stream: Breezy.CandidateStream): string option =
    match stream.``type`` with
    | "companyNotePosted" ->
        let json = stream.object.GetRawText()
        let content: Breezy.CandidateNoteStreamContent = deserializeJsonString json

        if (content.body.Contains("@recruitghyston", StringComparison.InvariantCultureIgnoreCase)
            && content.body.Contains("Invite", StringComparison.InvariantCultureIgnoreCase)) then
            Some content.actingUser.name
        else None

    | _ -> None

let determineStatus (transitions: Transition list): ApplicationStatus =
    let leftPipelineAt () =
        if transitions |> List.contains InviteToSecond then
            AfterSecond
        else if transitions |> List.contains InviteToPhone then
            AfterPhone
        else
            AfterCv

    if transitions |> List.contains Withdraw then
        Withdrawn (leftPipelineAt ())
    else if transitions |> List.contains TBN then
        TBNed (leftPipelineAt ())
    else if transitions |> List.contains Offer then
        Offered
    else InProgress

let parseRating (score: Breezy.Score): Rating =
    match score with
    | "very_poor" -> VeryPoor
    | "poor" -> Poor
    | "neutral" -> Neutral
    | "good" -> Good
    | "very_good" -> VeryGood
    | _ -> failwith $"Unknown score: {score}"

let parseScorecard (transitions: Transition list) (phoneInterviewer: string option) (scorecard: Breezy.Scorecard): Interview =
    let interviewStage =
        match transitions |> List.contains InviteToSecond with
        | true ->
            match phoneInterviewer with
            | Some phoneInterviewer ->
                if phoneInterviewer = scorecard.actingUser.name then Phone else Second
            | None -> Unknown
        | false -> Phone

    let skills =
        scorecard.scorecard.sections
        |> Option.defaultValue Array.empty
        |> Array.toSeq
        |> Seq.map (fun section -> section.criteria |> Array.toSeq)
        |> Seq.concat
        |> Seq.filter(fun criteria -> Option.isSome criteria.score)
        |> Seq.map (fun criteria -> { skill = criteria.text; rating = criteria.score |> Option.get |> parseRating })
        |> Seq.toList

    { stage = interviewStage
      interviewer = scorecard.actingUser.name
      rating = parseRating scorecard.scorecard.score
      skills = skills }

let parseApplication (position: Position) (candidate: Breezy.Candidate) (meta: Breezy.CandidateMeta): Application =
    let transitions =
        meta.stream
        |> Array.toSeq
        |> Seq.map parseTransition
        |> Seq.filter Option.isSome
        |> Seq.map Option.get
        |> Seq.toList

    let status = determineStatus transitions

    let phoneInterviewer =
        meta.stream
        |> Array.toSeq
        |> Seq.map tryFindPhoneInterviewer
        |> Seq.filter Option.isSome
        |> Seq.tryHead
        |> Option.flatten

    { position = position
      breezyId = candidate.id
      applied = candidate.creationDate
      interviews = meta.scorecards |> Array.toList |> List.map (parseScorecard transitions phoneInterviewer)
      status = status }