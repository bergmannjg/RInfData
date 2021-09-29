open System
open System.IO
open System.Text.Json
open System.Collections.Generic

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

let printHelp () =
    """
USAGE: RInfLoader.exe
               [--help] [--DatasetImports] [--SectionsOfLines] [--OperationalPoints]
               [--SOLTrackParameters <sol file>] [--OpInfo.Build <dataDir>]
               [--LineInfo.Build <dataDir>] [--Graph.Build <dataDir>]
               [--Graph.Route <dataDir> <ops>] [--Graph.Line <dataDir> <line>]

OPTIONS:

    --DatasetImports      load DatasetImports (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SectionsOfLines     load SectionsOfLines (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --OperationalPoints   load OperationalPoints (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SOLTrackParameters <SectionsOfLines file>
                          load SOLTrackParameters for all SectionsOfLines
                          (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --OpInfo.Build <dataDir>
                          build OpInfos from file OperationalPoints.json in <dataDir>.
    --LineInfo.Build <dataDir>
                          build LineInfos from files SectionsOfLines.json and OperationalPoints.json in <dataDir>.
    --Graph.Build <dataDir>
                          build graph of OperationalPoints and SectionsOfLines from files SectionsOfLines.json,
                          OperationalPoints.json and SOLTrackParameters.json in <dataDir>.
    --Graph.Route <dataDir> <opIds>
                          get path of route from <opIds>, ex. "DE   HH;DE   BL"
                          (assumes Graph.json and OpInfos.json in <dataDir>).
    --Graph.Line <dataDir> <line>
                          get path of line
                          (assumes Graph.json, LineInfos.json and OpInfos.json in <dataDir>).
    --help                display this list of options.
"""

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

let kilometerOfLine (op: OperationalPoint) (line: string) =
    op.RailwayLocations
    |> Array.tryFind (fun loc -> loc.NationalIdentNum = line)
    |> Option.map (fun loc -> loc.Kilometer)
    |> Option.defaultValue 0.0

let findOp ops opId =
    ops |> Array.find (fun op -> op.ID = opId)

let getLineInfo (name: string) ops (solsOfLine: SectionOfLine list) line =

    let firstOp =
        findOp ops (solsOfLine |> List.head).OPStartID

    let lastOp =
        findOp ops (solsOfLine |> List.last).OPEndID

    let uOPIDs =
        solsOfLine
        |> List.rev
        |> List.fold (fun (s: string list) sol -> ((findOp ops sol.OPStartID).UOPID :: s)) [ lastOp.UOPID ]
        |> List.toArray

    { Line = line
      Name = name
      Length =
          solsOfLine
          |> List.sumBy (fun sol -> sol.Length)
          |> sprintf "%.1f"
          |> Double.Parse
      StartKm = kilometerOfLine firstOp line
      EndKm = kilometerOfLine lastOp line
      UOPIDs = uOPIDs }

let buildLineInfo (ops: OperationalPoint []) (sols: SectionOfLine []) (line: string) : LineInfo [] =
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

    let nextNodesLists =
        firstNodes
        |> Array.map (fun firstNode -> getNextNodes solsOfLine firstNode [ firstNode ])

    if nextNodesLists.Length > 0 then
        let firstOp =
            findOp ops nextNodesLists.[0].Head.OPStartID

        let lastElem =
            nextNodesLists.[nextNodesLists.Length - 1]
            |> List.toArray

        let lastOp =
            findOp ops lastElem.[lastElem.Length - 1].OPEndID


        nextNodesLists
        |> Array.map (fun nextNodes -> getLineInfo (firstOp.Name + " - " + lastOp.Name) ops nextNodes line)
    else
        [||]

let buildLineInfos (ops: OperationalPoint []) (sols: SectionOfLine []) : LineInfo [] =
    sols
    |> Array.map (fun sol -> sol.LineIdentification)
    |> Array.distinct
    |> Array.collect (buildLineInfo ops sols)
    |> Array.sortBy (fun line -> int line.Line)

