/// transform OSM data to OsmOperationalPoint and OsmSectionOfLine
namespace OSM

type RailwayRefContent =
    | NotFound
    | FromOpId
    | FromRelation

type OsmOperationalPoint =
    { /// corresponding OSM element
      Element: Element
      Name: string
      Type: string
      Latitude: float
      Longitude: float
      Railway: string
      RailwayRef: string
      RailwayRefParent: string
      RailwayRefContent: RailwayRefContent }

type OsmWay =
    { ID: int64
      Tags: Map<string, string> option }

/// OsmSectionOfLine is the connection between two adjacent OsmOperationalPoints consisting of ways of the same line
type OsmSectionOfLine =
    { OsmOpStart: OsmOperationalPoint
      OsmOpEnd: OsmOperationalPoint
      Ways: Way [] }

[<RequireQualifiedAccess>]
module Tag =

    [<Literal>]
    let Railway = "railway"

    [<Literal>]
    let PlannedRailway = "planned:railway"

    [<Literal>]
    let DisusedRailway = "disused:railway"

    [<Literal>]
    let HistoricRailway = "historic:railway"

    [<Literal>]
    let Name = "name"

    [<Literal>]
    let NameDE = "name:de"

    [<Literal>]
    let Type = "type"

    [<Literal>]
    let Operator = "operator"

    [<Literal>]
    let Publictransport = "public_transport"

    [<Literal>]
    let Route = "route"

    [<Literal>]
    let Ref = "ref"

    [<Literal>]
    let Maxspeed = "maxspeed"

    [<Literal>]
    let RailwayRef = "railway:ref"

    [<Literal>]
    let RailwayRefDBAG = "railway:ref:DBAG"

    [<Literal>]
    let RailwayRefParent = "railway:ref:parent"

    [<Literal>]
    let RefIFOPT = "ref:IFOPT"

    [<Literal>]
    let UicRef = "uic_ref"

    [<Literal>]
    let Usage = "usage"

