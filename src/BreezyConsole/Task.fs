module BreezyConsole.Task

open System.Threading.Tasks
open FSharp.Control.Tasks.V2

let runSequentially (tasks: Task<'a> seq): Task<'a list> =
    task {
        let results = ResizeArray()

        for task in tasks do
            let! result = task
            results.Add(result)

        return results |> Seq.toList
    }

