module BreezyConsole.Breezy

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open BreezyConsole.Http


type Stage = {
    name: string
}

type Candidate = {
    [<JsonPropertyName("_id")>] id: string
    creationDate: DateTimeOffset
    stage: Stage
    updatedDate: DateTimeOffset
}

type Score = string // good, very_good

type ScorecardCriteria = {
    text: string
    score: Score option
}

type ScorecardSection = {
    criteria: ScorecardCriteria array
}

type ScorecardResult = {
    [<JsonPropertyName("_id")>] id: string
    note: string
    score: Score
    updatedDate: DateTimeOffset
    sections: ScorecardSection array
}

type ScorecardUser = {
    [<JsonPropertyName("_id")>] id: string
    name: string
}

type Scorecard = {
    [<JsonPropertyName("_id")>] id: string
    actingUser: ScorecardUser
    scorecard: ScorecardResult
}

type CandidateStream = {
    timestamp: DateTimeOffset
    ``type``: string
    object: JsonElement
}

type CandidateStatusStreamContent = {
    stage: Stage
}

type NoteUser = {
    name: string
}

type CandidateNoteStreamContent = {
    actingUser: NoteUser
    body: string
}

type CandidateMeta = {
    scorecards: Scorecard array
    stream: CandidateStream array
}

// TODO Update the page size to be a realistic number once I've finished developing
let getCandidates (httpClient: HttpClient) (companyId: string) (positionId: string): Task<Candidate list> =
    get $"https://api.breezy.hr/v3/company/{companyId}/position/{positionId}/candidates?page_size=3" httpClient

let getCandidateMeta (httpClient: HttpClient) (companyId: string) (positionId: string) (candidateId: string): Task<CandidateMeta> =
    get $"https://api.breezy.hr/v3/company/{companyId}/position/{positionId}/candidate/{candidateId}/meta" httpClient
