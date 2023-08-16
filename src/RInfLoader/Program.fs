open System
open System.IO
open System.Text.Json

open RInf
open RInfGraph

let username = Environment.GetEnvironmentVariable "RINF_USERNAME"

let password = Environment.GetEnvironmentVariable "RINF_PASSWORD"

let toCountries (s: string) =
    if s.Length = 0 then [| "Germany" |] else s.Split [| ',' |]

let printHelp () =
    """
USAGE: RInfLoader
               [--help] [--DatasetImports] [--SectionsOfLines] [--OperationalPoints]
               [--SOLTrackParameters <sol file>] [--OpInfo.Build <dataDir>]
               [--LineInfo.Build <dataDir>] [--Graph.Build <dataDir>]

OPTIONS:

    --DatasetImports      load DatasetImports (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SectionsOfLines     <countries>
                          load SectionsOfLines (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --OperationalPoints   <countries>
                          load OperationalPoints (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SOLTrackParameters <SectionsOfLines file>
                          load SOLTrackParameters for all SectionsOfLines
                          (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SOLTunnels <SectionsOfLines file>
                          load SOLTunnels for all SectionsOfLines
                          (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --OpInfo.Build <dataDir>
                          build OpInfos from file OperationalPoints.json in <dataDir>.
    --LineInfo.Build <dataDir>
                          build LineInfos from files SectionsOfLines.json and OperationalPoints.json in <dataDir>.
    --TunnelInfo.Build <dataDir>
                          build TunnelInfos from files SectionsOfLines.json and SOLTunnels.json in <dataDir>.
    --Graph.Build <dataDir>
                          build graph of OperationalPoints and SectionsOfLines from files SectionsOfLines.json,
                          OperationalPoints.json and SOLTrackParameters.json in <dataDir>.
    --Line.MissingSols <dataDir>
                          analyze missing SectionsOfLines
    --help                display this list of options.
"""

let getExtendedSectionsOfLines (withExpand: string) (ids: (int * int)[]) =
    async {
        try
            fprintfn stderr "new Api.Client, %A" ids.[0]

            use client = new Api.Client(username, password)

            let mutable results = List.empty

            for (id, versionId) in ids do
                let! result = client.GetSectionsOfLine(id, versionId, withExpand)
                results <- result :: results

            return results
        with error ->
            fprintfn stderr "%s" error.Message
            return List.Empty
    }

/// see http://www.fssnip.net/hr/title/Async-function-that-retries-work
let rec retry work resultOk retries =
    async {
        let! res = work

        if (resultOk res) || (retries = 0) then
            return res
        else
            return! retry work resultOk (retries - 1)
    }

let getExtendedSectionsOfLinesWithRetry (withExpand: string) ids =
    retry (getExtendedSectionsOfLines withExpand ids) (fun result -> result.Length > 0) 3

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
    ops |> Array.tryFind (fun (op: OperationalPoint) -> op.UOPID = opId)

let getLineInfo (name: string) ops (nodesOfLine: GraphNode list) (tunnels: TunnelInfo[]) imcode line =

    let firstOp = findOpByOPID ops (nodesOfLine |> List.head).Node

    let lastOp = findOpByOPID ops (nodesOfLine |> List.last).Edges.[0].Node

    match firstOp, lastOp with
    | Some firstOp, Some lastOp ->
        let uOPIDs =
            nodesOfLine
            |> List.rev
            |> List.fold
                (fun (s: string list) sol ->
                    match findOpByOPID ops sol.Node with
                    | Some op -> op.UOPID :: s
                    | None -> s)
                [ lastOp.UOPID ]
            |> List.toArray

        let tunnelsOfLine =
            tunnels
            |> Array.filter (fun t -> t.Line = line)
            |> Array.sortBy (fun t -> t.StartKm)
            |> Array.map (fun t -> t.Tunnel)
            |> Array.distinct

        Some
            { Line = line
              IMCode = imcode
              Name = name
              Length =
                nodesOfLine
                |> List.sumBy (fun sol -> if sol.Edges.Length > 0 then sol.Edges.[0].Length else 0.0)
                |> sprintf "%.1f"
                |> Double.Parse
              StartKm = kilometerOfLine firstOp line
              EndKm = kilometerOfLine lastOp line
              UOPIDs = uOPIDs
              Tunnels = tunnelsOfLine }
    | _ -> None

