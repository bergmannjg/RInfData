module Comparison

open System

open OSM
open RInf
open RInfGraph

type Location = { Latitude: float; Longitude: float }

/// Matching of RInf.OperationalPoint and OsmOperationalPoint via UOPID and RailwayRef
type OpMatching =
    { op: OperationalPoint
      stop: OsmOperationalPoint
      dist: float
      stopIsRelatedToLine: bool }

/// Matching of RInf.SectionOfLine and OsmSectionOfLine as a connection between to adjacent OpMatchings
type SoLMatching =
    { opStart: OpMatching
      opEnd: OpMatching
      /// compact form of RInf.SectionOfLine from opStart.op to opEnd.op
      rinfPath: GraphNode []
      /// OsmSectionOfLine from opStart.stop to opEnd.stop
      osmSol: OsmSectionOfLine }

type ProccessResult =
    { line: int
      countElements: int
      maxDist: float
      countStops: int
      countOps: int
      countUnrelated: int
      countCompared: int
      countMissing: int
      countSolMatchings: int
      maxSpeedDiff: int }

let private fill (s: string) (len: int) =
    if s.Length < len then
        String(' ', len - s.Length)
    else
        ""

let private toOPID (s: string) = "DE" + (fill s 5) + s

let private equivOPIDs = [| ("DE EBIL", "EBILP") |]

// todo: fix osm data
let fixOsmErrors (stop: OsmOperationalPoint) =
    if stop.Name = "Burg (Dithmarschen)"
       && stop.RailwayRef = "ABRD" then
        { stop with RailwayRef = "ABR" }
    else if stop.Name = "Kummersdorf (bei Storkow)"
            && stop.RailwayRef = "BSTW" then
        { stop with RailwayRef = "BKUM" }
    else if stop.Name = "Nürnberg-Dürrenhof"
            && stop.RailwayRef = "NGLH" then
        { stop with RailwayRef = "NDHF" }
    else
        stop

let private matchOPID (op: OperationalPoint) (stop: OsmOperationalPoint) =
    let fixedStop = fixOsmErrors stop

    equivOPIDs
    |> Array.exists (fun (opId, stopId) -> opId = op.UOPID && stopId = fixedStop.RailwayRef)
    || op.UOPID = (toOPID fixedStop.RailwayRef)

let private getOperationalPointsMatchings
    (stopsOfLine: OsmOperationalPoint [])
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    =
    let stops = Array.append allStops stopsOfLine

    ops
    |> Array.choose (fun op ->
        match stops |> Array.tryFind (matchOPID op) with
        | Some stop ->
            let dist =
                Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

            Some(
                { op = op
                  stop = stop
                  dist = dist
                  stopIsRelatedToLine = stopsOfLine |> Array.exists (matchOPID op) }
            )
        | None -> None)

let private getRInfShortestPathOnLine (line: int) (rinfGraph: GraphNode []) (opStart: string) (opEnd: string) =
    let path = Graph.getShortestPath rinfGraph [| opStart; opEnd |]

    if (path
        |> Array.exists (fun p ->
            p.Edges
            |> Array.exists (fun e -> e.Line <> line.ToString()))) then
        [||]
    else
        path

let private getSoLsMatchings
    (line: int)
    (opMatchings: OpMatching [])
    (rinfGraph: GraphNode [])
    (osmSols: OsmSectionOfLine [])
    =
    osmSols
    |> Array.map (fun osmSoL ->
        let opMatchingStart =
            opMatchings
            |> Array.find (fun m -> m.stop.Element = osmSoL.OsmOpStart.Element)

        let opMatchingEnd =
            opMatchings
            |> Array.find (fun m -> m.stop.Element = osmSoL.OsmOpEnd.Element)

        { opStart = opMatchingStart
          opEnd = opMatchingEnd
          rinfPath = getRInfShortestPathOnLine line rinfGraph opMatchingStart.op.UOPID opMatchingEnd.op.UOPID
          osmSol = osmSoL })

let private rinfGetMaxSpeeds (path: GraphNode []) =
    path
    |> Array.collect (fun node ->
        node.Edges
        |> Array.map (fun edge -> edge.MaxSpeed))
    |> Array.distinct

let private getMaxSpeeds (solMatching: SoLMatching) =
    let osmMaxSpeeds = OSM.Transform.SoL.getMaxSpeeds solMatching.osmSol

    let rinfMaxSpeeds = rinfGetMaxSpeeds solMatching.rinfPath

    let maxSpeedDiff =
        if osmMaxSpeeds.Length > 0
           && rinfMaxSpeeds.Length > 0 then
            Math.Abs(
                (osmMaxSpeeds |> Array.max)
                - (rinfMaxSpeeds |> Array.max)
            )
        else
            -1

    (osmMaxSpeeds, rinfMaxSpeeds, maxSpeedDiff)

