namespace RInfGraph

open FSharpx.Collections
open RInf.Types

type Location =
    { Latitude: float<degree>
      Longitude: float<degree> }

type PoILocation =
    { Latitude: float<degree>
      Longitude: float<degree>
      Content: string }

type OperationalPointType =
    { station: int
      smallstation: int
      passengerterminal: int
      freightterminal: int
      depotorworkshop: int
      traintechnicalservices: int
      passengerstop: int
      junction: int
      borderpoint: int
      shuntingyard: int
      technicalchange: int
      switch: int
      privatesiding: int
      domesticborderpoint: int }

type OpInfo =
    { UOPID: string
      Name: string
      RinfType: int
      Latitude: float<degree>
      Longitude: float<degree> }

type LineInfo =
    { Line: string
      Country: string
      Name: string
      Length: float<km>
      StartKm: float<km>
      EndKm: float<km>
      UOPIDs: string[]
      Tunnels: string[]
      Wikipedia: string Option }

type TunnelInfo =
    { Tunnel: string
      Length: float<km>
      StartLong: float<degree>
      StartLat: float<degree>
      StartKm: float<km>
      EndLong: float<degree>
      EndLat: float<degree>
      EndKm: float<km>
      Line: string
      Country: string }

type GraphEdge =
    { Node: string
      Cost: int
      Line: string
      Country: string
      MaxSpeed: int<km / h>
      Electrified: bool
      StartKm: float<km>
      EndKm: float<km>
      Length: float<km> }

type GraphNode = { Node: string; Edges: GraphEdge[] }

type PathElement =
    { From: string
      FromOPID: string
      To: string
      ToOPID: string
      Line: string
      LineText: string
      Country: string
      StartKm: float<km>
      EndKm: float<km>
      MaxSpeed: int<km / h> }

module Graph =

    let mutable verbose = false

    // Type of Operational Point, see https://www.era.europa.eu/system/files/2023-02/RINF%20Application%20guide%20V1.6.1.pdf
    let operationalPointType: OperationalPointType =
        { station = 10
          smallstation = 20
          passengerterminal = 30
          freightterminal = 40
          depotorworkshop = 50
          traintechnicalservices = 60
          passengerstop = 70
          junction = 80
          borderpoint = 90
          shuntingyard = 100
          technicalchange = 110
          switch = 120
          privatesiding = 130
          domesticborderpoint = 140 }

    let private toVertex (node: GraphNode) =
        (node.Node, node.Edges |> Array.map (fun e -> (e.Cost, e.Node)) |> Array.toList)
        |> fun (n, l) -> Dijkstra.makeVertex n l

    let addEdge
        (opidFrom: string)
        (opidTo: string)
        (line: string)
        (imcode: string)
        (cost: int)
        (maxSpeed: int<km / h>)
        (startKm: float<km>)
        (endKm: float<km>)
        (length: float<km>)
        (graph: Map<string, GraphEdge list>)
        =
        let createEdge
            (opEndId: string)
            (line: string)
            (cost: int)
            (maxSpeed: int<km / h>)
            (startKm: float<km>)
            (endKm: float<km>)
            (length: float<km>)
            : GraphEdge =
            { Node = opEndId
              Cost = cost
              Line = line
              Country = imcode
              MaxSpeed = maxSpeed
              Electrified = true
              StartKm = startKm
              EndKm = endKm
              Length = length }

        let edges1 =
            match graph |> Map.tryFind opidFrom with
            | Some edges -> edges
            | None -> []

        let edges2 =
            match graph |> Map.tryFind opidTo with
            | Some edges -> edges
            | None -> []

        graph
            .Add(opidFrom, (createEdge opidTo line cost maxSpeed startKm endKm length) :: edges1)
            .Add(opidTo, (createEdge opidFrom line cost maxSpeed endKm startKm length) :: edges2)

    let toGraph (g: GraphNode[]) =
        g
        |> Array.fold