let buildLineInfo
    (ops: OperationalPoint[])
    (nodes: GraphNode[])
    (tunnels: TunnelInfo[])
    (imcode: string, line: string)
    : LineInfo[] =
    let solsOfLine =
        nodes
        |> Array.filter (fun sol ->
            sol.Edges
            |> Array.exists (fun e -> e.Line = line && e.IMCode = imcode && e.StartKm < e.EndKm))
        |> Array.map (fun sol ->
            { sol with
                Edges =
                    sol.Edges
                    |> Array.filter (fun e -> e.Line = line && e.IMCode = imcode && e.StartKm < e.EndKm) })
        |> Array.sortBy (fun n -> n.Edges.[0].StartKm)

    let getFirstNodes (solsOfLine: GraphNode[]) =
        solsOfLine
        |> Array.filter (fun solX -> not (solsOfLine |> Array.exists (fun solY -> solX.Node = solY.Edges.[0].Node)))

    let firstNodes = getFirstNodes solsOfLine

    let rec getNextNodes (solsOfLine: GraphNode[]) (startSol: GraphNode) (nextNodes: GraphNode list) : GraphNode list =
        let nextNode =
            solsOfLine |> Array.tryFind (fun sol -> sol.Node = startSol.Edges.[0].Node)

        match nextNode with
        | Some nextNode -> getNextNodes solsOfLine nextNode (nextNode :: nextNodes)
        | None -> nextNodes |> List.rev

    let nextNodesLists =
        firstNodes
        |> Array.map (fun firstNode -> getNextNodes solsOfLine firstNode [ firstNode ])

    if nextNodesLists.Length > 0 then
        let firstOp = findOpByOPID ops nextNodesLists.[0].Head.Node

        let lastElem = nextNodesLists.[nextNodesLists.Length - 1] |> List.toArray

        let lastOp = findOpByOPID ops lastElem.[lastElem.Length - 1].Edges.[0].Node

        match firstOp, lastOp with
        | Some firstOp, Some lastOp ->
            nextNodesLists
            |> Array.map (fun nextNodes ->
                getLineInfo (firstOp.Name + " - " + lastOp.Name) ops nextNodes tunnels imcode line)
            |> Array.choose id
        | _ -> [||]
    else
        [||]

let buildLineInfos (ops: OperationalPoint[]) (nodes: GraphNode[]) (tunnels: TunnelInfo[]) : LineInfo[] =
    nodes
    |> Array.collect (fun sol -> sol.Edges |> Array.map (fun e -> (e.IMCode, e.Line)))
    |> Array.distinct
    |> Array.collect (buildLineInfo ops nodes tunnels)
    |> Array.sortBy (fun line -> line.Line)

let getMaxSpeed (sol: SectionOfLine) (trackParamsOfSol: SOLTrackParameter[]) (defaultValue: int) =
    trackParamsOfSol
    |> Array.tryFind (fun param -> param.ID = "IPP_MaxSpeed" && not (isNull param.Value))
    |> (fun param ->
        match param with
        | Some param -> int param.Value
        | None ->
            fprintfn stderr "sol %s, maxspeed not found" sol.solName
            defaultValue)

// missing tunnels in extraEdges
let extraTunnels: TunnelInfo[] =
    [| { Tunnel = "Landrückentunnel"
         Length = 262.104 - 251.325
         StartLong = 9.650944
         StartLat = 50.406190
         StartKm = Some 251.325
         StartOP = "DE00FFU"
         EndLong = 9.663088
         EndLat = 50.309693
         EndKm = Some 262.104
         EndOP = "DE0NMOT"
         SingelTrack = false
         Line = "1733" } |]