let private getRInfOsmMatching
    (line: int)
    (elementsOfLine: Element [])
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    =
    let stopsOfLine =
        Array.concat [ (Transform.Op.nodeStopsToOsmOperationalPoints elementsOfLine)
                       (Transform.Op.wayStopsToOsmOperationalPoints elementsOfLine)
                       (Transform.Op.relationStopsToOsmOperationalPoints elementsOfLine) ]

    let opMatchings = getOperationalPointsMatchings stopsOfLine allStops ops

    let matchedOsmOps = opMatchings |> Array.map (fun m -> m.stop)

    let osmSols = Transform.SoL.getOsmSectionsOfLine line matchedOsmOps elementsOfLine

    let solMatchings = getSoLsMatchings line opMatchings rinfGraph osmSols

    (stopsOfLine, opMatchings, solMatchings)

let private compareLine
    (compact: bool)
    (line: int)
    (elementsOfLine: Element [])
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    =

    let (stopsOfLine, opMatchings, solMatchings) =
        getRInfOsmMatching line elementsOfLine allStops ops rinfGraph

    if not compact then
        printfn "line: %d, stops: %d, ops: %d, matchings: %d" line stopsOfLine.Length ops.Length opMatchings.Length

    if not compact then
        solMatchings
        |> Array.iter (fun solMatching ->
            let (osmMaxSpeeds, rinfMaxSpeeds, maxSpeedDiff) = getMaxSpeeds solMatching

            printfn
                "SoL %s - %s, ways: %d, osmMaxSpeeds = %A, rinfPath: %d, rinfMaxSpeeds = %A, maxSpeedDiff: %d"
                solMatching.osmSol.OsmOpStart.Name
                solMatching.osmSol.OsmOpEnd.Name
                solMatching.osmSol.Ways.Length
                osmMaxSpeeds
                solMatching.rinfPath.Length
                rinfMaxSpeeds
                maxSpeedDiff)

    if not compact then
        let findOp (op: OperationalPoint) =
            opMatchings
            |> Array.exists (fun m -> m.op.UOPID = op.UOPID)

        ops
        |> Array.filter (findOp >> not)
        |> Array.iter (fun op -> printfn "Missing stop %s %s %s" op.Name op.UOPID op.Type)

    let unassigned =
        opMatchings
        |> Array.filter (fun m -> not m.stopIsRelatedToLine)

    if not compact then
        unassigned
        |> Array.iter (fun m -> printfn "Unassigned to line stop %s %s %s" m.op.Name m.op.UOPID m.op.Type)

    if not compact then
        let findStop stop =
            opMatchings
            |> Array.exists (fun m -> m.stop.RailwayRef = stop.RailwayRef)

        stopsOfLine
        |> Array.filter (findStop >> not)
        |> Array.iter (fun stop -> printfn "Missing op %s %s" stop.Name stop.RailwayRef)

    if not compact then
        opMatchings
        |> Array.iter (fun m -> printfn "dist %.3f, op %s %s %s" m.dist m.op.Name m.op.UOPID m.op.Type)

    if not compact then
        opMatchings
        |> Array.filter (fun m -> m.dist > 0.7)
        |> Array.iter (fun m ->
            let url =
                sprintf
                    "https://brouter.de/brouter-web/#map=14/%f/%f/osm-mapnik-german_style&pois=%f,%f,RInf;%f,%f,OSM&profile=rail"
                    m.op.Latitude
                    m.op.Longitude
                    m.op.Longitude
                    m.op.Latitude
                    m.stop.Longitude
                    m.stop.Latitude

            printfn "info |%d|%s|%s|[%.3f](%s)|" line m.op.Name m.op.UOPID m.dist url)

    let maxDist =
        opMatchings
        |> Array.map (fun m -> m.dist)
        |> fun dists ->
            if dists.Length > 0 then
                (dists |> Array.max)
            else
                0.0

    let maxSpeedDiff =
        solMatchings
        |> Array.map (fun solMatching ->
            let (_, _, maxSpeedDiff) = getMaxSpeeds solMatching
            maxSpeedDiff)
        |> fun diffs ->
            if diffs.Length > 0 then
                (diffs |> Array.max)
            else
                -1

    if compact then
        printfn
            "line: %d, elements: %d, maxDist: %.3f, stops: %d, ops: %d, unrelated: %d, compared: %d, missing: %d, sols: %d, maxSpeedDiff: %d"
            line
            elementsOfLine.Length
            maxDist
            stopsOfLine.Length
            ops.Length
            unassigned.Length
            opMatchings.Length
            (ops.Length - opMatchings.Length)
            solMatchings.Length
            maxSpeedDiff

    { line = line
      countElements = elementsOfLine.Length
      maxDist = maxDist
      countStops = stopsOfLine.Length
      countOps = ops.Length
      countUnrelated = unassigned.Length
      countCompared = opMatchings.Length
      countMissing = ops.Length - opMatchings.Length
      countSolMatchings = solMatchings.Length
      maxSpeedDiff = maxSpeedDiff }

