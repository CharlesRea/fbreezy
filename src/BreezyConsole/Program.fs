open System.Net.Http
open System.Threading.Tasks
open BreezyConsole
open FSharp.Control.Tasks.V2
open BreezyConsole.Http

type SigninCommand = {
    email: string
    password: string
}

type SigninResponse = {
    accessToken: string
}

let companyId = "e85537b9339b01"

let positionIds = [
    "c3da354dcaef01" // developer
    "f7bc961fe44f01" // Internship
    "96ff3356fe5901" // Placement
]

let signin (httpClient: HttpClient) email password: Task<SigninResponse> =
    task {
        let request = { email = email; password = password; }
        use! response = httpClient.PostAsync("https://api.breezy.hr/v3/signin", jsonContent request)
        response.EnsureSuccessStatusCode() |> ignore
        use! responseStream = response.Content.ReadAsStreamAsync()
        return! deserializeJson responseStream
    }

let processCandidate (httpClient: HttpClient) (positionId: string) (candidate: Breezy.Candidate): Task<Application.Application> =
    task {
        printf $"Fetching candidate {candidate.id}\n\n"
        let! candidateMeta = Breezy.getCandidateMeta httpClient companyId positionId candidate.id
        printf "Fetched candidate\n\n"

        return Application.parseApplication candidate candidateMeta
    }

let processPosition (httpClient: HttpClient) (positionId: string) =
    task {
        printf $"Fetching candidates for position {positionId}\n\n"
        let! candidates = Breezy.getCandidates httpClient companyId positionIds.[0]
        printf $"Received {candidates.Length} candidates\n\n"

        for candidate in candidates do
            let! application = processCandidate httpClient positionId candidate
            printf "Parsed application: \n%A\n\n" application
            do! Task.Delay(800) // Delay to keep us below 100 requests per second and avoid rate limiting: https://developer.breezy.hr/docs/rate-limiting

        return! Task.CompletedTask
    }

let run email password =
    task {
        use httpClient = new HttpClient()

        printf $"Signing in as {email}\n"
        let! signinResponse = signin httpClient email password
        printf $"Signed in successfully\n\n"

        httpClient.DefaultRequestHeaders.Add("Authorization", signinResponse.accessToken)

        for position in positionIds do
            do! processPosition httpClient position

        return! Task.CompletedTask
    }

[<EntryPoint>]
let main argv =
    let email = argv.[0]
    let password = argv.[1]

    run email password |> Async.AwaitTask |> Async.RunSynchronously

    0 // return an integer exit code
