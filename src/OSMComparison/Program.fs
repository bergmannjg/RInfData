open System
open System.IO
open System.Text.Json

open Sparql
open EraKG
open OSM

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

let filterUnmatchedUOPID (operationalPoints: OperationalPoint []) (osmEntries: Entry []) : OperationalPoint [] =
    let fill (s: string) (len: int) =
        if s.Length < len then
            System.String('0', len - s.Length)
        else
            ""

    /// from railwayRef to opid
    let toOPID (s: string) =
        if s.Length > 0 && s.Length <= 5 then
            "DE" + (fill s 5) + s.Replace(" ", "0")
        else
            ""

    let matchType (op: OperationalPoint) =
        op.Type = "station" || op.Type = "passengerStop"

    let matchUOPID (railwayRef: string) (uOPID: string) =
        toOPID railwayRef = uOPID.Replace(" ", "0")
        || if uOPID.Contains "  "
              && railwayRef.Length = 4
              && railwayRef.Contains " " then
               let railwayRefX = railwayRef.Replace(" ", "  ")
               toOPID railwayRefX = uOPID.Replace(" ", "0") // matches 'TU R' with 'DETU  R'
           else
               false

    operationalPoints
    |> Array.filter (fun op ->
        matchType op
        && osmEntries
           |> Array.exists (fun entry ->
               entry.RailwayRef.IsSome
               && matchUOPID entry.RailwayRef.Value op.UOPID)
           |> not)

let filterByName (operationalPoints: OperationalPoint []) (osmEntries: Entry []) : OperationalPoint [] =
    operationalPoints
    |> Array.filter (fun op ->
        match osmEntries
              |> Array.tryFind (fun entry -> entry.Name = op.Name)
            with
        | Some entry ->
            if entry.RailwayRef.IsSome then
                fprintfn
                    stderr
                    $"found by name {entry.Name}, RailwayRef '{entry.RailwayRef.Value}' distinct from UOPID '{op.UOPID}'"

            true
        | None -> false)

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
                    let! result = OSM.Api.loadOsmData ()
                    fprintfn stderr $"loadOsmData, {result.Length} bytes"
                    File.WriteAllText(file, result)

                let result = readFile<QueryResults> argv.[1] "sparql-osm.json"
                let entries = OSM.Api.toEntries result
                fprintfn stderr $"osm entries: {entries.Length}"

                return JsonSerializer.Serialize entries
            }
        else if argv.[0] = "--Osm.Compare"
                && argv.Length > 1
                && checkIsDir argv.[1] then
            async {
                let operationalPoints =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"

                fprintfn stderr $"kg operationalPoints: {operationalPoints.Length}"

                let osmEntries = readFile<OSM.Entry []> argv.[1] "OsmEntries.json"
                fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

                let operationalPointsNotFound = filterUnmatchedUOPID operationalPoints osmEntries

                let notFound = operationalPointsNotFound.Length
                let found = operationalPoints.Length - notFound

                let opsFoundByName = filterByName operationalPointsNotFound osmEntries
                let foundByName = opsFoundByName.Length

                operationalPointsNotFound
                |> Array.groupBy (fun op -> op.Type)
                |> Array.iter (fun (k, l) -> fprintfn stderr $"type {k}, not found {l.Length}")

                fprintfn stderr $"found {found}, not found {notFound}, foundByName {foundByName}"
                return JsonSerializer.Serialize operationalPointsNotFound
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
