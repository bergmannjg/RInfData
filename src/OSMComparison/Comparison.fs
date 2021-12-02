module Comparison

open System

open OSM
open RInf
open RInfGraph

type Location = { Latitude: float; Longitude: float }

type RailwayRefMatching =
    | ByNothing
    | ByOpId
    /// opid with same UicRef
    | ByUicRef
    | ByName

/// Matching of RInf.OperationalPoint and OsmOperationalPoint via UOPID and RailwayRef
type OpMatching =
    { op: OperationalPoint
      stop: OsmOperationalPoint
      distOfOpToStop: float
      distOfOpToWaysOfLine: float
      nodeOnWaysOfLine: OSM.Node option
      railwayRefMatching: RailwayRefMatching
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
      opMatchingOfMaxDist: OpMatching option
      countStops: int
      countOps: int
      countUnrelated: int
      countCompared: int
      countMatchedWithOpId: int
      countMatchedWithUicRef: int
      countMatchedWithName: int
      countMissing: int
      countSolMatchings: int
      maxSpeedDiff: int
      railwayRefsMatchedWithUicRef: (string * int * int64) []
      missingStops: string [] }

/// reason why there is no matching of rinf op and osm stop
type ReasonOfNoMatching =
    /// historic station is not mapped in osm
    | HistoricStation
    /// distance of matched rinf op and osm stop is gt than maxDistanceOfOpToWaysOfLine
    | DistanceToWaysOfLine
    | Unexpected

type MissingStop =
    { line: int
      opid: string
      reason: ReasonOfNoMatching }

/// distance of max. platform length
let private maxDistanceOfMatchedOps = 0.7

let private maxDistanceOfOpToWaysOfLine = 1.0

let private maxDistanceOfMatchingByName = 1.0

let private missingStops: MissingStop [] =
    [| { line = 1962
         opid = "DE HNPL"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 6194
         opid = "DE DWID"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 6385
         opid = "DE DWIO"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 6441
         opid = "DE WLOW"
         reason = ReasonOfNoMatching.HistoricStation } |]

// todo: fix osm data
let private fixOsmErrors (stop: OsmOperationalPoint) =
    if Data.idOf stop.Element = 19654034
       && stop.RailwayRef = "" then // todo
        { stop with
            RailwayRef = "NRPF"
            RailwayRefContent = RailwayRefContent.FromOpId }
    else
        stop

let private distance (op: OperationalPoint) (stop: OsmOperationalPoint) =
    Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

let private normalize (s: string) = s.Replace(" ", "").Replace("-", "")

let private equalNames (s1: string) (s2: string) = normalize s1 = normalize s2

let private matchOPID (op: OperationalPoint) (stopIsRelatedToLine: bool) (stop: OsmOperationalPoint) =
    let fixedStop = fixOsmErrors stop

    let inSplitList (s1: string) (splits: string []) =
        splits |> Array.exists (fun s -> s1 = s)

    if op.UOPID = fixedStop.RailwayRef
       || (op.UOPID = fixedStop.RailwayRefParent
           && distance op stop < 1.0) then
        Some(stop, RailwayRefMatching.ByOpId, stopIsRelatedToLine)
    else if (fixedStop.RailwayRefsUicRef.Length > 0
             && inSplitList op.UOPID fixedStop.RailwayRefsUicRef) then
        Some(stop, RailwayRefMatching.ByUicRef, stopIsRelatedToLine)
    else
        None

let private urlOpToLonLat (op: OperationalPoint) (lon: float) (lat: float) =
    sprintf
        "https://brouter.de/brouter-web/#map=14/%f/%f/osm-mapnik-german_style&pois=%f,%f,RInf;%f,%f,OSM&profile=rail"
        op.Latitude
        op.Longitude
        op.Longitude
        op.Latitude
        lon
        lat

let private makeMatching
    (relationOfLine: Relation)
    (op: OperationalPoint)
    (stop: OsmOperationalPoint)
    (railwayRefMatching: RailwayRefMatching)
    (stopIsRelatedToLine: bool)
    (elementsOfLine: Element [])
    =
    let distOfOpToStop =
        Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

    let (distOfOpToWaysOfLine, node) =
        Transform.SoL.getMinDistanceToWays op.Latitude op.Longitude elementsOfLine

    { op = op
      stop = stop
      distOfOpToStop = distOfOpToStop
      distOfOpToWaysOfLine = distOfOpToWaysOfLine
      nodeOnWaysOfLine = node
      railwayRefMatching = railwayRefMatching
      stopIsRelatedToLine = stopIsRelatedToLine }

