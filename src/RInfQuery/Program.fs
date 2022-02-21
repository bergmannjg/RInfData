open System
open System.IO
open System.Text.Json
open System.Collections.Generic

open RInf
open RInfGraph

let printHelp () =
    """
USAGE: RInfQuery
               [--help] [--OperationalPoints.Line <dataDir> <line>]
               [--Compare.Line <line>] [--Compare.Line.Remote <line>]  [--Compare.Lines <maxlines>]
               [--Graph.Route <dataDir> <ops>] [--Graph.Line <dataDir> <line>]

OPTIONS:

    --OperationalPoints.Line <dataDir> <line>
                          get OperationalPoints of line from file OperationalPoints.json in <dataDir>.
    --Graph.Route <dataDir> <opIds>
                          get path of route from <opIds>, ex. "DE   HH;DE   BL"
                          (assumes Graph.json and OpInfos.json in <dataDir>).
    --Graph.Line <dataDir> <line>
                          get path of line
                          (assumes Graph.json, LineInfos.json and OpInfos.json in <dataDir>).
    --Compare.Line <line> compare local RInf and local OSM data of line.
    --Compare.Line.Remote <line>
                          compare local RInf and remote OSM data of line.
    --Compare.Lines <maxlines>
                          compare local RInf and local OSM data of max lines.
    --help                display this list of options.
"""

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let kilometerOfLine (op: OperationalPoint) (line: string) =
    op.RailwayLocations
    |> Array.tryFind (fun loc -> loc.NationalIdentNum = line)
    |> Option.map (fun loc -> loc.Kilometer)
    |> Option.defaultValue 0.0

let findOp ops opId =
    ops |> Array.find (fun op -> op.ID = opId)

let findOpByOPID (ops: OperationalPoint []) opId =
    match (Array.tryFind (fun (op: OperationalPoint) -> op.UOPID = opId) ops) with
    | Some op -> op
    | None -> raise (System.ArgumentException("opid not found: " + opId))

let getLineInfo (name: string) ops (solsOfLine: GraphNode list) line =

    let firstOp = findOpByOPID ops (solsOfLine |> List.head).Node

    let lastOp = findOpByOPID ops (solsOfLine |> List.last).Edges.[0].Node

    let uOPIDs =
        solsOfLine
        |> List.rev
        |> List.fold (fun (s: string list) sol -> ((findOpByOPID ops sol.Node).UOPID :: s)) [ lastOp.UOPID ]
        |> List.toArray

    { Line = line
      Name = name
      Length =
        solsOfLine
        |> List.sumBy (fun sol ->
            if sol.Edges.Length > 0 then
                sol.Edges.[0].Length
            else
                0.0)
        |> sprintf "%.1f"
        |> Double.Parse
      StartKm = kilometerOfLine firstOp line
      EndKm = kilometerOfLine lastOp line
      UOPIDs = uOPIDs }

let toMap (opInfos: OpInfo []) =
    opInfos
    |> Array.fold
        (fun (m: Dictionary<string, OpInfo>) op ->
            if not (m.ContainsKey op.UOPID) then
                m.Add(op.UOPID, op)

            m)
        (Dictionary<string, OpInfo>())

let osmDataDir = "../../osm-data/"

let rinfDataDir = "../../rinf-data/"

let dbDataDir = "../../db-data/"

let compareLineAsync (line: int) (useRemote: bool) (useAllStops: bool) =
    Comparison.compareLineAsync osmDataDir rinfDataDir dbDataDir line useRemote useAllStops (Some "Deutschland")

[<EntryPoint>]
let main argv =
    try
        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--OperationalPoints.Line"
                && argv.Length > 2 then
            async {
                let ops = readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"

                let opsOfLine =
                    ops
                    |> Array.filter (fun op ->
                        op.RailwayLocations
                        |> Array.exists (fun loc -> argv.[2] = loc.NationalIdentNum))
                    |> Array.map (fun op ->
                        let loc =
                            op.RailwayLocations
                            |> Array.find (fun loc -> argv.[2] = loc.NationalIdentNum)

                        ({ UOPID = op.UOPID
                           Name = op.Name
                           Latitude = op.Latitude
                           Longitude = op.Longitude },
                         loc.Kilometer))
                    |> Array.sortBy (fun (_, km) -> km)
                    |> Array.distinct

                opsOfLine
                |> Array.iter (fun (op, km) -> printfn "%s, %s, Km: %.1f" op.Name op.UOPID km)

                opsOfLine
                |> Array.map (fun (op, _) ->
                    { Latitude = op.Latitude
                      Longitude = op.Longitude
                      Content = op.UOPID })
                |> Graph.getBRouterPoIUrl
                |> printfn "%s"

                return ""
            }
        else if argv.[0] = "--Graph.Line" && argv.Length > 2 then
            async {
                let g = readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> toMap

                let graph = Graph.toGraph g

                readFile<LineInfo []> argv.[1] "LineInfos.json"
                |> Array.filter (fun line -> line.Line = argv.[2])
                |> Array.sortBy (fun line -> line.StartKm)
                |> Array.iter (fun lineInfo ->
                    printfn "%s, StartKm: %.1f, EndKm: %.1f" lineInfo.Name lineInfo.StartKm lineInfo.EndKm

                    let path = Graph.getPathOfLineFromGraph g graph lineInfo

                    Graph.printPath path

                    Graph.getLocationsOfPath g map path
                    |> Array.iter (Graph.getBRouterUrl >> printfn "%s"))

                return ""
            }
        else if argv.[0] = "--Graph.Route" && argv.Length = 3 then
            async {
                let g = readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> toMap

                let args = argv.[2].Split ";"

                let path = Graph.getShortestPath g args

                printfn "Path:"
                Graph.printPath path
                printfn "compact Path:"
                Graph.printPath (Graph.getCompactPath path)

                Graph.getLocationsOfPath g map path
                |> Array.iter (Graph.getBRouterUrl >> printfn "%s")

                return ""
            }
        else if argv.[0] = "--Compare.Line" && argv.Length >= 2 then
            async {
                do!
                    compareLineAsync (int argv.[1]) false (argv.Length = 3 && argv.[2] = "--allStops")
                    |> Async.Ignore

                return ""
            }
        else if argv.[0] = "--Compare.Line.Remote"
                && argv.Length >= 2 then
            async {
                do!
                    compareLineAsync (int argv.[1]) true (argv.Length = 3 && argv.[2] = "--allStops")
                    |> Async.Ignore

                return ""
            }
        else if argv.[0] = "--Compare.Lines" && argv.Length > 1 then
            async {
                do!
                    Comparison.compareLinesAsync osmDataDir rinfDataDir dbDataDir (int argv.[1])
                    |> Async.Ignore

                return ""
            }
        else if argv.[0] = "--Compare.CheckMissingStops" then
            async {
                do!
                    MissingStops.checkMissingStops rinfDataDir
                    |> Async.Ignore

                return ""
            }
        else
            async { return printHelp () }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with
    | e -> fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
