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

type Score = string

type ScorecardCriteria = {
    text: string
    score: Score option
}

type ScorecardSection = {
    criteria: ScorecardCriteria array
}

type ScorecardResult = {
    note: string option
    score: Score
    updatedDate: DateTimeOffset option
    sections: (ScorecardSection array) option
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

type Question = {
    text: string
    response: JsonElement option // String for text questions, JSON object for file uploads
}

type Questionnaire = {
    questions: Question array
}

type CandidateMeta = {
    scorecards: Scorecard array
    stream: CandidateStream array
    questionnaires: Questionnaire array
}

let getCandidates (httpClient: HttpClient) (companyId: string) (positionId: string): Task<Candidate list> =
    get $"https://api.breezy.hr/v3/company/{companyId}/position/{positionId}/candidates" httpClient

let getCandidateMeta (httpClient: HttpClient) (companyId: string) (positionId: string) (candidateId: string): Task<CandidateMeta> =
    get $"https://api.breezy.hr/v3/company/{companyId}/position/{positionId}/candidate/{candidateId}/meta" httpClient
