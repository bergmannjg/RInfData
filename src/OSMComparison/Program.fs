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

[<EntryPoint>]
let main argv =
    try
        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--Osm"
                && argv.Length > 1
                && checkIsDir argv.[1] then
            async {
                let file = argv.[1] + $"sparql-osm.json"

                if not (File.Exists file) then
                    let! result = OSM.Sparql.Api.loadOsmData ()
                    fprintfn stderr $"loadOsmData, {result.Length} bytes"
                    File.WriteAllText(file, result)

                let result = readFile<QueryResults> argv.[1] "sparql-osm.json"
                let entries = OSM.Sparql.Api.ToEntries result
                fprintfn stderr $"osm entries: {entries.Length}"

                return JsonSerializer.Serialize entries
            }
        else if argv.[0] = "--Wikidata"
                && argv.Length > 1
                && checkIsDir argv.[1] then
            async {
                let file = argv.[1] + $"sparql-wikidata.json"

                if not (File.Exists file) then
                    let! result = Wikidata.Sparql.Api.loadOsmData ()
                    fprintfn stderr $"loadWikidata, {result.Length} bytes"
                    File.WriteAllText(file, result)

                let result = readFile<QueryResults> argv.[1] "sparql-wikidata.json"
                let entries = Wikidata.Sparql.Api.ToEntries result

                return JsonSerializer.Serialize entries
            }
        else if argv.[0].StartsWith "--Wikidata.Compare"
                && argv.Length > 1
                && checkIsDir argv.[1] then
            async {
                let operationalPoints =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"

                fprintfn stderr $"kg operationalPoints: {operationalPoints.Length}"

                let wikidataEntries =
                    readFile<Wikidata.Sparql.Entry []> argv.[1] "WikidataEntries.json"

                fprintfn stderr $"kg wikidataEntries: {wikidataEntries.Length}"

                Wikidata.Comparison.compare (argv.[0] = "--Wikidata.Compare.Extra") operationalPoints wikidataEntries

                return ""
            }
        else if argv.[0].StartsWith "--Osm.Compare"
                && argv.Length > 1
                && checkIsDir argv.[1] then
            async {
                let operationalPoints =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"

                fprintfn stderr $"kg operationalPoints: {operationalPoints.Length}"

                let osmEntries = readFile<Entry []> argv.[1] "OsmEntries.json"
                fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

                compare (argv.[0] = "--Osm.Compare.Extra") operationalPoints osmEntries

                return ""
            }
        else if argv.[0].StartsWith "--Osm.Query"
                && argv.Length > 2
                && checkIsDir argv.[1] then
            async {
                let osmEntries = readFile<Entry []> argv.[1] "OsmEntries.json"
                fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

                osmEntries
                |> Array.filter (fun entry ->
                    entry.Name = argv.[2]
                    || entry.Stop.Contains argv.[2])
                |> Array.map (fun entry ->
                    printfn $"{entry}"
                    entry)
                |> Array.iter (fun entry ->
                    if entry.RailwayRef.IsSome then
                        printfn $"***** {entry.Stop} {entry.RailwayRef.Value}")

                return ""
            }
        else if argv.[0].StartsWith "--Osm.Get" && argv.Length > 2 then
            async {
                let! json = OSM.Api.loadOsmData argv.[1] argv.[2]
                printfn $"{json}"

                match json with
                | Some osmjson when osmjson.elements.Length > 0 ->
                    let entries = OSM.Api.ToEntries osmjson
                    printfn $"{entries.[0]}"
                | _ -> ()

                return ""
            }
        else
            async {
                fprintfn stderr $"{argv.[0]} unexpected"
                return printHelp ()
            }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with
    | e -> fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
