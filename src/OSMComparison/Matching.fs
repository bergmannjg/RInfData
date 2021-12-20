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
    | ByName
    | ByOther

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

type IMatching =
    abstract Match: OperationalPoint -> OpMatching option

/// distance of max. platform length
let maxDistanceOfMatchedOps = 0.7

let maxDistanceOfOpToWaysOfLine = 1.0

let maxDistanceOfMatchingByName = 1.0

let private distOfOpToStop (op: OperationalPoint) (stop: OsmOperationalPoint) =
    Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

let private printMachtingInfo
    (railwayRefMatching: RailwayRefMatching)
    (op: OperationalPoint)
    (stop: OsmOperationalPoint)
    =
    match railwayRefMatching with
    | RailwayRefMatching.ByName
    | RailwayRefMatching.ByOther ->
        printfn
            "Matching.%s, op %s %s, stop %s (https://www.openstreetmap.org/%s/%d)"
            (railwayRefMatching.ToString())
            op.UOPID
            op.Name
            stop.Name
            (Data.kindOf stop.Element)
            (Data.idOf stop.Element)

        match stop.Element with
        | Node v -> printfn "add_railwayref(%d, 'node', '%s');" (v.id) (Transform.Op.toRailwayRef op.UOPID)
        | Way w -> printfn "add_railwayref(%d, 'way', '%s');" (w.id) (Transform.Op.toRailwayRef op.UOPID)
        | Relation r -> printfn "add_railwayref(%d, 'relation', '%s');" (r.id) (Transform.Op.toRailwayRef op.UOPID)

    | _ -> ()

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

    printMachtingInfo railwayRefMatching op stop

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