let buildTunnelInfos (sols: SectionOfLine[]) (trackParams: SOLTrackParameter[]) (tunnels: SOLTunnel[]) : TunnelInfo[] =
    let now = System.DateTime.Now

    tunnels
    |> Array.filter (fun t -> now <= t.ValidityDateEnd)
    |> Array.map (fun t ->
        let line, sol, singelTrack =
            match
                sols
                |> Array.tryFind (fun sol -> sol.SOLTracks |> Array.exists (fun track -> track.TrackID = t.TrackID))
            with
            | Some sol ->
                sol.LineIdentification,
                sol,
                (sol.SOLTracks
                 |> Array.exists (fun tr -> tr.SOLTrackIdentification = "single-track"))
            | None ->
                raise (
                    System.ArgumentException(
                        sprintf "sol not found for trackid: %d %s" t.TrackID t.SOLTunnelIdentification
                    )
                )

        { Tunnel = t.SOLTunnelIdentification
          Length = t.EndKm - t.StartKm
          StartLong = t.StartLong
          StartLat = t.StartLat
          StartKm = Some t.StartKm
          StartOP = sol.StartOP.Value.UOPID
          EndLong = t.EndLong
          EndLat = t.EndLat
          EndKm = Some t.EndKm
          EndOP = sol.EndOP.Value.UOPID
          SingelTrack = singelTrack
          Line = line })
    |> fun tunnels -> Array.concat [ tunnels; extraTunnels ]
    |> Array.distinct

let nullDefaultValue<'a when 'a: null> (defaultValue: 'a) (value: 'a) =
    if isNull value then defaultValue else value

let scale (maxSpeed: int) = 1.0

let travelTime (length: float) (maxSpeed: int) =
    length / (float (maxSpeed) * (scale maxSpeed))

let getCost (length: float) (maxSpeed: int) =
    let cost = int (10000.0 * (travelTime length maxSpeed))

    if (cost <= 0) then 1 else cost

let removeSols (sols: SectionOfLine[]) =
    sols
    |> Array.filter (fun sol ->
        not (
            ExtraEdges.removableEdges
            |> Array.exists (fun (line, opStart, opEnd) ->
                sol.LineIdentification = line
                && sol.StartOP.IsSome
                && sol.StartOP.Value.UOPID = opStart
                && sol.EndOP.IsSome
                && sol.EndOP.Value.UOPID = opEnd)
        ))

let buildGraph
    (ops: OperationalPoint[])
    (allSols: SectionOfLine[])
    (trackParams: SOLTrackParameter[])
    (extraEdges: bool)
    =
    let sols = if extraEdges then (removeSols allSols) else allSols

    let mutable graph = Map.empty

    let addOp (op: OperationalPoint) =
        if not (graph.ContainsKey op.UOPID) then
            graph <- graph.Add(op.UOPID, List.empty)

    let findOp opId =
        ops |> Array.tryFind (fun op -> op.ID = opId)

    let passengersLineCats = [| "10"; "20"; "30"; "40"; "50"; "60" |]

    let getPassengerLineCat (trackParamsOfSol: SOLTrackParameter[]) =
        trackParamsOfSol
        |> Array.tryFind (fun param -> param.ID = "IPP_LineCat" && passengersLineCats |> Array.contains (param.Value))

    let addSol (sol: SectionOfLine) =
        match findOp sol.OPStartID, findOp sol.OPEndID with
        | Some(opStart), Some(opEnd) ->
            let trackParamsOfSol =
                trackParams
                |> Array.filter (fun param ->
                    sol.SOLTracks |> Array.exists (fun t -> t.TrackID = param.SOLTrack.TrackID))

            let lineCat =
                match getPassengerLineCat trackParamsOfSol with
                | Some lineCat -> Some lineCat.Value
                | None -> Some "60" // ignore missing linecat

            if lineCat.IsSome then
                let maxSpeed = getMaxSpeed sol trackParamsOfSol 100
                let cost = getCost sol.Length maxSpeed

                graph <-
                    graph.Add(
                        opStart.UOPID,
                        { Node = opEnd.UOPID
                          Cost = cost
                          Line = sol.LineIdentification
                          IMCode = sol.IMCode
                          MaxSpeed = maxSpeed
                          StartKm = kilometerOfLine opStart sol.LineIdentification
                          EndKm = kilometerOfLine opEnd sol.LineIdentification
                          Length = sol.Length }
                        :: graph.[opStart.UOPID]
                    )

                graph <-
                    graph.Add(
                        opEnd.UOPID,
                        { Node = opStart.UOPID
                          Cost = cost
                          Line = sol.LineIdentification
                          IMCode = sol.IMCode
                          MaxSpeed = maxSpeed
                          StartKm = kilometerOfLine opEnd sol.LineIdentification
                          EndKm = kilometerOfLine opStart sol.LineIdentification
                          Length = sol.Length }
                        :: graph.[opEnd.UOPID]
                    )
        | _ -> fprintfn stderr "not found, %A or %A" sol.OPStartID sol.OPEndID

    ops |> Array.iter addOp

    sols |> Array.iter addSol

    fprintfn stderr "Nodes: %i, Edges: %i" graph.Count (graph |> Seq.sumBy (fun item -> item.Value.Length))

    if extraEdges then
        graph <- ExtraEdges.addExtraEdges graph

    graph
    |> Seq.map (fun kv ->
        { Node = kv.Key
          Edges = graph.[kv.Key] |> List.toArray })
    |> Seq.toArray