let private tryPick chooser (seq: seq<bool * OsmOperationalPoint []>) =
    seq
    |> Seq.tryPick (fun (b, arr) -> arr |> Array.tryPick (chooser b))

let private matchName (op: OperationalPoint) (stopIsRelatedToLine: bool) (s: OsmOperationalPoint) =
    if s.RailwayRefContent = RailwayRefContent.NotFound
       && equalNames op.Name s.Name
       && distance op s < maxDistanceOfMatchingByName then
        Some(s, stopIsRelatedToLine)
    else
        None

let private getOperationalPointsMatchings
    (relationOfLine: Relation)
    (stopsOfLine: OsmOperationalPoint [])
    (allStops: OsmOperationalPoint [])
    (opsOfLine: OperationalPoint [])
    (elementsOfLine: Element [])
    =
    let stops =
        [ (true, stopsOfLine)
          (false, allStops) ]

    let printMachtingInfo (op: OperationalPoint) (stop: OsmOperationalPoint) =
        printfn
            "Matching.ByName, op %s %s, stop %s (https://www.openstreetmap.org/node/%d)"
            op.UOPID
            op.Name
            stop.Name
            (Data.idOf stop.Element)

        printfn "add_railwayref(%d, '%s');" (Data.idOf stop.Element) (Transform.Op.toRailwayRef op.UOPID)

    opsOfLine
    |> Array.choose (fun op ->
        match stops |> tryPick (matchOPID op) with
        | Some (stop, m, stopIsRelatedToLine) ->
            Some(makeMatching relationOfLine op stop m stopIsRelatedToLine elementsOfLine)
        | None ->
            match stops |> tryPick (matchName op) with
            | Some (stop, stopIsRelatedToLine) ->
                printMachtingInfo op stop
                Some(makeMatching relationOfLine op stop RailwayRefMatching.ByName stopIsRelatedToLine elementsOfLine)
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
    (relationOfLine: Relation)
    (elementsOfLine: Element [])
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (uicRefMappings: DB.UicRefMapping [])
    =
    let stopsOfLine =
        Array.concat [ (Transform.Op.nodeStopsToOsmOperationalPoints uicRefMappings elementsOfLine)
                       (Transform.Op.wayStopsToOsmOperationalPoints uicRefMappings elementsOfLine)
                       (Transform.Op.relationStopsToOsmOperationalPoints uicRefMappings elementsOfLine) ]

    let opMatchings =
        getOperationalPointsMatchings relationOfLine stopsOfLine allStops ops elementsOfLine

    let matchedOsmOps = opMatchings |> Array.map (fun m -> m.stop)

    let osmSols = Transform.SoL.getOsmSectionsOfLine line matchedOsmOps elementsOfLine

    let solMatchings = getSoLsMatchings line opMatchings rinfGraph osmSols

    (stopsOfLine, opMatchings, solMatchings)

let private showWaysOfMatching = false

let private osmUrl (op: OperationalPoint) =
    sprintf "https://www.openstreetmap.org/#map=18/%f/%f" op.Latitude op.Longitude


let private collectResult
    (compact: bool)
    (line: int)
    (relationOfLine: Relation)
    (elementsOfLine: Element [])
    (opsOfLine: OperationalPoint [])
    (stopsOfLine: OsmOperationalPoint [])
    (maybeOpMatchings: OpMatching [])
    (solMatchings: SoLMatching [])
    =

    let opMatchings =
        maybeOpMatchings
        |> Array.filter (fun m ->
            if m.distOfOpToWaysOfLine < maxDistanceOfOpToWaysOfLine then
                true
            else
                let url =
                    match m.nodeOnWaysOfLine with
                    | Some node -> urlOpToLonLat m.op node.lon node.lat
                    | None -> ""

                printfn "distance to line %d, stop %s %s, dist: %.3f" line m.op.Name m.op.UOPID m.distOfOpToWaysOfLine

                printfn
                    "distOfOpToWaysOfLine|%d|%s|%s|[%.3f](%s)|[%d](https://www.openstreetmap.org/relation/%d)|"
                    line
                    m.op.Name
                    m.op.UOPID
                    m.distOfOpToWaysOfLine
                    url
                    relationOfLine.id
                    relationOfLine.id

                false)

    maybeOpMatchings
    |> Array.iter (fun m ->
        if m.distOfOpToStop > maxDistanceOfMatchedOps then
            printfn
                "distance from op to stop, line %d, stop %s %s, dist: %.3f"
                line
                m.op.Name
                m.op.UOPID
                m.distOfOpToStop

            printfn
                "distOfOpToStop|%d|%s|%s|[%.3f](%s)|"
                line
                m.op.Name
                m.op.UOPID
                m.distOfOpToStop
                (urlOpToLonLat m.op m.stop.Longitude m.stop.Latitude))

    let matchedWithOpId =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByOpId)

    let matchedWithUicRef =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByUicRef)

    let matchedWithName =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByName)

    if not compact then
        printfn
            "line: %d, stops: %d, ops: %d, matchings: %d, matchedWithUicRef: %d, matchedWithName: %d, relation: %d"
            line
            stopsOfLine.Length
            opsOfLine.Length
            opMatchings.Length
            matchedWithUicRef.Length
            matchedWithName.Length
            relationOfLine.id

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
                maxSpeedDiff

            if showWaysOfMatching then
                solMatching.osmSol.Ways
                |> Array.iter (fun w -> printfn "  %d, maxspeed: %A" w.id (OSM.Data.getTagValue Tag.Maxspeed (Way w))))

    let findOp (op: OperationalPoint) =
        opMatchings
        |> Array.exists (fun m -> m.op.UOPID = op.UOPID)

    let unassigned =
        opMatchings
        |> Array.filter (fun m -> not m.stopIsRelatedToLine)

    if not compact then
        unassigned
        |> Array.iter (fun m ->
            printfn
                "Unassigned to line stop %s %s %s, minDist: %.3f"
                m.op.Name
                m.op.UOPID
                m.op.Type
                m.distOfOpToWaysOfLine)

    (*
    if not compact then
        let findStop stop =
            opMatchings
            |> Array.exists (fun m -> m.stop.RailwayRef = stop.RailwayRef)

        stopsOfLine
        |> Array.filter (findStop >> not)
        |> Array.iter (fun stop -> printfn "Missing op %s %s" stop.Name stop.RailwayRef)
    *)

    let (maxDist, opMatchingOfMaxDist) =
        opMatchings
        |> Array.map (fun m -> (m.distOfOpToStop, Some m))
        |> fun dists ->
            if dists.Length > 0 then
                (dists |> Array.maxBy (fun (d, _) -> d))
            else
                (0.0, None)

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

    let railwayRefMatchingOfMaxDist =
        match opMatchingOfMaxDist with
        | Some m -> m.railwayRefMatching.ToString()
        | None -> ""

    if compact then
        printfn
            "line: %d, elements: %d, maxDist: (%.3f, %s), stops: %d, ops: %d, compared: %d, missing: %d, matched(OpId: %d, hUicRef: %d, Name: %d), sols: %d, maxSpeedDiff: %d"
            line
            elementsOfLine.Length
            maxDist
            railwayRefMatchingOfMaxDist
            stopsOfLine.Length
            opsOfLine.Length
            opMatchings.Length
            (opsOfLine.Length - opMatchings.Length)
            matchedWithOpId.Length
            matchedWithUicRef.Length
            matchedWithName.Length
            solMatchings.Length
            maxSpeedDiff

    let missingStopsReason line opid =
        match missingStops
              |> Array.tryFind (fun ms -> ms.line = line && ms.opid = opid)
            with
        | Some ms -> ms.reason
        | None ->
            match maybeOpMatchings
                  |> Array.tryFind (fun m -> m.op.UOPID = opid)
                with
            | Some ms -> ReasonOfNoMatching.DistanceToWaysOfLine
            | None -> ReasonOfNoMatching.Unexpected

    opsOfLine
    |> Array.filter (findOp >> not)
    |> Array.iter (fun op ->
        printfn
            "%s stop(%s) %s %s %s, %s"
            (if compact then
                 "  missing"
             else
                 "Missing")
            ((missingStopsReason line op.UOPID).ToString())
            op.Name
            op.UOPID
            op.Type
            (osmUrl op))

    if not compact then
        opMatchings
        |> Array.iter (fun m ->
            printfn
                "dist %.3f, op %s %s %s, stop %d, match %s"
                m.distOfOpToStop
                m.op.Name
                m.op.UOPID
                m.op.Type
                (Data.idOf m.stop.Element)
                (if m.railwayRefMatching = RailwayRefMatching.ByUicRef then
                     "byUicRef "
                     + (m.stop.RailwayRefsUicRef |> String.concat ",")
                 else if m.railwayRefMatching = RailwayRefMatching.ByName then
                     "byName " + m.stop.Name
                 else
                     "byOpId " + m.stop.RailwayRef))

    let railwayRefsMatchedWithUicRef =
        matchedWithUicRef
        |> Array.map (fun m ->
            (m.stop.RailwayRefsUicRef |> String.concat ",", m.stop.UicRef, OSM.Data.idOf m.stop.Element))
        |> Array.distinct

    let missingStops =
        opsOfLine
        |> Array.filter (findOp >> not)
        |> Array.map (fun op -> op.UOPID)

    { line = line
      countElements = elementsOfLine.Length
      maxDist = maxDist
      opMatchingOfMaxDist = opMatchingOfMaxDist
      countStops = stopsOfLine.Length
      countOps = opsOfLine.Length
      countUnrelated = unassigned.Length
      countCompared = opMatchings.Length
      countMatchedWithOpId = matchedWithOpId.Length
      countMatchedWithUicRef = matchedWithUicRef.Length
      countMatchedWithName = matchedWithName.Length
      countMissing = opsOfLine.Length - opMatchings.Length
      countSolMatchings = solMatchings.Length
      maxSpeedDiff = maxSpeedDiff
      railwayRefsMatchedWithUicRef = railwayRefsMatchedWithUicRef
      missingStops = missingStops }

