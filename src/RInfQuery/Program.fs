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
               [--SectionsOfLine.Line <dataDir> <imcode> <line>]
               [--Graph.Route <dataDir> <opIds>] [--Graph.Line <dataDir> <line>]

OPTIONS:

    --OperationalPoints.Line <dataDir> <country> <line>
                          get OperationalPoints of line from file OperationalPoints.json in <dataDir>.
    --SectionsOfLine.Line <dataDir> <imcode> <line>
                          get SectionsOfLine of line from file SectionsOfLine.json in <dataDir>.
    --Graph.Route <dataDir> <opIds>
                          get path of route from <opIds>, ex. "DE000HH;DE000BL"
                          (assumes Graph.json and OpInfos.json in <dataDir>).
    --Graph.Line <dataDir> <line>
                          get path of line
                          (assumes Graph.json, LineInfos.json and OpInfos.json in <dataDir>).
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

let findOpByOPID (ops: OperationalPoint[]) opId =
    match (Array.tryFind (fun (op: OperationalPoint) -> op.UOPID = opId) ops) with
    | Some op -> op
    | None -> raise (System.ArgumentException("opid not found: " + opId))

let toMap (opInfos: OpInfo[]) =
    opInfos
    |> Array.fold
        (fun (m: Dictionary<string, OpInfo>) op ->
            if not (m.ContainsKey op.UOPID) then
                m.Add(op.UOPID, op)

            m)
        (Dictionary<string, OpInfo>())

let isElectrified (sol: EraKG.SectionOfLine) =
    sol.Tracks
    |> Array.choose (fun t -> t.contactLineSystem)
    |> Array.forall (fun s -> not (s.Contains "not electrified"))

