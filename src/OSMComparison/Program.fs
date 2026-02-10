open System
open System.IO
open System.Text.Json

open Sparql
open EraKG
open OSM.Sparql
open OSM.Comparison

let printHelp () =
    """
USAGE: EraKGOsmComparison

OPTIONS:

    --Osm                 <dataDir>
                          load OSM data.
    --Osm.Compare         <dataDir>
                          compare OSM data.
    --help                display this list of options.
"""

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let checkIsDir (path: string) = Directory.Exists path

let server = "qlever.cs.uni-freiburg.de"

let endpoint (server: string) = $"https://{server}/api/osm-planet"

[<EntryPoint>]
let main argv =
    try
        let getServer () =
            if argv.Length > 2 then argv.[2] else server

        let getEndpoint () = endpoint <| getServer ()

        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--Osm" && argv.Length > 1 && checkIsDir argv.[1] then
            async {
                let file = argv.[1] + $"sparql-osm.json"

                if not (File.Exists file) then
                    let! result = OSM.Sparql.Api.loadData (getEndpoint ())
                    fprintfn stderr $"loadOsmData, {result.Length} bytes"
                    File.WriteAllText(file, result)

                let result = readFile<QueryResults> argv.[1] "sparql-osm.json"
                let entries = OSM.Sparql.Api.fromQueryResults result

                File.WriteAllText(argv.[1] + "OsmEntries.json", JsonSerializer.Serialize entries)

                return ""
            }
        else if argv.[0] = "--Osm.Compare" && argv.Length > 1 && checkIsDir argv.[1] then
            async {
                let pathInput = argv.[1]

                let pathOutput =
                    if argv.Length > 2 && checkIsDir argv.[2] then
                        argv.[2]
                    else
                        argv.[1]

                // operational points with type station or passenger stop in germany
                let ops =
                    readFile<RInfGraph.OpInfo[]> pathInput "OpInfos.json"
                    |> Array.filter (fun op -> op.UOPID.StartsWith "DE" && (op.RinfType = 10 || op.RinfType = 70))

                fprintfn stderr $"kg operationalPoints: {ops.Length}"

                let osmEntries = readFile<Entry[]> pathInput "OsmEntries.json"
                fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

                let result = findMatchings ops osmEntries false
                let opsNotFound = result |> Array.filter _.OsmUrl.IsNone

                fprintfn
                    stderr
                    $"total {ops.Length}, found {ops.Length - opsNotFound.Length}, not found {opsNotFound.Length}"

                File.WriteAllText(pathOutput + "RInfOsmMatchings.json", JsonSerializer.Serialize(result))

                return ""
            }
        else
            async {
                fprintfn stderr $"{argv.[0]} unexpected"
                return printHelp ()
            }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with e ->
        fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
