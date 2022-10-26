namespace RInfGraph

open FSharpx.Collections

type Location = { Latitude: float; Longitude: float }

type PoILocation =
    { Latitude: float
      Longitude: float
      Content: string }

type OpInfo =
    { UOPID: string
      Name: string
      Latitude: float
      Longitude: float }

type LineInfo =
    { Line: string
      IMCode: string
      Name: string
      Length: float
      StartKm: float
      EndKm: float
      UOPIDs: string []
      Tunnels: string [] }

type TunnelInfo =
    { Tunnel: string
      StartLong: float
      StartLat: float
      StartKm: float
      EndLong: float
      EndLat: float
      EndKm: float
      SingelTrack: bool
      Line: string }

type GraphEdge =
    { Node: string
      Cost: int
      Line: string
      IMCode: string
      MaxSpeed: int
      StartKm: float
      EndKm: float
      Length: float }

type GraphNode = { Node: string; Edges: GraphEdge [] }

type PathElement =
    { From: string
      FromOPID: string
      To: string
      ToOPID: string
      Line: string
      LineText: string
      IMCode: string
      StartKm: float
      EndKm: float
      MaxSpeed: int }

module Graph =
    let private toVertex (node: GraphNode) =
        (node.Node,
         node.Edges
         |> Array.map (fun e -> (e.Cost, e.Node))
         |> Array.toList)
        |> fun (n, l) -> Dijkstra.makeVertex n l

    let addEdge
        (opidFrom: string)
        (opidTo: string)
        (line: string)
        (imcode: string)
        (cost: int)
        (maxSpeed: int)
        (startKm: float)
        (endKm: float)
        (length: float)
        (graph: Map<string, GraphEdge list>)
        =
        let createEdge
            (opEndId: string)
            (line: string)
            (cost: int)
            (maxSpeed: int)
            (startKm: float)
            (endKm: float)
            (length: float)
            : GraphEdge =
            { Node = opEndId
              Cost = cost
              Line = line
              IMCode = imcode
              MaxSpeed = maxSpeed
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
            .Add(
                opidFrom,
                (createEdge opidTo line cost maxSpeed startKm endKm length)
                :: edges1
            )
            .Add(
                opidTo,
                (createEdge opidFrom line cost maxSpeed endKm startKm length)
                :: edges2
            )

    let toGraph (g: GraphNode []) =
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

    let private toGraphNode (g: GraphNode []) nodes =
        match nodes with
        | [| n1; n2 |] ->
            let node1 = g |> Seq.find (fun n -> n.Node = n1)

            let edges =
                node1.Edges
                |> Array.filter (fun edge -> edge.Node = n2)
                |> Array.sortBy (fun e -> e.Cost)

            let (cost, line, imcode, maxSpeed, startKm, endKm, length) =
                edges
                |> Seq.tryHead
                |> Option.map (fun edge ->
                    (edge.Cost, edge.Line, edge.IMCode, edge.MaxSpeed, edge.StartKm, edge.EndKm, edge.Length))
                |> Option.defaultValue (0, "", "", 0, 0.0, 0.0, 0.0)

            { Node = n1
              Edges =
                [| { Node = n2
                     Cost = cost
                     Line = line
                     IMCode = imcode
                     MaxSpeed = maxSpeed
                     StartKm = startKm
                     EndKm = endKm
                     Length = length } |] }
            |> Some
        | _ -> None

#if FABLE_COMPILER
    let private toGraphNodes (g: GraphNode []) (path: Dijkstra.Path option) =
#else
    let private toGraphNodes (g: GraphNode []) (path: Dijkstra.Path<string> option) =
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
    let getShortestPathFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex>) (ids: string []) =
#else
    let getShortestPathFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex<string>>) (ids: string []) =
