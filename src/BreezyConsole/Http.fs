module BreezyConsole.Http

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

let toSnakeCase (str: string): string =
    let mapCharacter c: string =
        if Char.IsUpper(c) then
            $"_{Char.ToLower(c)}"
        else $"{c}"

    str |> Seq.map mapCharacter |> String.concat ""

type SnakeCaseNamingConverter() =
    inherit JsonNamingPolicy()
    override this.ConvertName(name) = toSnakeCase name

let jsonOptions = JsonSerializerOptions()
jsonOptions.PropertyNamingPolicy <- SnakeCaseNamingConverter()
jsonOptions.Converters.Add(JsonFSharpConverter())
let inline deserializeJson (stream: Stream) = JsonSerializer.DeserializeAsync<'a>(stream, jsonOptions).AsTask()
let deserializeJsonString (str: string) = JsonSerializer.Deserialize<'a>(str, jsonOptions)
let serializeJson (content: 'a) = JsonSerializer.Serialize(content, jsonOptions)

let jsonContent content: StringContent =
    new StringContent(serializeJson content, Encoding.UTF8, "application/json")

let get<'a> (url: string) (httpClient: HttpClient): Task<'a> =
    task {
        use! response = httpClient.GetAsync(url)
        response.EnsureSuccessStatusCode() |> ignore

        use! responseStream = response.Content.ReadAsStreamAsync()
        return! deserializeJson responseStream
    }