[<EntryPoint>]
let main argv =
    try
        use client = new Api.Client(username, password)

        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--DatasetImports" then
            async {
                let! response = client.GetDatasetImports()
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SectionsOfLine" && argv.Length > 3 then
            async {
                let! response = client.GetSectionsOfLine(int argv.[1], int argv.[2], "", true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SectionsOfLines" && argv.Length > 1 then
            async {
                let! response = client.GetSectionsOfLines(toCountries argv.[1])

                let responseMapped =
                    response
                    |> Array.map (fun sol ->
                        { sol with
                            OPStartID =
                                if sol.StartOP.IsNone then
                                    sol.OPStartID
                                else
                                    sol.StartOP.Value.ID
                            OPEndID = if sol.EndOP.IsNone then sol.OPEndID else sol.EndOP.Value.ID

                        })

                return JsonSerializer.Serialize responseMapped
            }
        else if argv.[0] = "--Routes" && argv.Length > 2 then
            async {
                let! response = client.GetRoutes(argv.[1], argv.[2], true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--OperationalPoints" && argv.Length > 1 then
            async {
                let! response = client.GetOperationalPoints(toCountries argv.[1])

                let filter (opTracks: OPTrack[]) cond =
                    opTracks
                    |> nullDefaultValue [||]
                    |> Array.map (fun t ->
                        { t with
                            OPTrackParameters = t.OPTrackParameters |> nullDefaultValue [||] |> Array.filter cond })

                let response =
                    response
                    |> Array.map (fun op ->
                        { op with
                            OPTracks = filter op.OPTracks (fun p -> p.IsApplicable = "Y") })

                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--OperationalPoint" && argv.Length > 2 then
            async {
                let! response = client.GetOperationalPoint(int argv.[1], int argv.[2], true, true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SOLTrackParameters" && argv.Length > 1 then
            let operations =
                JsonSerializer.Deserialize<SectionOfLine[]>(File.ReadAllText argv.[1])
                |> Array.map (fun sol -> (sol.ID, sol.VersionID))
                |> Array.splitInto 100
                |> Array.map (getExtendedSectionsOfLinesWithRetry "SOLTrackParameters")

            let filters = [| "IPP_LineCat"; "IPP_MaxSpeed"; "IPP_TENClass" |]

            async {
                let! response = operations |> Async.Sequential

                let response =
                    response
                    |> List.concat
                    |> List.toArray
                    |> Array.collect (fun sol -> nullDefaultValue [||] sol.SOLTracks)
                    |> Array.collect (fun track ->
                        track.SOLTrackParameters
                        |> nullDefaultValue [||]
                        |> Array.filter (fun tp -> tp.IsApplicable = "Y" && filters |> Array.contains tp.ID))

                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SOLTunnels" && argv.Length > 1 then
            let operations =
                JsonSerializer.Deserialize<SectionOfLine[]>(File.ReadAllText argv.[1])
                |> Array.map (fun sol -> (sol.ID, sol.VersionID))
                |> Array.splitInto 100
                |> Array.map (getExtendedSectionsOfLinesWithRetry "SOLTunnels")

            async {
                let! response = operations |> Async.Sequential

                let response =
                    response
                    |> List.concat
                    |> List.toArray
                    |> Array.collect (fun sol -> nullDefaultValue [||] sol.SOLTracks)
                    |> Array.collect (fun track -> track.SOLTunnels |> nullDefaultValue [||])

                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--Graph.Build" && argv.Length > 1 then
            async {
                let ops = readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let sols = readFile<SectionOfLine[]> argv.[1] "SectionsOfLines.json"

                let trackParams = readFile<SOLTrackParameter[]> argv.[1] "SOLTrackParameters.json"

                let extraEdges = not (argv.Length > 2 && argv.[2] = "--noExtraEdges")

                let g = buildGraph ops sols trackParams extraEdges

                return JsonSerializer.Serialize g
            }
        else if argv.[0] = "--OpInfo.Build" && argv.Length > 1 then
            async {
                let now = System.DateTime.Now

                let ops =
                    readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"
                    |> Array.filter (fun op -> now < op.ValidityDateEnd)
                    |> Array.map (fun op ->
                        { UOPID = op.UOPID
                          Name = op.Name
                          Latitude = op.Latitude
                          Longitude = op.Longitude })

                return JsonSerializer.Serialize ops
            }
        else if argv.[0] = "--TunnelInfo.Build" && argv.Length > 1 then
            async {
                let sols = readFile<SectionOfLine[]> argv.[1] "SectionsOfLines.json"

                let trackParams = readFile<SOLTrackParameter[]> argv.[1] "SOLTrackParameters.json"

                let tunnels = readFile<SOLTunnel[]> argv.[1] "SOLTunnels.json"

                let tunnelInfos = buildTunnelInfos sols trackParams tunnels

                return JsonSerializer.Serialize tunnelInfos
            }
        else if argv.[0] = "--LineInfo.Build" && argv.Length > 1 then
            async {
                let ops = readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let nodes = readFile<GraphNode[]> argv.[1] "Graph.json"

                let tunnels = readFile<TunnelInfo[]> argv.[1] "TunnelInfos.json"

                let lineInfos = buildLineInfos ops nodes tunnels

                return JsonSerializer.Serialize lineInfos
            }
        else if argv.[0] = "--Line.MissingSols" && argv.Length > 1 then
            async {
                // checked by https://geovdbn.deutschebahn.com/isr
                let linesWithSolsNotInUse =
                    [| "1023"
                       "1570"
                       "1711"
                       "2273"
                       "2850"
                       "2854"
                       "2961"
                       "2982"
                       "3005"
                       "3450"
                       "4330"
                       "5330"
                       "5340"
                       "5919"
                       "6088"
                       "6214"
                       "6253"
                       "6311"
                       "6386"
                       "6425"
                       "6680"
                       "6697"
                       "6726"
                       "6759"
                       "6901"
                       "6938" |]

                let g = readFile<GraphNode[]> argv.[1] "Graph-orig.json"

                let estimateMaxSpeed (op: string) (line: string) =
                    g
                    |> Array.tryFind (fun n -> n.Node = op)
                    |> fun n ->
                        match n with
                        | Some n -> n.Edges |> Array.tryFind (fun e -> e.Line = line)
                        | None -> None
                    |> Option.map (fun e -> e.MaxSpeed)
                    |> Option.defaultValue 100

                let ops = readFile<OperationalPoint[]> argv.[1] "OperationalPoints.json"

                let lineInfos = buildLineInfos ops g [||]

                lineInfos
                |> Array.groupBy (fun line -> (line.Line, line.IMCode))
                |> Array.filter (fun ((line, imCode), infos) ->
                    imCode = "0080"
                    && not (linesWithSolsNotInUse |> Array.contains line)
                    && infos.Length > 1)
                |> Array.iter (fun ((line, _), infos) ->
                    let sorted = infos |> Array.sortBy (fun info -> info.StartKm)

                    if sorted.Length = 2 && sorted.[1].StartKm > sorted.[0].EndKm then
                        let dist = Math.Abs(sorted.[1].StartKm - sorted.[0].EndKm)

                        let fromOp = sorted.[0].UOPIDs.[sorted.[0].UOPIDs.Length - 1]
                        let toOp = sorted.[1].UOPIDs.[0]
                        let maxSpeed = estimateMaxSpeed fromOp line

                        if dist < 50.0 then
                            printfn
                                "\"%s\" \"%s\" \"%s\" %d %d %.3f %.3f %.3f"
                                fromOp
                                toOp
                                line
                                (getCost dist maxSpeed)
                                maxSpeed
                                sorted.[0].EndKm
                                sorted.[1].StartKm
                                dist

                    if sorted.Length = 2 && sorted.[1].StartKm < sorted.[0].EndKm then
                        let intersect =
                            Set.intersect (sorted.[0].UOPIDs |> Set.ofArray) (sorted.[1].UOPIDs |> Set.ofArray)

                        fprintfn stderr "line with overlapping sols: %s, intersect opids: %A" line intersect)

                return ""
            }
        else
            async { return printHelp () }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with e ->
        fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
