open CommandLine
open DbUp
open System
open System.Reflection
open DbUp.Helpers

type Options =
    { [<Option("connection-string",
               Default = "Host=localhost;Database=postgres;Port=5433;Username=postgres;Password=postgres",
               HelpText = "Connection String")>]
      connectionString: string

      [<Option("clean", HelpText = "Clean the DB before running migrations")>]
      clean: bool }

let cleanScriptsDirectory = "Clean";

let migrateDatabase (connectionString: string) =
    let builder = DeployChanges.To
                     .PostgresqlDatabase(connectionString)
                     .WithScriptsEmbeddedInAssembly(
                         Assembly.GetExecutingAssembly(),
                         fun script -> not(script.Contains(cleanScriptsDirectory)) && script.EndsWith(".sql")
                     )
                     .WithExecutionTimeout(Nullable(TimeSpan.FromSeconds(30.0)))
                     .WithTransactionPerScript()
                     .LogToConsole()

    let result = builder.Build().PerformUpgrade()

    match result.Successful with
    | true ->
        Console.ForegroundColor <- ConsoleColor.Green
        Console.WriteLine("Migrations applied successfully");
        Console.ResetColor();
    | false ->
        raise result.Error

let cleanDatabase (connectionString: string) =
    let result = DeployChanges.To
                     .PostgresqlDatabase(connectionString)
                     .WithScriptsEmbeddedInAssembly(
                         Assembly.GetExecutingAssembly(),
                         fun script -> script.Contains(cleanScriptsDirectory) && script.EndsWith(".sql")
                     )
                     .WithExecutionTimeout(Nullable(TimeSpan.FromSeconds(30.0)))
                     .WithTransactionPerScript()
                     .LogToConsole()
                     .JournalTo(NullJournal())
                     .Build()
                     .PerformUpgrade()

    match result.Successful with
    | true ->
        Console.ForegroundColor <- ConsoleColor.Green
        Console.WriteLine("Cleaned database successfully");
        Console.ResetColor();
    | false ->
        raise result.Error

let run (options: Options) =
    EnsureDatabase.For.PostgresqlDatabase(options.connectionString)
    if (options.clean) then cleanDatabase options.connectionString
    migrateDatabase options.connectionString

[<EntryPoint>]
let main argv =
  let result = Parser.Default.ParseArguments<Options>(argv)
  match result with
  | :? Parsed<Options> as parsed ->
      run parsed.Value
      0 // Return success exit code
  | _ -> -1 // Failure exit code
