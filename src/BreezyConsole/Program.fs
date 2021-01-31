open System.Net.Http
open System.Threading.Tasks
open BreezyConsole
open FSharp.Control.Tasks.V2
open BreezyConsole.Http
open BreezyConsole.Application

type SigninCommand = {
    email: string
    password: string
}

type SigninResponse = {
    accessToken: string
}

let companyId = "e85537b9339b01"

let positions = [
    { breezyId = "c3da354dcaef01"; name = "Graduate" }
    { breezyId = "f7bc961fe44f01"; name = "Internship" }
    { breezyId = "96ff3356fe5901"; name = "Placement" }
]

let signin (httpClient: HttpClient) email password: Task<SigninResponse> =
    task {
        let request = { email = email; password = password; }
        use! response = httpClient.PostAsync("https://api.breezy.hr/v3/signin", jsonContent request)
        response.EnsureSuccessStatusCode() |> ignore
        use! responseStream = response.Content.ReadAsStreamAsync()
        return! deserializeJson responseStream
    }

let processCandidate (httpClient: HttpClient) (position: Position) (candidate: Breezy.Candidate): Task<Application> =
    task {
        printf $"Fetching candidate {candidate.id}\n\n"
        let! candidateMeta = Breezy.getCandidateMeta httpClient companyId position.breezyId candidate.id
        printf "Fetched candidate\n\n"

        return parseApplication position candidate candidateMeta
    }

let processPosition (httpClient: HttpClient) (position: Position): Task<Application list> =
    task {
        printf $"Fetching candidates for position {position.name}\n\n"
        let! candidates = Breezy.getCandidates httpClient companyId position.breezyId
        printf $"Received {candidates.Length} candidates\n\n"

        let! applications =
            candidates
            |> Seq.ofList
            |> Seq.map (processCandidate httpClient position)
            |> Task.runSequentially

        printf $"Fetched all candidates for position {position.name}\n\n"
        return applications
    }

let run email password =
    task {
        use httpClient = new HttpClient()

        printf $"Signing in as {email}\n"
        let! signinResponse = signin httpClient email password
        printf $"Signed in successfully\n\n"

        httpClient.DefaultRequestHeaders.Add("Authorization", signinResponse.accessToken)

        let! applications =
            positions
            |> Seq.ofList
            |> Seq.map (processPosition httpClient)
            |> Task.runSequentially

        printf $"Fetched all applications from Breezy API\n\n"
        do! Db.saveApplications (applications |> List.concat)
        printf "Saved applications to DB\n\n"
    }

[<EntryPoint>]
let main argv =
    let email = argv.[0]
    let password = argv.[1]

    run email password |> Async.AwaitTask |> Async.RunSynchronously

    0 // return an integer exit code