let buildGraph (ops: OperationalPoint []) (sols: SectionOfLine []) (trackParams: SOLTrackParameter []) =
    let mutable graph = Map.empty

    let addOp (op: OperationalPoint) =
        if not (graph.ContainsKey op.UOPID) then
            graph <- graph.Add(op.UOPID, List.empty)

    let findOp opId =
        ops |> Array.tryFind (fun op -> op.ID = opId)

    let getMaxSpeed (sol: SectionOfLine) (trackParamsOfSol: SOLTrackParameter []) =
        trackParamsOfSol
        |> Array.tryFind
            (fun param ->
                param.ID = "IPP_MaxSpeed"
                && not (isNull param.Value))
        |> (fun param ->
            match param with
            | Some param -> int param.Value
            | None ->
                fprintfn stderr "sol %s, maxspeed not found" sol.solName
                100)

    let scale (maxSpeed: int) =
        if maxSpeed >= 250 then 1.4
        else if maxSpeed >= 200 then 1.2
        else if maxSpeed >= 160 then 1.1
        else 1.0

    let travelTime (length: float) (maxSpeed: int) =
        length / (float (maxSpeed) * (scale maxSpeed))

    let getCost (sol: SectionOfLine) (maxSpeed: int) =
        let cost =
            int (10000.0 * (travelTime sol.Length maxSpeed))

        if (cost <= 0) then 1 else cost

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
                let maxSpeed = getMaxSpeed sol trackParamsOfSol
                let cost = getCost sol maxSpeed

                graph <-
                    graph.Add(
                        opStart.UOPID,
                        { Node = opEnd.UOPID
                          Cost = cost
                          Line = sol.LineIdentification
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

    graph
    |> Seq.map
        (fun kv ->
            let op =
                ops |> Array.find (fun op -> op.UOPID = kv.Key)

            { Node = kv.Key
              Edges = graph.[kv.Key] |> List.toArray })
    |> Seq.toArray

let toMap (opInfos: OpInfo []) =
    opInfos
    |> Array.fold
        (fun (m: Dictionary<string, OpInfo>) op ->
            if not (m.ContainsKey op.UOPID) then
                m.Add(op.UOPID, op)

            m)
        (Dictionary<string, OpInfo>())

[<EntryPoint>]
let main argv =
    try
        use client =
            new Api.Client(username, password, country)

        if argv.Length = 0 then
            async { return printHelp () }
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
                let ops =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"

                let sols =
                    readFile<SectionOfLine []> argv.[1] "SectionsOfLines.json"

                let trackParams =
                    readFile<SOLTrackParameter []> argv.[1] "SOLTrackParameters.json"

                let g = buildGraph ops sols trackParams

                return JsonSerializer.Serialize g
            }
        else if argv.[0] = "--OpInfo.Build" && argv.Length > 1 then
            async {
                let now = System.DateTime.Now

                let ops =
                    readFile<OperationalPoint []> argv.[1] "OperationalPoints.json"
                    |> Array.filter (fun op -> now < op.ValidityDateEnd)
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

                let sols =
                    readFile<SectionOfLine []> argv.[1] "SectionsOfLines.json"
                    |> Array.filter (fun sol -> now < sol.ValidityDateEnd)

                let lineInfos = buildLineInfos ops sols

                return JsonSerializer.Serialize lineInfos
            }
        else if argv.[0] = "--Graph.Line" && argv.Length > 2 then
            async {
                let g =
                    readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> toMap

                let graph = Graph.toGraph g

                readFile<LineInfo []> argv.[1] "LineInfos.json"
                |> Array.filter (fun line -> line.Line = argv.[2])
                |> Array.sortBy (fun line -> line.StartKm)
                |> Array.iter
                    (fun lineInfo ->
                        printfn "%s, StartKm: %.1f, EndKm: %.1f" lineInfo.Name lineInfo.StartKm lineInfo.EndKm

                        let path =
                            Graph.getPathOfLineFromGraph g graph lineInfo

                        Graph.printPath path

                        Graph.getBRouterUrl (Graph.getLocationsOfPath g map path)
                        |> printfn "%s")

                return ""
            }
        else if argv.[0] = "--Graph.Route" && argv.Length = 3 then
            async {
                let g =
                    readFile<GraphNode []> argv.[1] "Graph.json"

                let map =
                    readFile<OpInfo []> argv.[1] "OpInfos.json"
                    |> toMap

                let args = argv.[2].Split ";"

                let path = Graph.getShortestPath g args

                printfn "Path:"
                Graph.printPath path
                printfn "compact Path:"
                Graph.printPath (Graph.getCompactPath path)

                Graph.getBRouterUrl (Graph.getLocationsOfPath g map path)
                |> printfn "%s"

                return ""
            }
        else
            async { return printHelp () }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with
    | e -> fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