let private compareLine
    (compact: bool)
    (line: int)
    (relationOfLine: Relation)
    (elementsOfLine: Element [])
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (uicRefMappings: DB.UicRefMapping [])
    =

    let (stopsOfLine, opMatchings, solMatchings) =
        getRInfOsmMatching line relationOfLine elementsOfLine allStops ops rinfGraph uicRefMappings

    collectResult compact line relationOfLine elementsOfLine ops stopsOfLine opMatchings solMatchings

let private filterUicRefMapping (op: OperationalPoint) (m: DB.UicRefMapping) =
    m.DS100.Split [| ',' |]
    |> Array.exists (fun s -> Transform.Op.toOPID s = op.UOPID)

// UicRefMappings may contain all active stations
let private filterUicRefMappings (uicRefMappings: DB.UicRefMapping []) (ops: OperationalPoint []) =
    ops
    |> Array.filter (fun op ->
        uicRefMappings
        |> Array.exists (filterUicRefMapping op))

let private suisseOps =
    [| "DE  RNK"
       "DE  RBE"
       "DE RBEF"
       "DE RNHN"
       "DE RHRB" |]

let private printWaysOfLineNotInRelation (line: int) (r: Relation) (elementsOfLine: Element []) =
    elementsOfLine
    |> Array.choose (Data.asWay)
    |> Array.filter (fun w ->
        OSM.Data.hasTagWithValue Tag.Railway "rail" (Way w)
        && OSM.Data.existsTag Tag.Usage (Way w)
        && OSM.Data.hasTagWithValue Tag.Ref (line.ToString()) (Way w)
        && not (r.members |> Array.exists (fun m -> m.ref = w.id)))
    // |> Array.map (fun w ->
    //     printfn "way %d not in relation %d of line %d" w.id r.id line
    //     w)
    |> fun ways ->
        if ways.Length > 0 then
            printfn "  ways not in relation %d of line %d: %d " r.id line ways.Length

