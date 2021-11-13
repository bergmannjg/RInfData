namespace OSM

open System.Text.Json.Serialization

module Command =

    open System.Diagnostics
    open System.Threading.Tasks

    type CommandResult =
        { ExitCode: int
          StandardOutput: string
          StandardError: string }

    let executeCommand executable args =
        async {
            let startInfo = ProcessStartInfo()
            startInfo.FileName <- executable

            for a in args do
                startInfo.ArgumentList.Add(a)

            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.UseShellExecute <- false
            startInfo.CreateNoWindow <- true
            use p = new Process()
            p.StartInfo <- startInfo
            p.Start() |> ignore

            let outTask =
                Task.WhenAll(
                    [| p.StandardOutput.ReadToEndAsync()
                       p.StandardError.ReadToEndAsync() |]
                )

            do! p.WaitForExitAsync() |> Async.AwaitTask

            let! out = outTask |> Async.AwaitTask

            return
                { ExitCode = p.ExitCode
                  StandardOutput = out.[0]
                  StandardError = out.[1] }
        }

    let executeShellCommand command =
        executeCommand "/usr/bin/env" [ "-S"; "bash"; "-c"; command ]

    let exedir = System.Environment.GetEnvironmentVariable "OSMS3S_EXEDIR"

    let dbdir = System.Environment.GetEnvironmentVariable "OSMS3S_DBDIR"

    let execQuery (query: string) =
        async {
            let cmd =
                "echo \""
                + query.Replace("\"", "\\\"")
                + "\" | "
                + exedir
                + "/bin/osm3s_query --db-dir="
                + dbdir

            let! r = executeShellCommand cmd

            if r.ExitCode <> 0 then
                raise (System.InvalidOperationException(r.StandardError))

            return r.StandardOutput
        }
