/// match RInf OperationalPoints and OsmOperationalPoints
module Matching

open System

open OSM
open RInf
open RInfGraph

type Location = { Latitude: float; Longitude: float }

type RailwayRefMatching =
    | ByNothing
    | ByOpId
    | ByOpIdParent
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

/// reason why there is no matching of rinf op and osm stop
type ReasonOfNoMatching =
    /// historic station is not mapped in osm
    | HistoricStation
    /// distance of matched rinf op and osm stop is gt than maxDistanceOfMatchedOps
    | DistanceToStop
    /// distance of matched rinf op and osm ways of line is gt than maxDistanceOfOpToWaysOfLine
    | DistanceToWaysOfLine
    | OsmNotYetMapped
    | Unexpected

type MissingStop =
    { line: int
      opid: string
      reason: ReasonOfNoMatching }

/// distance of max. platform length
let maxDistanceOfMatchedOps = 0.7

let maxDistanceOfOpToWaysOfLine = 1.0

let maxDistanceOfMatchingByName = 1.0

let missingStops: MissingStop [] =
    [| { line = 1962
         opid = "DE HNPL"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 3443
         opid = "DE RKWU"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 5100
         opid = "DE NHAL"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 6078
         opid = "DE BHPN"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 6194
         opid = "DE DWID"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 6220
         opid = "DE BMZD"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 6345
         opid = "DELEGOB"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 6385
         opid = "DE DWIO"
         reason = ReasonOfNoMatching.HistoricStation }
       { line = 6406
         opid = "DE  LMR"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
       { line = 6441
         opid = "DE WSGR"
         reason = ReasonOfNoMatching.OsmNotYetMapped }
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

    if op.UOPID = fixedStop.RailwayRef then
        Some(stop, RailwayRefMatching.ByOpId, stopIsRelatedToLine)
    else if op.UOPID = fixedStop.RailwayRefParent then
        Some(stop, RailwayRefMatching.ByOpIdParent, stopIsRelatedToLine)
    else if (fixedStop.RailwayRefsUicRef.Length = 1
             && op.UOPID = fixedStop.RailwayRefsUicRef.[0]) then
        Some(stop, RailwayRefMatching.ByUicRef, stopIsRelatedToLine)
    else
        None

let private matchName (op: OperationalPoint) (stopIsRelatedToLine: bool) (s: OsmOperationalPoint) =
    if
        s.RailwayRefContent = RailwayRefContent.NotFound
        && equalNames op.Name s.Name
        && distance op s < maxDistanceOfMatchingByName
        && not
            (
                missingStops
                |> Array.exists (fun ms -> ms.opid = op.UOPID)
            )
    then
        Some(s, stopIsRelatedToLine)
    else
        None

let private distOfOpToStop (op: OperationalPoint) (stop: OsmOperationalPoint) =
    Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

let private makeMatching
    (relationOfLine: Relation)
    (op: OperationalPoint)
    (stop: OsmOperationalPoint)
    (railwayRefMatching: RailwayRefMatching)
    (stopIsRelatedToLine: bool)
    (elementsOfLine: Element [])
    =
    let (distOfOpToWaysOfLine, node) =
        Transform.SoL.getMinDistanceToWays op.Latitude op.Longitude relationOfLine elementsOfLine

    { op = op
      stop = stop
      distOfOpToStop = distOfOpToStop op stop
      distOfOpToWaysOfLine = distOfOpToWaysOfLine
      nodeOnWaysOfLine = node
      railwayRefMatching = railwayRefMatching
      stopIsRelatedToLine = stopIsRelatedToLine }

let private tryPick chooser (seq: seq<bool * OsmOperationalPoint []>) =
    seq
    |> Seq.tryPick (fun (b, arr) -> arr |> Array.tryPick (chooser b))

let private getOperationalPointsMatchings
    (relationOfLine: Relation)
    (stopsOfLine: OsmOperationalPoint [])
    (allStops: OsmOperationalPoint [])
    (mapAllStops: Map<string, Transform.Op.Key * OsmOperationalPoint>)
    (opsOfLine: OperationalPoint [])
    (elementsOfLine: Element [])
    =
    let stops =
        [ (true, stopsOfLine)
          (false, allStops) ]

    let map = Transform.Op.toRailwayRefMap stopsOfLine mapAllStops

    let printMachtingInfo (op: OperationalPoint) (stop: OsmOperationalPoint) =
        printfn
            "Matching.ByName, op %s %s, stop %s (https://www.openstreetmap.org/node/%d)"
            op.UOPID
            op.Name
            stop.Name
            (Data.idOf stop.Element)

        match stop.Element with
        | Node v -> printfn "add_railwayref(%d, '%s');" (v.id) (Transform.Op.toRailwayRef op.UOPID)
        | _ -> ()

    opsOfLine
    |> Array.choose (fun op ->
        match map.TryFind op.UOPID with
        | Some (keyType, s) ->
            let matching =
                match keyType with
                | Transform.Op.Key.FromRailwayRef -> RailwayRefMatching.ByOpId
                | Transform.Op.Key.FromUicRef -> RailwayRefMatching.ByUicRef

            Some(makeMatching relationOfLine op s matching true elementsOfLine)
        | _ ->
            match stops |> tryPick (matchOPID op) with
            | Some (stop, m, stopIsRelatedToLine) ->
                Some(makeMatching relationOfLine op stop m stopIsRelatedToLine elementsOfLine)
            | None ->
                match stops |> tryPick (matchName op) with
                | Some (stop, stopIsRelatedToLine) ->
                    printMachtingInfo op stop

                    Some(
                        makeMatching relationOfLine op stop RailwayRefMatching.ByName stopIsRelatedToLine elementsOfLine
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

let getRInfOsmMatching
    (line: int)
    (relationOfLine: Relation)
    (elementsOfLine: Element [])
    (allStops: OsmOperationalPoint [])
    (mapAllStops: Map<string, Transform.Op.Key * OsmOperationalPoint>)
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (uicRefMappings: DB.UicRefMapping [])
    =
    let stopsOfLine =
        Array.concat [ (Transform.Op.nodeStopsToOsmOperationalPoints uicRefMappings elementsOfLine)
                       (Transform.Op.wayStopsToOsmOperationalPoints uicRefMappings elementsOfLine)
                       (Transform.Op.relationStopsToOsmOperationalPoints uicRefMappings elementsOfLine) ]

    let opMatchings =
        getOperationalPointsMatchings relationOfLine stopsOfLine allStops mapAllStops ops elementsOfLine

    let matchedOsmOps = opMatchings |> Array.map (fun m -> m.stop)

    let osmSols = Transform.SoL.getOsmSectionsOfLine line matchedOsmOps elementsOfLine

    let solMatchings = getSoLsMatchings line opMatchings rinfGraph osmSols

    (stopsOfLine, opMatchings, solMatchings)