[<RequireQualifiedAccess>]
module Transform =

    module Op =

        let toRailwayRefMap (ops: OsmOperationalPoint []) (initialmap: Map<string, OsmOperationalPoint>) =
            let add (map: Map<string, OsmOperationalPoint>) (op: OsmOperationalPoint) (key: string) =
                match map.TryFind key with
                | Some v ->
                    // prefer tag Railway = "station"
                    if v.Railway <> "station" && op.Railway = "station" then
                        map.Add(key, op)
                    else
                        map
                | None -> map.Add(key, op)

            ops
            |> Array.fold
                (fun map op ->
                    if op.RailwayRef <> "" then
                        add map op op.RailwayRef
                    else
                        map)
                initialmap

        let private relevantRailwayTag (t: string option) =
            match t with
            | Some t ->
                t = "station"
                || t = "stop"
                || t = "halt"
                || t = "platform"
                || t = "service_station"
                || t = "yard"
                || t = "site"
                || t = "facility"
                || t = "junction"
                || t = "crossover"
                || t = "cross_section"
                || t = "spur_junction"
            | None -> false

        let private relevantRailwayTags (tags: string []) (e: Element) =
            tags
            |> Array.exists (fun tag -> relevantRailwayTag (OSM.Data.getTagValue tag e))

        let private isRailWayElement (t: System.Type) (e: Element) =
            match e with
            | Node n when t = typeof<Node> ->
                relevantRailwayTags
                    [| Tag.Railway
                       Tag.PlannedRailway
                       Tag.DisusedRailway
                       Tag.HistoricRailway |]
                    e
            | Way _ when t = typeof<Way> -> relevantRailwayTag (OSM.Data.getTagValue Tag.Railway e)
            | Relation r when t = typeof<Relation> ->
                (OSM.Data.existsTag Tag.RailwayRef e
                 || OSM.Data.existsTag Tag.UicRef e)
                // && OSM.Data.hasTagWithValue Tag.Publictransport "stop_area" e
                && (r.members
                    |> Array.exists (fun m -> m.``type`` = "node" && m.role = "stop"))
            | _ -> false

        let asRailWayElement<'a> (choose: Element -> 'a option) (e: Element) =
            if isRailWayElement typeof<'a> e then
                choose e
            else
                None

        let private fill (s: string) (len: int) =
            if s.Length < len then
                System.String(' ', len - s.Length)
            else
                ""

        /// from railwayRef to opid
        let toOPID (s: string) =
            if s.Length > 0 && s.Length <= 5 then
                "DE" + (fill s 5) + s
            else if s.Length > 0 && s.Contains ";" then // todo: change osm data
                let s1 = s.Substring(0, (s.IndexOf ";"))
                "DE" + (fill s1 5) + s1
            else
                ""

        /// from opid to railwayRef
        let toRailwayRef (opid: string) =
            if opid.Length = 7 then
                opid.Substring(2).Trim()
            else
                opid

        let private makeOsmOperationalPoint (e: Element) (lat: float) (lon: float) (elements: Element []) =
            let tagValue key =
                match OSM.Data.getTagValue key e with
                | Some v -> v
                | None -> ""

            let otherTagValue key value =
                if value = "" then
                    match OSM.Data.getTagValue key e with
                    | Some v -> v
                    | None -> ""
                else
                    value

            let toInt (s: string) =
                match System.Int32.TryParse s with
                | true, int -> int
                | _ -> 0

            let getRailwayRefFromRelation (railwayRef: string) (e: Element) =
                if railwayRef = "" then
                    match OSM.Data.getRelationOf e elements with
                    | Some r ->
                        if OSM.Data.hasTagWithValue Tag.Publictransport "stop_area" (Relation r) then
                            match OSM.Data.getTagValue Tag.RailwayRef (Relation r) with
                            | Some ref -> ref
                            | None -> ""
                        else
                            ""
                    | None -> ""
                else
                    railwayRef

            let isSBB = tagValue Tag.Operator = "SBB"

            let railwayRefStep0 = tagValue Tag.RailwayRef
            let railwayRefStep1 = otherTagValue Tag.RailwayRefDBAG railwayRefStep0
            let railwayRefStep2 = getRailwayRefFromRelation railwayRefStep1 e

            let nameStep0 = tagValue Tag.NameDE
            let nameStep1 = otherTagValue Tag.Name nameStep0

            let railway = otherTagValue Tag.Publictransport (tagValue Tag.Railway)

            if isSBB then
                None
            else
                Some
                    { Element = e
                      Name = nameStep1
                      Type = tagValue Tag.Railway
                      Latitude = lat
                      Longitude = lon
                      Railway = railway
                      RailwayRef = toOPID railwayRefStep2
                      RailwayRefParent = toOPID (tagValue Tag.RailwayRefParent)
                      RailwayRefContent =
                        if railwayRefStep0 <> "" then
                            RailwayRefContent.FromOpId
                        else if railwayRefStep0 = "" && railwayRefStep2 <> "" then
                            RailwayRefContent.FromRelation
                        else
                            RailwayRefContent.NotFound }

        let wayStopsToOsmOperationalPoints (elements: Element []) =
            let map = Data.toMap elements

            elements
            |> Array.choose (asRailWayElement Data.asWay)
            |> Array.choose (fun w ->
                match OSM.Data.getAnyNodeOfWayMapped w map with
                | Some n -> makeOsmOperationalPoint (Way w) n.lat n.lon elements
                | _ -> None)

        let relationStopsToOsmOperationalPoints (elements: Element []) =
            elements
            |> Array.choose (asRailWayElement Data.asRelation)
            |> Array.choose (fun r ->
                match OSM.Data.getAnyNodeOfRelation r "stop" elements with
                | Some n -> makeOsmOperationalPoint (Relation r) n.lat n.lon elements
                | _ -> None)

        let nodeStopsToOsmOperationalPoints (elements: Element []) =
            elements
            |> Array.choose (asRailWayElement Data.asNode)
            |> Array.choose (fun n -> makeOsmOperationalPoint (Node n) n.lat n.lon elements)

    module SoL =
        open System

        // see http://www.fssnip.net/7P8/title/Calculate-distance-between-two-GPS-latitudelongitude-points
        let ``calculate distance`` (p1Latitude, p1Longitude) (p2Latitude, p2Longitude) =
            let r = 6371.0 // km

            let dLat = (p2Latitude - p1Latitude) * Math.PI / 180.0

            let dLon = (p2Longitude - p1Longitude) * Math.PI / 180.0

            let lat1 = p1Latitude * Math.PI / 180.0
            let lat2 = p2Latitude * Math.PI / 180.0

            let a =
                Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
                + Math.Sin(dLon / 2.0)
                  * Math.Sin(dLon / 2.0)
                  * Math.Cos(lat1)
                  * Math.Cos(lat2)

            let c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a))

            r * c

        let private getMinDistanceToWayNodes (lat: float) (lon: float) (way: Way) (elements: Element []) =
            way.nodes
            |> Array.choose (fun n -> Data.nodeOf n elements)
            |> Array.map (fun nodeOnWay ->
                (``calculate distance`` (lat, lon) (nodeOnWay.lat, nodeOnWay.lon), Some nodeOnWay))
            |> fun arr ->
                if arr.Length > 0 then
                    arr |> Array.minBy (fun (d, _) -> d)
                else
                    (System.Double.MaxValue, None)

        let getMinDistanceToWays (lat: float) (lon: float) (relationOfLine: Relation) (elements: Element []) =
            elements
            |> Array.choose (Data.asWay)
            |> Array.map (fun w -> getMinDistanceToWayNodes lat lon w elements)
            |> fun arr ->
                if arr.Length > 0 then
                    arr |> Array.minBy (fun (d, _) -> d)
                else
                    (-1.0, None)

        let private filterNearestWays (dist: float) (ways: (Way * float) []) =
            if ways.Length > 1 then
                let (way1, d1) = ways.[0]
                let (way2, d2) = ways.[1]

                if d1 < dist && d2 < dist then
                    [| way1; way2 |]
                else
                    [||]
            else
                [||]

        let private getWaysAround (node: Node) (relation: Relation) (dist: float) (elements: Element []) =
            OSM.Data.getMembers relation "way" ""
            |> Array.choose (fun m -> OSM.Data.wayOf m.ref elements)
            |> Array.map (
                (fun w ->
                    let (d, _) = getMinDistanceToWayNodes node.lat node.lon w elements
                    (w, d))
            )
            |> Array.sortBy (fun (_, d) -> d)
            |> filterNearestWays dist

        /// <summary>
        /// get ways on relation of line related to node with id stationId,
        /// corresponding overpass query:
        /// <code>
        /// node(stationId);rel(bn)[route=tracks];node(stationId);way(around:1000)[ref=line];
        /// or node(stationId);rel(bn)[type=public_transport];node(r);way(bn)[railway=rail][ref=line];
        /// or node(stationId);way(bn)[railway=rail][ref=line];
        /// or rel(stationId);node(r);way(bn)[railway=rail][ref=line];
        /// </code>
        /// </summary>
        let private getWaysRelatedToStation (station: Element) (line: int) (elements: Element []) : Way [] =
            match station with
            | Node node ->
                match elements |> OSM.Data.getRelationOf station with
                | Some relation ->
                    if OSM.Data.hasTagWithValue Tag.Route "tracks" (Relation relation) then
                        getWaysAround node relation 1.0 elements
                    else if OSM.Data.hasTagWithValue Tag.Type "public_transport" (Relation relation) then
                        OSM.Data.getWaysOfMembers
                            relation
                            "node"
                            "stop"
                            (fun w -> Data.hasTagWithValue Tag.Railway "rail" (Way w))
                            elements
                        |> Array.distinctBy (fun w -> w.id)
                    else
                        [||]
                | None ->
                    elements
                    |> OSM.Data.getWaysOfElement station (fun w ->
                        OSM.Data.hasTagWithValue Tag.Railway "rail" (Way w)
                        && OSM.Data.hasTagWithValue Tag.Ref (line.ToString()) (Way w))
            | Way way -> [| way |]
            | Relation relation ->
                OSM.Data.getWaysOfMembers
                    relation
                    "node"
                    "stop"
                    (fun w -> Data.hasTagWithValue Tag.Railway "rail" (Way w))
                    elements
                |> Array.distinct

        let private getPairs (arr: 'a []) =
            if arr.Length < 2 then
                [||]
            else
                arr
                |> Array.take (arr.Length - 1)
                |> Array.mapi (fun i a -> (arr.[i], arr.[i + 1]))

        type private WayCache =
            { way: Way
              tags: Map<string, string> option
              nodes: int64 []
              firstNode: int64
              lastNode: int64 }

        let private getWayCache (members: Member []) (elements: Element []) =
            members
            |> Array.filter (fun m -> m.``type`` = "way")
            |> Array.choose (fun m ->
                match Data.tryGetElement m.ref elements with
                | Some (Way way) ->
                    match Data.tryGetElement way.nodes.[0] elements,
                          Data.tryGetElement way.nodes.[way.nodes.Length - 1] elements
                        with
                    | Some firstNode, Some lastNode ->
                        Some
                            { way = way
                              tags = way.tags
                              nodes = way.nodes
                              firstNode = OSM.Data.idOf firstNode
                              lastNode = OSM.Data.idOf lastNode }
                    | _ -> None
                | _ -> None)

        let private isConnectedWay (way1: WayCache) (way2: WayCache) =
            way1.way <> way2.way
            && (way1.lastNode = way2.firstNode
                || way1.lastNode = way2.lastNode
                || way1.firstNode = way2.firstNode
                || way1.firstNode = way2.lastNode)

        let private toGraph (waysCache: WayCache []) =

            waysCache
            |> Array.fold
                (fun (graph: Map<int64, Dijkstra.Vertex<int64>>) w ->
                    let edges =
                        waysCache
                        |> Array.choose (fun x ->
                            if isConnectedWay x w then
                                Some(1, x.way.id)
                            else
                                None)
                        |> Array.toList

                    graph.Add(w.way.id, (Dijkstra.makeVertex w.way.id edges)))
                Map.empty

        let private getRouteOfWays
            (fromWay: WayCache)
            (toWay: WayCache)
            (waysCache: WayCache [])
            (graph: Map<int64, Dijkstra.Vertex<int64>>)
            =

            match Dijkstra.shortestPath graph fromWay.way.id toWay.way.id with
            | Some path ->
                path.Nodes
                |> List.map (fun p -> waysCache |> Array.find (fun w -> w.way.id = p))
                |> List.toArray
            | None -> [||]

        let private getAnyRouteOfWays
            (fromWays: WayCache [])
            (toWays: WayCache [])
            (waysCache: WayCache [])
            (graph: Map<int64, Dijkstra.Vertex<int64>>)
            =
            Array.allPairs fromWays toWays
            |> Array.tryPick (fun (fromWay, toWay) ->
                let route = getRouteOfWays fromWay toWay waysCache graph

                if route.Length > 0 then
                    Some route
                else
                    None)
            |> Option.defaultValue [||]

        let private getAnyWaysFromTo
            (fromIds: Way [])
            (toIds: Way [])
            (waysCache: WayCache [])
            (graph: Map<int64, Dijkstra.Vertex<int64>>)
            =
            let getWays (ids: Way []) =
                waysCache
                |> Array.filter (fun x -> ids |> Array.contains x.way)

            let fromWays = getWays fromIds

            let toWays = getWays toIds

            if fromWays.Length > 0 && toWays.Length > 0 then
                getAnyRouteOfWays fromWays toWays waysCache graph
            else
                [||]

        let private getOsmSectionOfLine
            waysCache
            (graph: Map<int64, Dijkstra.Vertex<int64>>)
            ((op1, ids1), (op2, ids2))
            =
            { OsmOpStart = op1
              OsmOpEnd = op2
              Ways =
                getAnyWaysFromTo ids1 ids2 waysCache graph
                |> Array.map (fun w -> w.way) }

        let getRelationOfRailwayLine (line: int) (elements: Element []) =
            elements
            |> Array.tryFind (fun e ->
                match e with
                | Relation r ->
                    OSM.Data.hasTagWithValue Tag.Route "tracks" (Relation r)
                    && OSM.Data.hasTagWithValue Tag.Ref (line.ToString()) (Relation r)
                | _ -> false)
            |> fun e ->
                match e with
                | Some (Relation r) -> Some r
                | _ -> None

        let getOsmSectionsOfLine (line: int) (ops: OsmOperationalPoint []) (elements: Element []) =
            match getRelationOfRailwayLine line elements with
            | Some relation ->
                let waysCache = getWayCache relation.members elements
                let graph = toGraph waysCache

                ops
                |> Array.map (fun op -> (op, getWaysRelatedToStation op.Element line elements))
                |> Array.filter (fun (_, s) -> s.Length > 0)
                |> getPairs
                |> Array.map (getOsmSectionOfLine waysCache graph)
            | _ -> [||]

        let getMaxSpeeds (sol: OsmSectionOfLine) =
            sol.Ways
            |> Array.choose (fun w ->
                match OSM.Data.getTagValue Tag.Maxspeed (Way w) with
                | Some speed ->
                    match System.Int32.TryParse speed with
                    | true, int -> Some int
                    | _ -> None
                | None -> None)
            |> Array.distinct
