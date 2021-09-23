open System
open System.IO
open System.Text.Json

open RInf
open RInfGraph

let username =
    Environment.GetEnvironmentVariable "RINF_USERNAME"

let password =
    Environment.GetEnvironmentVariable "RINF_PASSWORD"

let country =
    match Environment.GetEnvironmentVariable "RINF_COUNTRY" with
    | null -> "Germany"
    | v -> v

let getSectionsOfLines (ids: (int * int) []) =
    async {
        try
            fprintfn stderr "new Api.Client, %A" ids.[0]

            use client =
                new Api.Client(username, password, country)

            let mutable results = List.empty

            for (id, versionId) in ids do
                let! result = client.GetSectionsOfLine(id, versionId, true)
                results <- result :: results

            return results
        with
        | error ->
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

let getSectionsOfLinesWithRetry ids =
    retry (getSectionsOfLines ids) (fun result -> result.Length > 0) 3

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let getLineInfo ops (solsOfLine: SectionOfLine list) line =
    let findOp ops opId =
        ops |> Array.find (fun op -> op.ID = opId)

    let firstOp =
        findOp ops (solsOfLine |> List.head).OPStartID

    let lastOp =
        findOp ops (solsOfLine |> List.last).OPEndID

    let kilometerOfLine (op: OperationalPoint) (line: string) =
        op.RailwayLocations
        |> Array.tryFind (fun loc -> loc.NationalIdentNum = line)
        |> Option.map (fun loc -> loc.Kilometer)
        |> Option.defaultValue 0.0

    let uOPIDs =
        solsOfLine
        |> List.rev
        |> List.fold (fun (s: string list) sol -> ((findOp ops sol.OPStartID).UOPID :: s)) [ lastOp.UOPID ]
        |> List.toArray

    { Line = line
      Name = firstOp.Name + " - " + lastOp.Name
      Length =
          solsOfLine
          |> List.sumBy (fun sol -> sol.Length)
          |> sprintf "%.1f"
          |> Double.Parse
      StartKm = kilometerOfLine firstOp line
      EndKm = kilometerOfLine lastOp line
      UOPIDs = uOPIDs }

let getLine (ops: OperationalPoint []) (sols: SectionOfLine []) (line: string) : LineInfo [] =
    let solsOfLine =
        sols
        |> Array.filter (fun sol -> sol.LineIdentification = line)

    let getFirstNodes (solsOfLine: SectionOfLine []) =
        solsOfLine
        |> Array.filter
            (fun solX ->
                not (
                    solsOfLine
                    |> Array.exists (fun solY -> solX.OPStartID = solY.OPEndID)
                ))

    let firstNodes = getFirstNodes solsOfLine

    let rec getNextNodes
        (solsOfLine: SectionOfLine [])
        (startSol: SectionOfLine)
        (nextNodes: SectionOfLine list)
        : SectionOfLine list =
        let nextNode =
            solsOfLine
            |> Array.tryFind (fun sol -> sol.OPStartID = startSol.OPEndID)

        match nextNode with
        | Some nextNode -> getNextNodes solsOfLine nextNode (nextNode :: nextNodes)
        | None -> nextNodes |> List.rev

    if firstNodes.Length > 0 then
        fprintfn stderr "line %s, %d sols" line solsOfLine.Length

        firstNodes
        |> Array.iter (fun sol -> fprintfn stderr "first %s" sol.solName)

        solsOfLine
        |> Array.iter (fun sol -> fprintfn stderr "sol %s" sol.solName)

    firstNodes
    |> Array.map
        (fun firstNode ->
            let nextNodes =
                getNextNodes solsOfLine firstNode [ firstNode ]

            getLineInfo ops nextNodes line)


let getLines (ops: OperationalPoint []) (sols: SectionOfLine []) : LineInfo [] =
    sols
    |> Array.map (fun sol -> sol.LineIdentification)
    |> Array.distinct
    |> Array.collect (fun line -> getLine ops sols line)
    |> Array.sortBy (fun line -> int line.Line)

let build (ops: OperationalPoint []) (sols: SectionOfLine []) (trackParams: SOLTrackParameter []) =
    let mutable graph = Map.empty

    let addOp (op: OperationalPoint) =
        if not (graph.ContainsKey op.UOPID) then
            graph <- graph.Add(op.UOPID, List.empty)

    let findOp opId =
        ops |> Array.tryFind (fun op -> op.ID = opId)

    let getMaxSpeed (trackParamsOfSol: SOLTrackParameter []) =
        trackParamsOfSol
        |> Array.tryFind
            (fun param ->
                param.ID = "IPP_MaxSpeed"
                && not (isNull param.Value))
        |> Option.map (fun param -> int param.Value)

    let travelTime (length: float) (maxSpeed: int) =
        int ((length * 3600.0) / (float maxSpeed))

    let getCost (sol: SectionOfLine) (trackParamsOfSol: SOLTrackParameter []) =
        match getMaxSpeed trackParamsOfSol with
        | Some maxSpeed -> Some(travelTime sol.Length maxSpeed)
        | None ->
            if trackParamsOfSol.Length > 0 then
                fprintfn stderr "sol %s, maxspeed not found" sol.solName
                Some(travelTime sol.Length 100)
            else
                None

    let passengersLineCats = [| "10"; "20"; "30"; "40"; "50"; "60" |]

    let hasPassengerLineCat (trackParamsOfSol: SOLTrackParameter []) =
        trackParamsOfSol
        |> Array.exists
            (fun param ->
                param.ID = "IPP_LineCat"
                && passengersLineCats |> Array.contains (param.Value))

    let addSol (sol: SectionOfLine) =
        match findOp sol.OPStartID, findOp sol.OPEndID with
        | Some (opStart), Some (opEnd) ->
            let trackIds =
                sol.SOLTracks
                |> Array.map (fun tracks -> tracks.TrackID)
                |> Array.distinct

            let trackParamsOfSol =
                trackParams
                |> Array.filter
                    (fun param ->
                        trackIds
                        |> Array.exists (fun t -> t = param.TrackID))

            if (hasPassengerLineCat trackParamsOfSol) then

                match getCost sol trackParamsOfSol with
                | Some cost ->
                    graph <-
                        graph.Add(
                            opStart.UOPID,
                            { Node = opEnd.UOPID
                              Cost = cost
                              Line = sol.LineIdentification
                              Length = sol.Length }
                            :: graph.[opStart.UOPID]
                        )

                    graph <-
                        graph.Add(
                            opEnd.UOPID,
                            { Node = opStart.UOPID
                              Cost = cost
                              Line = sol.LineIdentification
                              Length = sol.Length }
                            :: graph.[opEnd.UOPID]
                        )
                | None -> ()
        | _ -> fprintfn stderr "not found, %A or %A" sol.OPStartID sol.OPEndID

    ops |> Array.iter addOp

    sols |> Array.iter addSol

    fprintfn stderr "Nodes: %i, Edges: %i" graph.Count (graph |> Seq.sumBy (fun item -> item.Value.Length))

    graph
    |> Seq.map
        (fun kv ->
            let op =
                ops |> Array.find (fun op -> op.UOPID = kv.Key)

            { Node = kv.Key
              Edges = graph.[kv.Key] |> List.toArray })
    |> Seq.toArray

[<EntryPoint>]
let main argv =
    try
        use client =
            new Api.Client(username, password, country)

        if argv.Length = 0 then
            async { return "no arg" }
        else if argv.[0] = "--DatasetImports" then
            async {
                let! response = client.GetDatasetImports()
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SectionsOfLine" && argv.Length > 2 then
            async {
                let! response = client.GetSectionsOfLine(int argv.[1], int argv.[2], true, true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SectionsOfLines" then
            async {
                let! response = client.GetSectionsOfLines()
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--Routes" && argv.Length > 2 then
            async {
                let! response = client.GetRoutes(argv.[1], argv.[2], true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--OperationalPoints" then
            async {
                let! response = client.GetOperationalPoints()

                let filter (opTracks: OPTrack []) cond =
                    opTracks
                    |> Array.map
                        (fun t ->
                            { t with
                                  OPTrackParameters = t.OPTrackParameters |> Array.filter cond })

                let response =
                    response
                    |> Array.map
                        (fun op ->
                            { op with
                                  OPTracks = filter op.OPTracks (fun p -> p.IsApplicable = "Y") })

                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--OperationalPoint" && argv.Length > 2 then
            async {
                let! response = client.GetOperationalPoint(int argv.[1], int argv.[2], true, true)
                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--SOLTrackParameters"
                && argv.Length > 1 then
            let operations =
                JsonSerializer.Deserialize<SectionOfLine []>(File.ReadAllText argv.[1])
                |> Array.map (fun sol -> (sol.ID, sol.VersionID))
                |> Array.splitInto 100
                |> Array.map getSectionsOfLinesWithRetry

            let filters =
                [| "IPP_LineCat"
                   "IPP_MaxSpeed"
                   "IPP_TENClass" |]

            async {
                let! response = operations |> Async.Sequential

                let response =
                    response
                    |> List.concat
                    |> List.toArray
                    |> Array.collect (fun sol -> sol.SOLTracks)
                    |> Array.collect
                        (fun track ->
                            track.SOLTrackParameters
                            |> Array.filter
                                (fun t ->
                                    t.IsApplicable = "Y"
                                    && filters |> Array.contains t.ID))

                return JsonSerializer.Serialize response
            }
        else if argv.[0] = "--Graph.Build" && argv.Length > 1 then
            async {
                let now = System.DateTime.Now

                let ops =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"
                    |> Array.filter (fun op -> op.ValidityDateStart < now)

                let sols =
                    readFile<SectionOfLine []> argv.[1] "SectionsOfLines.json"

                let trackParams =
                    readFile<SOLTrackParameter []> argv.[1] "SOLTrackParameters.json"

                let g = build ops sols trackParams

                return JsonSerializer.Serialize g
            }
        else if argv.[0] = "--OpInfo.Build" && argv.Length > 1 then
            async {
                let now = System.DateTime.Now

                let ops =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"
                    |> Array.filter (fun op -> op.ValidityDateStart < now)
                    |> Array.map
                        (fun op ->
                            { UOPID = op.UOPID
                              Name = op.Name
                              Latitude = op.Latitude
                              Longitude = op.Longitude })

                return JsonSerializer.Serialize ops
            }
        else if argv.[0] = "--LineInfo.Build" && argv.Length > 1 then
            async {
                let now = System.DateTime.Now

                let ops =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"
                    |> Array.filter (fun op -> op.ValidityDateStart < now)

                let sols =
                    readFile<SectionOfLine []> argv.[1] "SectionsOfLines.json"

                let lineInfos = getLines ops sols

                return JsonSerializer.Serialize lineInfos
            }
        else if argv.[0] = "--Graph.Line" && argv.Length > 2 then
            async {
                let g =
                    readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> Array.fold (fun (m: Map<string, OpInfo>) op -> m.Add(op.UOPID, op)) Map.empty

                let graph = Graph.toGraph g

                readFile<LineInfo []> argv.[1] "LineInfos.json"
                |> Array.filter (fun line -> line.Line = argv.[2])
                |> Array.sortBy (fun line -> line.StartKm)
                |> Array.iter
                    (fun lineInfo ->
                        printfn "%s, StartKm: %.1f, EndKm: %.1f" lineInfo.Name lineInfo.StartKm lineInfo.EndKm

                        let nodes =
                            Graph.getPathOfLineFromGraph g graph lineInfo

                        Graph.printPath nodes |> printfn "%s"

                        Graph.printBRouterUrl g map nodes |> printfn "%s")

                return ""
            }
        else if argv.[0] = "--Graph.Route" && argv.Length = 3 then
            async {
                let g =
                    readFile<GraphNode []> argv.[1] "Graph.json"

                fprintfn stderr "Nodes: %i, Edges total: %i" g.Length (g |> Seq.sumBy (fun item -> item.Edges.Length))

                let args = argv.[2].Split ";"

                Graph.getShortestPath g args
                |> Graph.printPath
                |> printfn "%s"

                return ""
            }
        else if argv.[0] = "--Graph.Route.Op" && argv.Length = 3 then
            async {
                let g =
                    readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> Array.fold (fun (m: Map<string, OpInfo>) op -> m.Add(op.UOPID, op)) Map.empty

                let args = argv.[2].Split ";"

                let nodes = Graph.getShortestPath g args

                Graph.printPath nodes |> printfn "%s"

                Graph.printBRouterUrl g map nodes |> printfn "%s"

                return ""
            }
        else
            async { return "unkown arg" }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with
    | e -> fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