let private getOperationalPointsMatchings (opsOfLine: OperationalPoint []) (matchings: IMatching seq) =
    opsOfLine
    |> Array.map (fun op -> matchings |> Seq.tryPick (fun m -> m.Match op))
    |> Array.choose id

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
    (elementsOfLine: Element [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (matchings: IMatching seq)
    =
    let opMatchings = getOperationalPointsMatchings ops matchings

    let matchedOsmOps = opMatchings |> Array.map (fun m -> m.stop)

    let osmSols = Transform.SoL.getOsmSectionsOfLine line matchedOsmOps elementsOfLine

    let solMatchings = getSoLsMatchings line opMatchings rinfGraph osmSols

    (opMatchings, solMatchings)

type MatchingByOpId
    (
        relationOfLine: Relation,
        stopsOfLine: OsmOperationalPoint [],
        elementsOfLine: Element [],
        allStops: OsmOperationalPoint [],
        mapAllStops: Map<string, OsmOperationalPoint>
    ) =

    let map = Transform.Op.toRailwayRefMap stopsOfLine mapAllStops

    let matchOPID (op: OperationalPoint) (stopIsRelatedToLine: bool) (stop: OsmOperationalPoint) =
        let fixedStop = stop

        if op.UOPID = fixedStop.RailwayRef then
            Some(stop, RailwayRefMatching.ByOpId, stopIsRelatedToLine)
        else if op.UOPID = fixedStop.RailwayRefParent then
            Some(stop, RailwayRefMatching.ByOpIdParent, stopIsRelatedToLine)
        else
            None

    let matchByOpId (op: OperationalPoint) =
        let stops =
            [ (true, stopsOfLine)
              (false, allStops) ]

        match map.TryFind op.UOPID with
        | Some s ->
            let matching = RailwayRefMatching.ByOpId

            Some(makeMatching relationOfLine op s matching true elementsOfLine)
        | _ ->
            match stops |> tryPick (matchOPID op) with
            | Some (stop, m, stopIsRelatedToLine) ->
                Some(makeMatching relationOfLine op stop m stopIsRelatedToLine elementsOfLine)
            | None -> None

    interface IMatching with
        member this.Match op = matchByOpId op

type MatchingByName
    (
        relationOfLine: Relation,
        stopsOfLine: OsmOperationalPoint [],
        elementsOfLine: Element [],
        allStops: OsmOperationalPoint []
    ) =

    let normalize (s: string) = s.Replace(" ", "").Replace("-", "")

    let equalNames (s1: string) (s2: string) = normalize s1 = normalize s2

    let distance (op: OperationalPoint) (stop: OsmOperationalPoint) =
        Transform.SoL.``calculate distance`` (op.Latitude, op.Longitude) (stop.Latitude, stop.Longitude)

    let matchName (op: OperationalPoint) (stopIsRelatedToLine: bool) (s: OsmOperationalPoint) =
        if
            s.RailwayRefContent = RailwayRefContent.NotFound
            && equalNames op.Name s.Name
            && distance op s < maxDistanceOfMatchingByName
            && not
                (
                    MissingStops.missingStops
                    |> Array.exists (fun ms -> ms.opid = op.UOPID)
                )
        then
            Some(s, stopIsRelatedToLine)
        else
            None

    let matchByName (op: OperationalPoint) =
        let stops =
            [ (true, stopsOfLine)
              (false, allStops) ]

        match stops |> tryPick (matchName op) with
        | Some (stop, stopIsRelatedToLine) ->
            Some(makeMatching relationOfLine op stop RailwayRefMatching.ByName stopIsRelatedToLine elementsOfLine)
        | None -> None

    interface IMatching with
        member this.Match op = matchByName op

type MatchingByUicRef
    (
        relationOfLine: Relation,
        stopsOfLine: OsmOperationalPoint [],
        elementsOfLine: Element [],
        allStops: OsmOperationalPoint [],
        uicRefMappings: DB.UicRefMapping []
    ) =

    let getRailwayRefFromIFOPT (ifopt: string option) =
        match ifopt with
        | Some ifopt when ifopt.Length > 0 ->

            let m = System.Text.RegularExpressions.Regex.Match(ifopt, "^(de:[0-9]+:[0-9]+).*")

            if m.Success then
                let ifopt = m.Groups.[1].Value

                match uicRefMappings
                      |> Array.tryFind (fun s -> s.IFOPT = ifopt)
                    with
                | Some s -> s.DS100
                | None -> ""
            else
                ""
        | _ -> ""

    let uicRefOf (e: Element) =
        match OSM.Data.getTagValue OSM.Tag.UicRef e with
        | Some v ->
            match Int32.TryParse v with
            | (true, v) -> v
            | _ -> 0
        | None -> 0

    let getRailwayRefFromUicRef (uicRef: int) =
        if uicRef > 0 then
            match uicRefMappings
                  |> Array.tryFind (fun s -> s.EVA_NR = uicRef)
                with
            | Some s -> s.DS100
            | None -> ""
        else
            ""

    let matchUicRef (op: OperationalPoint) (stopIsRelatedToLine: bool) (s: OsmOperationalPoint) =
        let opids =
            (getRailwayRefFromUicRef (uicRefOf s.Element))
                .Split [| ',' |]

        if opids.Length = 1
           && (OSM.Transform.Op.toOPID opids.[0]) = op.UOPID then
            Some(s, stopIsRelatedToLine)
        else
            let opid = getRailwayRefFromIFOPT (OSM.Data.getTagValue OSM.Tag.RefIFOPT s.Element)

            if OSM.Transform.Op.toOPID opid = op.UOPID then
                Some(s, stopIsRelatedToLine)
            else
                None

    let matchByUicRef (op: OperationalPoint) =
        let stops =
            [ (true, stopsOfLine)
              (false, allStops) ]

        match stops |> tryPick (matchUicRef op) with
        | Some (stop, stopIsRelatedToLine) ->
            Some(makeMatching relationOfLine op stop RailwayRefMatching.ByOther stopIsRelatedToLine elementsOfLine)
        | None -> None

    interface IMatching with
        member this.Match op = matchByUicRef op