let private processAsync osmDataDir rinfDataDir compact useRemote area allStops uicRefMappings line =
    async {
        try
            use client = OverpassRequest.getClient ()
            let! osmData = OSMLoader.loadOsmData osmDataDir client useRemote area line

            let ops =
                (RInfLoader.loadRInfOperationalPoints line rinfDataDir)
                |> Array.filter (fun op -> not (suisseOps |> Array.contains op.UOPID))

            let filteredOps = ops |> filterUicRefMappings uicRefMappings

            if not compact then
                if ops.Length <> filteredOps.Length then
                    ops
                    |> Array.filter (fun op ->
                        not (
                            filteredOps
                            |> Array.exists (fun fOp -> fOp.UOPID = op.UOPID)
                        ))
                    |> Array.iter (fun op -> printfn "maybe inactive op: %s %s %s" op.UOPID op.Name (osmUrl op))

            match OSM.Transform.SoL.getRelationOfRailwayLine line osmData.elements with
            | Some r ->
                printWaysOfLineNotInRelation line r osmData.elements

                return
                    Some(
                        compareLine
                            compact
                            line
                            r
                            osmData.elements
                            allStops
                            filteredOps
                            (RInfLoader.loadRInfGraph rinfDataDir)
                            uicRefMappings
                    )
            | None -> return None
        with
        | ex ->
            printfn "Error: %s %s" ex.Message ex.StackTrace
            return None

    }

