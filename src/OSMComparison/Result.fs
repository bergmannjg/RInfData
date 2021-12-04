module Result

open System

open OSM
open RInf
open RInfGraph
open Matching

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
      countMatchedWithOpIdParent: int
      countMatchedWithUicRef: int
      countMatchedWithName: int
      countMissing: int
      countSolMatchings: int
      maxSpeedDiff: int
      railwayRefsMatchedWithUicRef: (string * int * int64) []
      missingStops: string [] }

let private showWaysOfMatching = false

let osmUrl (op: OperationalPoint) =
    sprintf "https://www.openstreetmap.org/#map=18/%f/%f" op.Latitude op.Longitude

let private urlOpToLonLat (op: OperationalPoint) (lon: float) (lat: float) =
    sprintf
        "https://brouter.de/brouter-web/#map=14/%f/%f/osm-mapnik-german_style&pois=%f,%f,RInf;%f,%f,OSM&profile=rail"
        op.Latitude
        op.Longitude
        op.Longitude
        op.Latitude
        lon
        lat

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

let collectResult
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
            if m.distOfOpToWaysOfLine < Matching.maxDistanceOfOpToWaysOfLine then
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

    let matchedWithOpIdParent =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByOpIdParent)

    let matchedWithUicRef =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByUicRef)

    let matchedWithName =
        opMatchings
        |> Array.filter (fun m -> m.railwayRefMatching = RailwayRefMatching.ByName)

    if not compact then
        printfn
            "line: %d, stops: %d, ops: %d, matchings: %d, matched(OpId: %d, OpIdParent: %d, hUicRef: %d, Name: %d), relation: %d"
            line
            stopsOfLine.Length
            opsOfLine.Length
            opMatchings.Length
            matchedWithOpId.Length
            matchedWithOpIdParent.Length
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
            "line: %d, elements: %d, maxDist: (%.3f, %s), stops: %d, ops: %d, compared: %d, missing: %d, matched(OpId: %d, OpIdParent: %d, hUicRef: %d, Name: %d), sols: %d, maxSpeedDiff: %d"
            line
            elementsOfLine.Length
            maxDist
            railwayRefMatchingOfMaxDist
            stopsOfLine.Length
            opsOfLine.Length
            opMatchings.Length
            (opsOfLine.Length - opMatchings.Length)
            matchedWithOpId.Length
            matchedWithOpIdParent.Length
            matchedWithUicRef.Length
            matchedWithName.Length
            solMatchings.Length
            maxSpeedDiff

    let missingStopsReason line opid =
        match Matching.missingStops
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
      countMatchedWithOpIdParent = matchedWithOpIdParent.Length
      countMatchedWithUicRef = matchedWithUicRef.Length
      countMatchedWithName = matchedWithName.Length
      countMissing = opsOfLine.Length - opMatchings.Length
      countSolMatchings = solMatchings.Length
      maxSpeedDiff = maxSpeedDiff
      railwayRefsMatchedWithUicRef = railwayRefsMatchedWithUicRef
      missingStops = missingStops }

let showDetails = false

let printCommonResult (resultOptions: ProccessResult option list) =
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

        let matchedWithOpIdParent =
            results
            |> List.sumBy (fun r -> r.countMatchedWithOpIdParent)

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
            "*** lines: %d, max dist: %.3f, stops: %d, ops: %d, unrelated: %d, compared: %d, missing: %d, matched(OpId: %d, OpIdParent: %d, UicRef: %d, Name: %d), maxSpeedMissing: %d, maxSpeedDiff: %d"
            results.Length
            maxDist
            stops
            ops
            unrelated
            compared
            missingStops.Length
            matchedWithOpId
            matchedWithOpIdParent
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