#endif
        let nodes =
            ids
            |> Array.map (fun id -> g |> Seq.tryFind (fun node -> node.Node = id))
            |> Array.choose id

        if nodes.Length = ids.Length && ids.Length > 1 then

            nodes
            |> Array.windowed 2
            |> Array.collect (fun n2 ->
                Dijkstra.shortestPath graph n2.[0].Node n2.[1].Node
                |> toGraphNodes g)
        else
            Array.empty

    let getShortestPath (g: GraphNode []) (ids: string []) =
        getShortestPathFromGraph g (toGraph g) ids

    let private getGraphNodesOfLine (imcode: string) (line: string) (graphNodes: GraphNode []) =
        graphNodes
        |> Array.choose (fun n ->
            let edges =
                n.Edges
                |> Array.filter (fun e -> e.IMCode = imcode && e.Line = line)

            if edges.Length > 0 then
                Some { n with Edges = edges }
            else
                None)

#if FABLE_COMPILER
    let getPathOfLineFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex>) (line: LineInfo) =
#else
    let getPathOfLineFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex<string>>) (line: LineInfo) =
#endif
        let graphNodesOfLine = getGraphNodesOfLine line.IMCode line.Line g
        getShortestPath graphNodesOfLine line.UOPIDs

    let getPathOfLine (g: GraphNode []) (line: LineInfo) =
        getPathOfLineFromGraph g (toGraph g) line

    let private compact (n1: GraphNode) (n2: GraphNode) (useMaxSpeed: bool) : (GraphNode * GraphNode option) =
        let edge1 = n1.Edges.[0]
        let edge2 = n2.Edges.[0]

        if edge1.Line = edge2.Line
           && (not useMaxSpeed || edge1.MaxSpeed = edge2.MaxSpeed)
           && n1.Node <> edge2.Node then

            let edge =
                { Node = edge2.Node
                  Cost = edge1.Cost + edge2.Cost
                  Line = edge1.Line
                  IMCode = edge1.IMCode
                  MaxSpeed =
                    if edge1.MaxSpeed < edge2.MaxSpeed then
                        edge1.MaxSpeed
                    else
                        edge2.MaxSpeed
                  StartKm = edge1.StartKm
                  EndKm = edge2.EndKm
                  Length = edge1.Length + edge2.Length }

            { Node = n1.Node; Edges = [| edge |] }, None
        else
            n1, Some(n2)

    type State =
        { Node: GraphNode option
          Nodes: GraphNode list }

    let private internalGetCompactPath (path: GraphNode []) (useMaxSpeed: bool) =
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

    let getCompactPath (path: GraphNode []) = internalGetCompactPath path false

    let getLineOfGraphNode (node: GraphNode) =
        node.Edges.[0].IMCode, node.Edges.[0].Line

    let lengthOfPath (path: GraphNode []) =
        path
        |> Array.sumBy (fun node -> node.Edges.[0].Length)

    let costOfPath (path: GraphNode []) =
        path
        |> Array.sumBy (fun node -> node.Edges.[0].Cost)

    let printPath (path: GraphNode []) =
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

    type internal Candidate = (string * string * string * string * string)

    // find 2 entries of same line in cpath
    let private getCandidateSameLine (cpath: GraphNode []) (graphNodes: GraphNode []) (choosen: Candidate list) =
        let groups =
            cpath
            |> Array.groupBy (fun p -> getLineOfGraphNode p)

        match groups
              |> Array.tryFind (fun (_, l) -> l.Length = 2)
            with
        | Some ((imcode, line), l) ->
            let fromNode = l.[0].Edges.[0].Node
            let toNode = l.[1].Node

            let fromIndex =
                cpath
                |> Array.findIndex (fun n -> n.Edges.[0].Node = fromNode)

            let toIndex =
                cpath
                |> Array.findIndex (fun n -> n.Node = toNode)

            let nodesBetween =
                cpath
                |> Array.skip (fromIndex + 1)
                |> Array.take (toIndex - (fromIndex + 1))

            let length = lengthOfPath nodesBetween

            if length > 0 && length < 50.0 && fromNode <> toNode then
                let candidate = ("CandidateSameLine", imcode, line, fromNode, toNode)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate

            else
                None
        | None -> None

    let private getCandidateReplaceSmallEdge
        (cpath: GraphNode [])
        (graphNodes: GraphNode [])
        (choosen: Candidate list)
        =
        let windowed = cpath |> Array.windowed 2

        let chooser (xs: GraphNode []) =
            let edgeOfLast = xs[1].Edges.[0]
            let edgeOfFirst = xs[0].Edges.[0]

            if edgeOfLast.Length < 10.0
               && edgeOfLast.Length * 5.0 < edgeOfFirst.Length then
                let candidate =
                    ("CandidateReplaceSmallEdge",
                     edgeOfFirst.IMCode,
                     edgeOfFirst.Line,
                     edgeOfFirst.Node,
                     edgeOfLast.Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            else if edgeOfFirst.Length < 10.0
                    && edgeOfFirst.Length * 5.0 < edgeOfLast.Length then
                let candidate =
                    ("CandidateReplaceSmallEdge", edgeOfLast.IMCode, edgeOfLast.Line, xs[0].Node, edgeOfFirst.Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            else
                None

        windowed |> Array.choose chooser |> Array.tryHead

    let private getLastEdge (cpath: GraphNode []) =
        (cpath |> Array.last).Edges |> Array.last

    let private getLastNode (cpath: GraphNode []) = (getLastEdge cpath).Node

    let private getCandidateSameLineWithLastNode
        (cpath: GraphNode [])
        (graphNodes: GraphNode [])
        (choosen: Candidate list)
        =
        if cpath.Length > 2 then
            let lastNode = getLastNode cpath

            let lineOfLastNode =
                match graphNodes
                      |> Array.tryFind (fun n -> n.Node = lastNode)
                    with
                | Some n -> n.Edges |> Array.map (fun e -> e.IMCode, e.Line)
                | None -> [||]

            match cpath
                  |> Array.take (cpath.Length - 1)
                  |> Array.tryFind (fun n ->
                      lineOfLastNode
                      |> Array.contains (n.Edges.[0].IMCode, n.Edges.[0].Line))
                with
            | Some node ->
                let candidate =
                    ("CandidateSameLineWithLastNode", node.Edges.[0].IMCode, node.Edges.[0].Line, node.Node, lastNode)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            | None -> None
        else
            None

    let private getCandidateLineToFirstNode (cpath: GraphNode []) (graphNodes: GraphNode []) (choosen: Candidate list) =
        if cpath.Length > 2 then
            let firstNode = cpath.[0].Node

            let existsEdgeToFirstNode (imcode: string) (line: string) =
                match graphNodes
                      |> Array.tryFind (fun n -> n.Node = firstNode)
                    with
                | Some n ->
                    n.Edges
                    |> Array.exists (fun e -> e.IMCode = imcode && e.Line = line)
                | None -> false

            match cpath
                  |> Array.skip 1
                  |> Array.take (System.Math.Min(cpath.Length - 1, 4))
                  |> Array.tryFind (fun n -> existsEdgeToFirstNode n.Edges[0].IMCode n.Edges[0].Line)
                with
            | Some n ->
                let candidate =
                    ("getCandidateSameLineWithFirstNode", n.Edges[0].IMCode, n.Edges[0].Line, firstNode, n.Edges[0].Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            | None -> None
        else
            None

    let private getCompactifyCandidate (path: GraphNode []) (graphNodes: GraphNode []) (choosen: Candidate list) =
        let cpath = getCompactPath path

        if cpath.Length > 2 then
            [| getCandidateSameLine
               getCandidateReplaceSmallEdge
               getCandidateSameLineWithLastNode
               getCandidateLineToFirstNode |]
            |> Array.tryPick (fun s -> s cpath graphNodes choosen)
        else
            None

    let private tryCompactifyPath (path: GraphNode []) (graphNodes: GraphNode []) (choosen: Candidate list) =
        match getCompactifyCandidate path graphNodes choosen with
        | Some candidate ->
            let (s, imcode, line, fromNode, toNode) = candidate

            let graphNodesOfLine = getGraphNodesOfLine imcode line graphNodes

            let spath = getShortestPath graphNodesOfLine [| fromNode; toNode |]

            if spath.Length > 0 then

                let fromIndex =
                    path
                    |> Array.tryFindIndex (fun n -> n.Edges.[0].Node = fromNode)

                let toIndex =
                    path
                    |> Array.tryFindIndex (fun n -> n.Node = toNode)

                let isPathFromFirstNode = spath.[0].Node = path.[0].Node

                let isPathToLastNode = (getLastNode path) = (getLastNode spath)

                match fromIndex, toIndex, isPathFromFirstNode, isPathToLastNode with
                | Some fromIndex, Some toIndex, _, _ ->
                    let n1 = path |> Array.take (fromIndex + 1)
                    let n2 = path |> Array.skip toIndex
                    Some(Array.concat [ n1; spath; n2 ], candidate)
                | Some fromIndex, None, _, true ->
                    let n1 = path |> Array.take (fromIndex + 1)
                    Some(Array.concat [ n1; spath ], candidate)
                | None, Some toIndex, true, _ ->
                    let n2 = path |> Array.skip toIndex
                    Some(Array.concat [ spath; n2 ], candidate)
                | None, None, true, true -> Some(spath, candidate)
                | _ -> None
            else
                Some(path, candidate)
        | None -> None

    let compactifyPath (path: GraphNode []) (graphNodes: GraphNode []) =
        let lengthOfOrigPath = lengthOfPath path

        let rec multiCompactifyPath path graphNodes maxDepth choosen =
            match tryCompactifyPath path graphNodes choosen with
            | Some (cpath, candidate) when System.Math.Abs(lengthOfOrigPath - (lengthOfPath cpath)) < 10.0 ->
                if maxDepth > 0 then
                    multiCompactifyPath cpath graphNodes (maxDepth - 1) (candidate :: choosen)
                else
                    cpath
            | _ -> path

        multiCompactifyPath path graphNodes 5 []

    let private enhancePathNodeWithMaxSpeed (n: GraphNode) (graphNodes: GraphNode []) =
        let imcode = n.Edges.[0].IMCode
        let line = n.Edges.[0].Line
        let graphNodesOfLine = getGraphNodesOfLine imcode line graphNodes

        let spath = getShortestPath graphNodesOfLine [| n.Node; n.Edges.[0].Node |]

        internalGetCompactPath spath true

    let getCompactPathWithMaxSpeed (path: GraphNode []) (graphNodes: GraphNode []) =
        getCompactPath path
        |> Array.collect (fun n -> enhancePathNodeWithMaxSpeed n graphNodes)

    let isWalkingPath (node: GraphNode) = node.Edges.[0].Line.StartsWith("99")

    let private splitNodes (cond: GraphNode -> bool) (path: GraphNode []) =
        Array.foldBack
            (fun x (s: (GraphNode list) list) ->
                let headList = s.Head

                if cond x then
                    [] :: s
                else
                    ((x :: headList) :: s.Tail))
            path
            [ [] ]
        |> List.map List.toArray
        |> List.toArray

    let private getLocationsOfSolPath
        (g: GraphNode [])
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (path: GraphNode [])
        =
        if path.Length > 0 then

            let lastLonlat =
                path
                |> Array.last
                |> fun node ->
                    let nodeOfEdge =
                        g
                        |> Array.find (fun n -> n.Node = node.Edges.[0].Node)

                    { Longitude = opInfos.[nodeOfEdge.Node].Longitude
                      Latitude = opInfos.[nodeOfEdge.Node].Latitude }

            let lonlats =
                path
                |> Array.map (fun node ->
                    { Longitude = opInfos.[node.Node].Longitude
                      Latitude = opInfos.[node.Node].Latitude })

            Array.append lonlats [| lastLonlat |]
        else
            [||]

    let getLocationsOfPath
        (g: GraphNode [])
        (opInfos: System.Collections.Generic.Dictionary<string, OpInfo>)
        (path: GraphNode [])
        =
        splitNodes isWalkingPath path
        |> Array.map (getLocationsOfSolPath g opInfos)
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
          IMCode = edge.IMCode
          StartKm = edge.StartKm
          EndKm = edge.EndKm
          MaxSpeed = edge.MaxSpeed }

    let getBRouterUrl (locations: Location []) =
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

    let getBRouterPoIUrl (locations: PoILocation []) =
        if locations.Length > 0 then

            let lonlats =
                locations
                |> Array.map (fun loc -> sprintf "%f,%f,%s" loc.Longitude loc.Latitude (loc.Content.Replace(" ", "%20")))
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