let private processAsync osmDataDir rinfDataDir compact useRemote area allStops line =
    async {
        try
            use client = OverpassRequest.getClient ()
            let! osmData = OSMLoader.loadOsmData osmDataDir client useRemote area line

            return
                Some(
                    compareLine
                        compact
                        line
                        osmData.elements
                        allStops
                        (RInfLoader.loadRInfOperationalPoints line rinfDataDir)
                        (RInfLoader.loadRInfGraph rinfDataDir)
                )
        with
        | ex ->
            printfn "Error: %s %s" ex.Message ex.StackTrace
            return None

    }

let private take<'a> count (arr: 'a []) =
    arr |> Array.take (min count arr.Length)

let private printCommonResult (resultOptions: ProccessResult option list) =
    let results = resultOptions |> List.choose id

    if results.Length > 0 then
        let maxDist =
            results
            |> List.map (fun r -> r.maxDist)
            |> List.max

        let stops = results |> List.sumBy (fun r -> r.countStops)

        let ops = results |> List.sumBy (fun r -> r.countOps)

        let unrelated = results |> List.sumBy (fun r -> r.countUnrelated)

        let compared = results |> List.sumBy (fun r -> r.countCompared)

        let missing = results |> List.sumBy (fun r -> r.countMissing)

        let maxSpeedDiff =
            results
            |> List.map (fun r -> r.maxSpeedDiff)
            |> List.max

        let maxSpeedMissing =
            results
            |> List.filter (fun r -> r.maxSpeedDiff = -1 && r.countSolMatchings > 0)

        printfn
            "*** lines: %d, max dist: %.3f, stops: %d, ops: %d, unrelated: %d, compared: %d, missing: %d, maxSpeedMissing: %d, maxSpeedDiff: %d"
            results.Length
            maxDist
            stops
            ops
            unrelated
            compared
            missing
            maxSpeedMissing.Length
            maxSpeedDiff

        results
        |> List.filter (fun r -> r.maxDist > 0.7) // max. platform length
        |> List.iter (fun r ->
            printfn
                "line: %d, max dist: %.3f, stops: %d, ops: %d, unassigned: %d, compared: %d"
                r.line
                r.maxDist
                r.countStops
                r.countOps
                r.countUnrelated
                r.countCompared)

        let withElements =
            (results
             |> List.filter (fun r -> r.countElements > 0))
                .Length

        let notCompared =
            (results
             |> List.filter (fun r -> r.countCompared = 0))
                .Length

        let withSols =
            (results
             |> List.filter (fun r -> r.countSolMatchings > 0))
                .Length

        let withMaxSpeedDiff =
            (results
             |> List.filter (fun r -> r.maxSpeedDiff >= 0))
                .Length

        let withMoreThanOneRInfOp =
            (results |> List.filter (fun r -> r.countOps > 1))
                .Length

        let withMoreThanOneOsmfOp =
            (results
             |> List.filter (fun r -> r.countCompared > 1))
                .Length

        printfn "***RInf railway lines: %d" results.Length
        printfn "***OSM relations with tag [route=tracks]: %d" withElements
        printfn "***OSM railway lines with with no station tag [railway:ref]: %d" notCompared
        printfn "***RInf railway lines with more than 1 operational point: %d" withMoreThanOneRInfOp
        printfn "***OSM railway lines with more than 1 osm operational point: %d" withMoreThanOneOsmfOp
        printfn "***OSM railway lines with sections of line: %d" withSols
        printfn "***OSM railway lines with maxspeed data: %d" withMaxSpeedDiff

let compareLineAsync osmDataDir rinfDataDir (line: int) (useRemote: bool) (area: string option) =
    processAsync osmDataDir rinfDataDir false useRemote area (OSMLoader.loadAllStops osmDataDir) line

let compareLinesAsync osmDataDir rinfDataDir (count: int) =
    let rec loop (work: 'a -> Async<'b>) (l: 'a list) (results: 'b list) =
        async {
            match l with
            | h :: t ->
                let! res = work h
                return! loop work t (res :: results)
            | [] -> return results |> List.rev
        }

    async {
        let lines =
            RInfLoader.loadRInfLines rinfDataDir
            |> take count
            |> Array.toList

        let allStops = OSMLoader.loadAllStops osmDataDir
        let! results = loop (processAsync osmDataDir rinfDataDir true false None allStops) lines []

        printCommonResult results
    }