[<EntryPoint>]
let main argv =
    try
        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--OperationalPoints.Line" && argv.Length > 3 then
            async {
                let ops = readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let opsOfLine =
                    ops
                    |> Array.filter (fun op ->
                        argv.[2] = op.Country
                        && op.RailwayLocations |> Array.exists (fun loc -> argv.[3] = loc.NationalIdentNum))
                    |> Array.map (fun op ->
                        let loc =
                            op.RailwayLocations |> Array.find (fun loc -> argv.[3] = loc.NationalIdentNum)

                        ({ UOPID = op.UOPID
                           Name = op.Name
                           RinfType = System.Int32.Parse(op.Type.Substring 5)
                           Latitude = op.Latitude
                           Longitude = op.Longitude },
                         loc.Kilometer))
                    |> Array.sortBy (fun (_, km) -> km)
                    |> Array.distinct

                opsOfLine
                |> Array.iter (fun (op, km) -> printfn "%s, %s, rintype/%d, Km: %.1f" op.Name op.UOPID op.RinfType km)

                opsOfLine
                |> Array.map (fun (op, _) ->
                    { Latitude = op.Latitude
                      Longitude = op.Longitude
                      Content = op.UOPID })
                |> Graph.getBRouterPoIUrl
                |> printfn "%s"

                return ""
            }
        else if argv.[0] = "--EraKG.OperationalPoints.Line" && argv.Length > 3 then
            async {
                let ops = readFile<EraKG.OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let opsOfLine =
                    ops
                    |> Array.filter (fun op ->
                        argv.[2] = op.Country
                        && op.RailwayLocations |> Array.exists (fun loc -> argv.[3] = loc.NationalIdentNum))
                    |> Array.map (fun op ->
                        let loc =
                            op.RailwayLocations |> Array.find (fun loc -> argv.[3] = loc.NationalIdentNum)

                        ({ UOPID = op.UOPID
                           Name = op.Name
                           RinfType = System.Int32.Parse(op.Type.Substring 5)
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
        else if argv.[0] = "--SectionsOfLine.Line" && argv.Length > 3 then
            async {
                let sols = readFile<SectionOfLine[]> argv.[1] "SectionsOfLines.json"
                let ops = readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let opsOfLine =
                    ops
                    |> Array.filter (fun op ->
                        op.RailwayLocations |> Array.exists (fun loc -> argv.[3] = loc.NationalIdentNum))
                    |> Array.map (fun op ->
                        let loc =
                            op.RailwayLocations |> Array.find (fun loc -> argv.[3] = loc.NationalIdentNum)

                        (op, loc.Kilometer))
                    |> Array.sortBy (fun (_, km) -> km)
                    |> Array.distinct

                let solsOfLine =
                    sols
                    |> Array.filter (fun sol -> sol.IMCode = argv.[2] && sol.LineIdentification = argv.[3])
                    |> Array.map (fun sol ->
                        (sol,
                         opsOfLine
                         |> Array.tryFind (fun (op, _) -> sol.StartOP.IsSome && op.UOPID = sol.StartOP.Value.UOPID),
                         opsOfLine
                         |> Array.tryFind (fun (op, _) -> sol.EndOP.IsSome && op.UOPID = sol.EndOP.Value.UOPID)))
                    |> Array.sortBy (fun (_, startOp, _) ->
                        match startOp with
                        | Some(_, km) -> km
                        | None -> 0.0)

                solsOfLine
                |> Array.iter (fun (sol, startOp, endOp) ->
                    match startOp, endOp with
                    | Some(startOp, startKm), Some(endOp, endKm) ->
                        printfn
                            "%s/%s - %s/%s, start: %.1f, end: %.1f, length: %.1f, %s"
                            startOp.UOPID
                            startOp.Type
                            endOp.UOPID
                            endOp.Type
                            startKm
                            endKm
                            sol.Length
                            sol.solName
                    | _ -> printfn "ops not found: %s" sol.solName)

                return ""
            }
        else if argv.[0] = "--EraKG.SectionsOfLine.Line" && argv.Length > 3 then
            async {
                let sols = readFile<EraKG.SectionOfLine[]> argv.[1] "SectionsOfLines.json"
                let ops = readFile<EraKG.OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let opsOfLine =
                    ops
                    |> Array.filter (fun op ->
                        op.RailwayLocations |> Array.exists (fun loc -> argv.[3] = loc.NationalIdentNum))
                    |> Array.map (fun op ->
                        let loc =
                            op.RailwayLocations |> Array.find (fun loc -> argv.[3] = loc.NationalIdentNum)

                        (op, loc.Kilometer))
                    |> Array.sortBy (fun (_, km) -> km)
                    |> Array.distinct

                let solsOfLine =
                    sols
                    |> Array.filter (fun sol -> sol.IMCode = argv.[2] && sol.LineIdentification = argv.[3])
                    |> Array.map (fun sol ->
                        (sol,
                         opsOfLine |> Array.tryFind (fun (op, _) -> op.UOPID = sol.StartOP),
                         opsOfLine |> Array.tryFind (fun (op, _) -> op.UOPID = sol.EndOP)))
                    |> Array.sortBy (fun (_, startOp, _) ->
                        match startOp with
                        | Some(_, km) -> km
                        | None -> 0.0)

                let getMaxSpeed (sol: EraKG.SectionOfLine) (defaultValue: int) =
                    sol.Tracks
                    |> Array.choose (fun t -> t.maximumPermittedSpeed)
                    |> Array.tryHead
                    |> Option.defaultValue defaultValue

                solsOfLine
                |> Array.iter (fun (sol, startOp, endOp) ->
                    match startOp, endOp with
                    | Some(startOp, startKm), Some(endOp, endKm) ->
                        printfn
                            "%s/%s - %s/%s, start: %.1f, end: %.1f, len: %.1f, speed: %d electrified: %b %s"
                            startOp.UOPID
                            startOp.Type
                            endOp.UOPID
                            endOp.Type
                            startKm
                            endKm
                            (sol.Length / 1000.0)
                            (getMaxSpeed sol 50)
                            (isElectrified sol)
                            sol.Name
                    | _ -> printfn "ops not found: %s" sol.Name)

                return ""
            }
        else if argv.[0] = "--EraKG.SectionsOfLine.Electrified" && argv.Length > 1 then
            async {
                let sols = readFile<EraKG.SectionOfLine[]> argv.[1] "SectionsOfLines.json"

                sols
                |> Array.groupBy (fun sol -> isElectrified sol)
                |> Array.iter (fun (k, sols) ->
                    printfn
                        "electrified: %s, length: %.0f"
                        (if k then "true " else "false")
                        (sols |> Array.sumBy (fun sol -> sol.Length)))

                return ""
            }
        else if argv.[0] = "--Graph.Line" && argv.Length > 2 then
            async {
                let g = readFile<GraphNode[]> argv.[1] "Graph.json"

                let map = readFile<OpInfo[]> argv.[1] "OpInfos.json" |> toMap

                let graph = Graph.toGraph g

                readFile<LineInfo[]> argv.[1] "LineInfos.json"
                |> Array.filter (fun line -> line.Line = argv.[2])
                |> Array.sortBy (fun line -> line.StartKm)
                |> Array.iter (fun lineInfo ->
                    printfn "%s, StartKm: %.1f, EndKm: %.1f" lineInfo.Name lineInfo.StartKm lineInfo.EndKm

                    let path = Graph.getPathOfLineFromGraph g graph lineInfo

                    Graph.printPathEx map path

                    Graph.getLocationsOfPath g map path
                    |> Array.iter (Graph.getBRouterUrl >> printfn "%s")

                    if lineInfo.Wikipedia.IsSome then
                        let path = lineInfo.Wikipedia.Value.Replace(" ", "%20")
                        printfn $"https://de.wikipedia.org/wiki/{path}")

                return ""
            }
        else if argv.[0] = "--Graph.Route" && argv.Length >= 3 then
            async {
                let useMaxSpeed = (argv.Length = 4 && argv.[3] = "--maxSpeed")
                let g = readFile<GraphNode[]> argv.[1] "Graph.json"

                let map = readFile<OpInfo[]> argv.[1] "OpInfos.json" |> toMap

                let args = argv.[2].Split ";"

                let path = Graph.getShortestPath g args

                if args.Length = 2 && map.ContainsKey args.[0] && map.ContainsKey args.[1] then
                    printfn "Path: %s to %s" map.[args.[0]].Name map.[args.[1]].Name
                else
                    printfn "Path:"

                Graph.printPathEx map path

                Graph.verbose <- true
                let getCompactPath path =
                    if useMaxSpeed then
                        Graph.getCompactPathWithMaxSpeed path g
                    else
                        Graph.getCompactPath path

                printfn "compact Path%s:" (if useMaxSpeed then " with maxSpeed" else "")

                Graph.printPath (getCompactPath path)

                let cpath = Graph.compactifyPath path g

                printfn "compactified Path:"
                Graph.printPath cpath

                printfn "compactified compact Path%s:" (if useMaxSpeed then " with maxSpeed" else "")

                Graph.printPath (getCompactPath cpath)

                printfn "brouter url of compactified path "

                Graph.getLocationsOfPath g map cpath
                |> Array.iter (Graph.getBRouterUrl >> printfn "%s")

                return ""
            }
        else
            async { return printHelp () }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with e ->
        fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
