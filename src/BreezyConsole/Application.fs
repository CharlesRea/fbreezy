module BreezyConsole.Application

open System
open BreezyConsole.Http

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
    date: DateTimeOffset
    rating: Rating
    skills: SkillRating list
}

type ApplicationStatus =
    | Offered
    | TBNed
    | Withdrawn
    | InProgress

type TbnStage =
    | Cv
    | AfterPhone
    | AfterSecond

type Application = {
    breezyId: string
    applied: DateTimeOffset
    interviews: Interview list
    status: ApplicationStatus
    tbnStage: TbnStage option
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
    if transitions |> List.contains Withdraw then
        Withdrawn
    else if transitions |> List.contains TBN then
        TBNed
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
        |> Array.toSeq
        |> Seq.map (fun section -> section.criteria |> Array.toSeq)
        |> Seq.concat
        |> Seq.filter(fun criteria -> Option.isSome criteria.score)
        |> Seq.map (fun criteria -> { skill = criteria.text; rating = criteria.score |> Option.get |> parseRating })
        |> Seq.toList

    { stage = interviewStage
      interviewer = scorecard.actingUser.name
      date = scorecard.scorecard.updatedDate
      rating = parseRating scorecard.scorecard.score
      skills = skills }

let parseApplication (candidate: Breezy.Candidate) (meta: Breezy.CandidateMeta): Application =
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

    let tbnStage =
        match status with
        | TBNed ->
            if transitions |> List.contains InviteToSecond then
                Some AfterSecond
            else if transitions |> List.contains InviteToPhone then
                Some AfterPhone
            else
                Some Cv
        | _ -> None

    { breezyId = candidate.id
      applied = candidate.creationDate
      interviews = meta.scorecards |> Array.toList |> List.map (parseScorecard transitions phoneInterviewer)
      status = status
      tbnStage = tbnStage }