#if FABLE_COMPILER
            (fun (graph: Map<string, Dijkstra.Vertex>) n ->
#else
            (fun (graph: Map<string, Dijkstra.Vertex<string>>) n ->
#endif
                let v = toVertex n
                graph.Add(v.Id, v))
            (Map.empty)

    let private toGraphNode (g: GraphNode[]) nodes =
        match nodes with
        | [| n1; n2 |] ->
            let node1 = g |> Seq.find (fun n -> n.Node = n1)

            let edges =
                node1.Edges
                |> Array.filter (fun edge -> edge.Node = n2)
                |> Array.sortBy (fun e -> e.Cost)

            let (cost, line, imcode, maxSpeed, electrified, startKm, endKm, length) =
                edges
                |> Seq.tryHead
                |> Option.map (fun edge ->
                    (edge.Cost,
                     edge.Line,
                     edge.Country,
                     edge.MaxSpeed,
                     edge.Electrified,
                     edge.StartKm,
                     edge.EndKm,
                     edge.Length))
                |> Option.defaultValue (0, "", "", 0<_>, false, 0.0<_>, 0.0<_>, 0.0<_>)

            { Node = n1
              Edges =
                [| { Node = n2
                     Cost = cost
                     Line = line
                     Country = imcode
                     MaxSpeed = maxSpeed
                     Electrified = electrified
                     StartKm = startKm
                     EndKm = endKm
                     Length = length } |] }
            |> Some
        | _ -> None

#if FABLE_COMPILER
    let private toGraphNodes (g: GraphNode[]) (path: Dijkstra.Path option) =
#else
    let private toGraphNodes (g: GraphNode[]) (path: Dijkstra.Path<string> option) =
#endif
        match path with
        | Some path ->
            path.Nodes
            |> Seq.windowed 2
            |> Seq.map (toGraphNode g)
            |> Seq.choose id
            |> Seq.toArray
        | None -> Array.empty

#if FABLE_COMPILER
    let getShortestPathFromGraph (g: GraphNode[]) (graph: Map<string, Dijkstra.Vertex>) (ids: string[]) =
#else
    let getShortestPathFromGraph (g: GraphNode[]) (graph: Map<string, Dijkstra.Vertex<string>>) (ids: string[]) =
#endif
        let nodes =
            ids
            |> Array.map (fun id -> g |> Seq.tryFind (fun node -> node.Node = id))
            |> Array.choose id

        if nodes.Length = ids.Length && ids.Length > 1 then

            nodes
            |> Array.windowed 2
            |> Array.collect (fun n2 -> Dijkstra.shortestPath graph n2.[0].Node n2.[1].Node |> toGraphNodes g)
        else
            Array.empty

    let getShortestPath (g: GraphNode[]) (ids: string[]) =
        getShortestPathFromGraph g (toGraph g) ids

    let private getGraphNodesOfLine (imcode: string) (line: string) (graphNodes: GraphNode[]) =
        graphNodes
        |> Array.choose (fun n ->
            let edges = n.Edges |> Array.filter (fun e -> e.Country = imcode && e.Line = line)

            if edges.Length > 0 then
                Some { n with Edges = edges }
            else
                None)

#if FABLE_COMPILER
    let getPathOfLineFromGraph (g: GraphNode[]) (graph: Map<string, Dijkstra.Vertex>) (line: LineInfo) =
#else
    let getPathOfLineFromGraph (g: GraphNode[]) (graph: Map<string, Dijkstra.Vertex<string>>) (line: LineInfo) =
#endif
        let graphNodesOfLine = getGraphNodesOfLine line.Country line.Line g
        let path = getShortestPath graphNodesOfLine line.UOPIDs

        // change inconsistent data
        if
            0 < path.Length
            && 1 = path[0].Edges.Length
            && 0.0<_> = path[0].Edges[0].Length
            && 0.0<_> = path[0].Edges[0].StartKm
        then
            Array.set
                path
                0
                { path[0] with
                    Edges =
                        [| { path[0].Edges[0] with
                               StartKm = path[0].Edges[0].EndKm } |] }

            path
        else
            path

    let getPathOfLine (g: GraphNode[]) (line: LineInfo) =
        getPathOfLineFromGraph g (toGraph g) line

    let private compact (n1: GraphNode) (n2: GraphNode) (useMaxSpeed: bool) : (GraphNode * GraphNode option) =
        let edge1 = n1.Edges.[0]
        let edge2 = n2.Edges.[0]

        if
            edge1.Line = edge2.Line
            && (not useMaxSpeed || edge1.MaxSpeed = edge2.MaxSpeed)
            && n1.Node <> edge2.Node
        then

            let edge =
                { Node = edge2.Node
                  Cost = edge1.Cost + edge2.Cost
                  Line = edge1.Line
                  Country = edge1.Country
                  MaxSpeed =
                    if edge1.MaxSpeed < edge2.MaxSpeed then
                        edge2.MaxSpeed
                    else
                        edge1.MaxSpeed
                  Electrified = edge1.Electrified && edge2.Electrified
                  StartKm = edge1.StartKm
                  EndKm = edge2.EndKm
                  Length = edge1.Length + edge2.Length }

            { Node = n1.Node; Edges = [| edge |] }, None
        else
            n1, Some(n2)

    type State =
        { Node: GraphNode option
          Nodes: GraphNode list }

    let private internalGetCompactPath (path: GraphNode[]) (useMaxSpeed: bool) =
        let s =
            path
            |> Array.fold
                (fun (s: State) p ->
                    match s.Node with
                    | Some n ->
                        match compact n p useMaxSpeed with
                        | n, Some p -> { Node = Some p; Nodes = n :: s.Nodes }
                        | n, None -> { Node = Some n; Nodes = s.Nodes }
                    | _ -> { Node = Some p; Nodes = s.Nodes })
                { Node = None; Nodes = [] }

        match s.Node with
        | Some node -> (node :: s.Nodes)
        | None -> s.Nodes
        |> List.rev
        |> List.toArray

    /// path with folded lines
    let getCompactPath (path: GraphNode[]) = internalGetCompactPath path false

    let getLineOfGraphNode (node: GraphNode) =
        node.Edges.[0].Country, node.Edges.[0].Line

    let lengthOfPath (path: GraphNode[]) =
        path |> Array.sumBy (fun node -> node.Edges.[0].Length)

    let costOfPath (path: GraphNode[]) =
        path |> Array.sumBy (fun node -> node.Edges.[0].Cost)

    let printPath (path: GraphNode[]) =
        path
        |> Array.iter (fun node ->
            printfn
                "%s %s %s %.3f %.3f %.1f %i %i"
                node.Node
                node.Edges.[0].Node
                node.Edges.[0].Line
                node.Edges.[0].StartKm
                node.Edges.[0].EndKm
                node.Edges.[0].Length
                node.Edges.[0].MaxSpeed
                node.Edges.[0].Cost)

        printfn "%.1f %i" (lengthOfPath path) (costOfPath path)

    let printPathEx (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>) (path: GraphNode[]) =
        let mutable totalCost = 0
        let mutable totalLength = 0.0<_>

        path
        |> Array.iter (fun node ->
            let opInfo = opInfos.[node.Node]
            totalCost <- totalCost + node.Edges.[0].Cost
            totalLength <- totalLength + node.Edges.[0].Length

            printfn
                "%s %s %s %.3f %.3f %.1f %i %i -- %s totalCost: %d totalLength %.3f"
                node.Node
                node.Edges.[0].Node
                node.Edges.[0].Line
                node.Edges.[0].StartKm
                node.Edges.[0].EndKm
                node.Edges.[0].Length
                node.Edges.[0].MaxSpeed
                node.Edges.[0].Cost
                opInfo.Name
                totalCost
                totalLength)

        printfn "%.1f %i" (lengthOfPath path) (costOfPath path)

    let private enhancePathNodeWithMaxSpeed (n: GraphNode) (graphNodes: GraphNode[]) =
        let imcode = n.Edges.[0].Country
        let line = n.Edges.[0].Line
        let graphNodesOfLine = getGraphNodesOfLine imcode line graphNodes

        let spath = getShortestPath graphNodesOfLine [| n.Node; n.Edges.[0].Node |]

        internalGetCompactPath spath true

    let getCompactPathWithMaxSpeed (path: GraphNode[]) (graphNodes: GraphNode[]) =
        getCompactPath path
        |> Array.collect (fun n -> enhancePathNodeWithMaxSpeed n graphNodes)

    let isWalkingPath (node: GraphNode) = node.Edges.[0].Line.StartsWith("99")

    let private splitNodes (cond: GraphNode -> bool) (path: GraphNode[]) =
        Array.foldBack
            (fun x (s: (GraphNode list) list) ->
                let headList = s.Head

                if cond x then [] :: s else ((x :: headList) :: s.Tail))
            path
            [ [] ]
        |> List.map List.toArray
        |> List.toArray

    let private getLocationsOfSolPath
        (g: GraphNode[])
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (excludedRinfTypes: int[])
        (path: GraphNode[])
        =
        if path.Length > 0 then

            let lastLonlat =
                path
                |> Array.last
                |> fun node ->
                    let nodeOfEdge = g |> Array.find (fun n -> n.Node = node.Edges.[0].Node)

                    { Longitude = opInfos.[nodeOfEdge.Node].Longitude
                      Latitude = opInfos.[nodeOfEdge.Node].Latitude }

            let lonlats =
                path
                |> Array.filter (fun node ->
                    if excludedRinfTypes.Length = 0 then
                        true
                    else
                        excludedRinfTypes |> Array.contains (opInfos.[node.Node].RinfType) |> not)
                |> Array.map (fun node ->
                    { Longitude = opInfos.[node.Node].Longitude
                      Latitude = opInfos.[node.Node].Latitude })

            Array.append lonlats [| lastLonlat |]
        else
            [||]

    let getLocationsOfPath
        (g: GraphNode[])
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (path: GraphNode[])
        =
        splitNodes isWalkingPath path
        |> Array.map (getLocationsOfSolPath g opInfos [||])
        |> Array.filter (fun l -> l.Length > 0)

    let getFilteredLocationsOfPath
        (g: GraphNode[])
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (path: GraphNode[])
        (excludedRinfTypes: int[])
        =
        splitNodes isWalkingPath path
        |> Array.map (getLocationsOfSolPath g opInfos excludedRinfTypes)
        |> Array.filter (fun l -> l.Length > 0)

    let toPathElement
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (lineInfos: System.Collections.Generic.Dictionary<string, LineInfo>)
        (n: GraphNode)
        : PathElement =
        let edge = n.Edges.[0]

        { From = opInfos.[n.Node].Name
          FromOPID = n.Node
          To = opInfos.[edge.Node].Name
          ToOPID = edge.Node
          Line = edge.Line
          LineText = lineInfos.[edge.Line].Name
          Country = edge.Country
          StartKm = edge.StartKm
          EndKm = edge.EndKm
          MaxSpeed = edge.MaxSpeed }

    let getBRouterUrl (locations: Location[]) =
        if locations.Length > 0 then

            let lonlats =
                locations
                |> Array.map (fun loc -> sprintf "%f,%f" loc.Longitude loc.Latitude)
                |> String.concat ";"

            let (midLon, midLat) =
                locations.[locations.Length / 2]
                |> fun loc -> (sprintf "%f" loc.Longitude, sprintf "%f" loc.Latitude)

            sprintf
                "https://brouter.de/brouter-web/#map=10/%s/%s/osm-mapnik-german_style&lonlats=%s&profile=rail"
                midLat
                midLon
                (lonlats)
        else
            ""

    let getBRouterPoIUrl (locations: PoILocation[]) =
        if locations.Length > 0 then

            let lonlats =
                locations
                |> Array.map (fun loc ->
                    sprintf "%f,%f,%s" loc.Longitude loc.Latitude (loc.Content.Replace(" ", "%20")))
                |> String.concat ";"

            let (midLon, midLat) =
                locations.[locations.Length / 2]
                |> fun loc -> (sprintf "%f" loc.Longitude, sprintf "%f" loc.Latitude)

            sprintf
                "https://brouter.de/brouter-web/#map=10/%s/%s/osm-mapnik-german_style&pois=%s&profile=rail"
                midLat
                midLon
                (lonlats)
        else
            ""

/// edge in multi objective graph
type MoGraphEdge =
    { Node: string
      LineCost: int
      DistanceCost: int
      Line: string
      Country: string
      MaxSpeed: int<km / h>
      Electrified: bool
      StartKm: float<km>
      EndKm: float<km>
      Length: float<km> }

/// node in multi objective graph
type MoGraphNode = { Node: string; Edges: MoGraphEdge[] }

module MoGraph =
    let toMoNodeName (node: string) (country: string) (line: string) : string = $"{node}/{country}/{line}"

    let fromMoNodeName (node: string) : (string * string * string) option =
        match node.Split [| '/' |] with
        | [| node; country; line |] -> Some(node, country, line)
        | _ -> None

    let toGraphNodePath (g: MoGraphNode array) (nodes: (uint32 * (uint32 array)) array) : GraphNode array =
        let toGraphEdge
            (edges: MoGraphEdge array)
            (moName: string)
            (n: string)
            (cost: int)
            (line: string)
            (country: string)
            : GraphEdge option =
            edges
            |> Array.tryFind (fun edge -> edge.Node = moName && edge.Line = line)
            |> Option.bind (fun edge ->
                Some
                    { Node = n
                      Cost = cost
                      Line = line
                      Country = country
                      MaxSpeed = edge.MaxSpeed
                      Electrified = edge.Electrified
                      StartKm = edge.StartKm
                      EndKm = edge.EndKm
                      Length = edge.Length })

        nodes
        |> Array.map (fun (n, c) -> g[int n], c[1])
        |> Array.filter (fun (node, _) -> fromMoNodeName node.Node |> Option.isSome)
        |> Array.pairwise
        |> Array.choose (fun ((n1, c1), (n2, c2)) ->
            match fromMoNodeName n1.Node, fromMoNodeName n2.Node with
            | Some(fst, _, lFst), Some(snd, country, lSnd) ->
                if lFst = lSnd then
                    toGraphEdge n1.Edges n2.Node snd (int (c2 - c1)) lSnd country
                    |> Option.bind (fun edge -> Some({ Node = fst; Edges = [| edge |] }: GraphNode))
                else
                    None
            | _, _ -> None)

    // three nodes are generated in the multi objective graph of type MoGraphNode[]
    // for every edge n1 -> line -> n2 with cost c in the graph of type GraphNode[]
    // 1) n1             -> line -> n1/contry/line with cost (10,0)
    // 2) n1/contry/line -> line -> n2/contry/line with cost (0,c)
    // 3) n2/contry/line -> line -> n2             with cost (10,0)
    let toMoGraphNodes (node: string) (edge: GraphEdge) : MoGraphNode array =
        [| { Node = node
             Edges =
               [| { Node = toMoNodeName node edge.Country edge.Line
                    LineCost = 10
                    DistanceCost = 0
                    Line = edge.Line
                    Country = edge.Country
                    MaxSpeed = 0<_>
                    Electrified = false
                    StartKm = 0.0<_>
                    EndKm = 0.0<_>
                    Length = 0.0<_> } |] }
           { Node = toMoNodeName node edge.Country edge.Line
             Edges =
               [| { Node = toMoNodeName edge.Node edge.Country edge.Line
                    LineCost = 0
                    DistanceCost = edge.Cost
                    Line = edge.Line
                    Country = edge.Country
                    MaxSpeed = edge.MaxSpeed
                    Electrified = edge.Electrified
                    StartKm = edge.StartKm
                    EndKm = edge.EndKm
                    Length = edge.Length } |] }
           { Node = toMoNodeName edge.Node edge.Country edge.Line
             Edges =
               [| { Node = edge.Node
                    LineCost = 10
                    DistanceCost = 0
                    Line = edge.Line
                    Country = edge.Country
                    MaxSpeed = 0<_>
                    Electrified = false
                    StartKm = 0.0<_>
                    EndKm = 0.0<_>
                    Length = 0.0<_> } |] } |]

    let toMoGraph (g: GraphNode array) : MoGraphNode array =
        let map: Map<string, MoGraphEdge[]> =
            g
            |> Array.fold
                (fun acc node ->
                    node.Edges
                    |> Array.fold
                        (fun acc edge ->
                            toMoGraphNodes node.Node edge
                            |> Array.fold
                                (fun acc n' ->
                                    match acc.TryGetValue n'.Node with
                                    | true, edges ->
                                        if edges |> Array.exists (fun e -> e = n'.Edges[0]) then
                                            acc
                                        else
                                            acc.Change(
                                                n'.Node,
                                                (fun edges ->
                                                    match edges with
                                                    | Some edges -> Some(Array.append edges [| n'.Edges[0] |])
                                                    | None -> None)
                                            )
                                    | false, _ -> acc.Add(n'.Node, n'.Edges))
                                acc)
                        acc)
                (Map<string, MoGraphEdge[]>([]))

        map.Keys
        |> Seq.toArray
        |> Array.map (fun node -> { Node = node; Edges = map[node] })

    let toMap (g: MoGraphNode array) : Map<string, int> =
        g
        |> Array.mapi (fun i n -> i, n)
        |> Array.fold (fun acc (i, node) -> acc.Change(node.Node, (fun _ -> Some i))) (Map<string, int>([]))

    let toArcs (g: MoGraphNode array) : (int * int * int * int) array =
        let g' = g |> Array.mapi (fun i n -> i, n)
        let map = toMap g

        g'
        |> Array.fold
            (fun acc (i, node) ->
                node.Edges
                |> Array.fold
                    (fun acc edge ->
                        let arc: (int * int * int * int) =
                            i, map[edge.Node], edge.LineCost, edge.DistanceCost

                        arc :: acc)
                    acc)
            []
        |> List.toArray

    type Solution = { Cost: int; Path: GraphNode array }

    let getShortestPathFromGraph g arcs (map: Map<string, int>) source target maxSolutions : Solution array =
        let arcs =
            arcs
            |> Array.mapi (fun i (f, t, c1, c2) -> uint32 f, TMosp.Arc(uint32 t, [| uint32 c1; uint32 c2 |], uint32 i))

        match map.TryGetValue source, map.TryGetValue target with
        | (true, source), (true, target) ->
            TMosp.Api.fromArcs (arcs, uint32 source, uint32 target, maxSolutions)
            |> Array.map (fun arr ->
                { Cost = int ((snd arr[arr.Length - 1])[1])
                  Path = toGraphNodePath g arr })
        | _, _ -> [||]

    let getShortestPath (g: GraphNode array) (source: string) (target: string) (maxSolutions: int) : Solution array =
        let g = toMoGraph g
        getShortestPathFromGraph g (toArcs g) (toMap g) source target maxSolutions