let private take<'a> count (arr: 'a []) =
    arr |> Array.take (min count arr.Length)

let showDetails = false

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

        let matchedWithOpId =
            results
            |> List.sumBy (fun r -> r.countMatchedWithOpId)

        let matchedWithUicRef =
            results
            |> List.sumBy (fun r -> r.countMatchedWithUicRef)

        let matchedWithName =
            results
            |> List.sumBy (fun r -> r.countMatchedWithName)

        let missingStops =
            results
            |> List.toArray
            |> Array.collect (fun r -> r.missingStops)
            |> Array.distinct
            |> Array.sort

        let maxSpeedDiff =
            results
            |> List.map (fun r -> r.maxSpeedDiff)
            |> List.max

        let maxSpeedMissing =
            results
            |> List.filter (fun r -> r.maxSpeedDiff = -1 && r.countSolMatchings > 0)

        printfn
            "*** lines: %d, max dist: %.3f, stops: %d, ops: %d, unrelated: %d, compared: %d, missing: %d, matched(OpId: %d, UicRef: %d, Name: %d), maxSpeedMissing: %d, maxSpeedDiff: %d"
            results.Length
            maxDist
            stops
            ops
            unrelated
            compared
            missingStops.Length
            matchedWithOpId
            matchedWithUicRef
            matchedWithName
            maxSpeedMissing.Length
            maxSpeedDiff

        results
        |> List.filter (fun r -> r.maxDist > maxDistanceOfMatchedOps)
        |> List.iter (fun r ->
            let railwayRefMatchingOfMaxDist =
                match r.opMatchingOfMaxDist with
                | Some m -> m.railwayRefMatching.ToString()
                | None -> ""

            printfn
                "line: %d, max dist: (%.3f, %s), stops: %d, ops: %d, unassigned: %d, compared: %d"
                r.line
                r.maxDist
                railwayRefMatchingOfMaxDist
                r.countStops
                r.countOps
                r.countUnrelated
                r.countCompared)

        if showDetails then
            results
            |> List.toArray
            |> Array.collect (fun r -> r.railwayRefsMatchedWithUicRef)
            |> Array.distinct
            |> Array.sort
            |> Array.iter (fun (railwayRef, uicref, id) ->
                printfn "railwayRef from uicref %s %d, node %d" railwayRef uicref id)

            missingStops
            |> Array.iter (fun uopid -> printfn "missing stop %s" uopid)

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

        printfn "***OSM relations with tag [route=tracks]: %d" withElements
        printfn "***OSM railway lines with with no station tag [railway:ref]: %d" notCompared
        printfn "***RInf railway lines with more than 1 operational point: %d" withMoreThanOneRInfOp
        printfn "***OSM railway lines with more than 1 osm operational point: %d" withMoreThanOneOsmfOp
        printfn "***OSM railway lines with sections of line: %d" withSols
        printfn "***OSM railway lines with maxspeed data: %d" withMaxSpeedDiff

let compareLineAsync
    osmDataDir
    rinfDataDir
    dbDataDir
    (line: int)
    (useRemote: bool)
    (useAllStops: bool)
    (area: string option)
    =
    let uicRefMappings = DB.DBLoader.loadMappings dbDataDir

    processAsync
        osmDataDir
        rinfDataDir
        false
        useRemote
        area
        (if useAllStops then
             OSMLoader.loadAllStops osmDataDir uicRefMappings
         else
             [||])
        uicRefMappings
        line

let compareLinesAsync osmDataDir rinfDataDir dbDataDir (count: int) =
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

        printfn "***RInf passenger railway lines: %d" lines.Length

        let uicRefMappings = DB.DBLoader.loadMappings dbDataDir
        let allStops = OSMLoader.loadAllStops osmDataDir uicRefMappings
        let! results = loop (processAsync osmDataDir rinfDataDir true false None allStops uicRefMappings) lines []

        printCommonResult results
    }

let compareLinesListAsync osmDataDir rinfDataDir dbDataDir (linesArg: string []) =
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
            linesArg
            |> Array.map (fun l -> int l)
            |> Array.toList

        printfn "***railway lines: %d" lines.Length

        let uicRefMappings = DB.DBLoader.loadMappings dbDataDir
        let allStops = OSMLoader.loadAllStops osmDataDir uicRefMappings
        let! results = loop (processAsync osmDataDir rinfDataDir true false None allStops uicRefMappings) lines []

        printCommonResult results
    }
