namespace RInfGraph

open FSharpx.Collections

type Location = { Latitude: float; Longitude: float }

type PoILocation =
    { Latitude: float
      Longitude: float
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
      Latitude: float
      Longitude: float }

type LineInfo =
    { Line: string
      Country: string
      Name: string
      Length: float
      StartKm: float
      EndKm: float
      UOPIDs: string[]
      Tunnels: string[]
      Wikipedia: string Option }

type TunnelInfo =
    { Tunnel: string
      Length: float
      StartLong: float
      StartLat: float
      StartKm: float option
      StartOP: string
      EndLong: float
      EndLat: float
      EndKm: float option
      EndOP: string
      SingelTrack: bool
      Line: string }

type GraphEdge =
    { Node: string
      Cost: int
      Line: string
      Country: string
      MaxSpeed: int
      Electrified: bool
      StartKm: float
      EndKm: float
      Length: float }

type GraphNode = { Node: string; Edges: GraphEdge[] }

type PathElement =
    { From: string
      FromOPID: string
      To: string
      ToOPID: string
      Line: string
      LineText: string
      Country: string
      StartKm: float
      EndKm: float
      MaxSpeed: int }

module Graph =

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
                |> Option.defaultValue (0, "", "", 0, false, 0.0, 0.0, 0.0)

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
        getShortestPath graphNodesOfLine line.UOPIDs

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
                        edge1.MaxSpeed
                    else
                        edge2.MaxSpeed
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
        let mutable totalLength = 0.0

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

    type internal Candidate = (string * string * string * string * string)

    // find 2 entries of same line in cpath
    let private getCandidateSameLine (cpath: GraphNode[]) (graphNodes: GraphNode[]) (choosen: Candidate list) =
        let groups = cpath |> Array.groupBy (fun p -> getLineOfGraphNode p)

        match groups |> Array.tryFind (fun (_, l) -> l.Length = 2) with
        | Some((imcode, line), l) ->
            let fromNode = l.[0].Edges.[0].Node
            let toNode = l.[1].Node

            let fromIndex = cpath |> Array.findIndex (fun n -> n.Edges.[0].Node = fromNode)

            let toIndex = cpath |> Array.findIndex (fun n -> n.Node = toNode)

            let nodesBetween =
                cpath |> Array.skip (fromIndex + 1) |> Array.take (toIndex - (fromIndex + 1))

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

    let private getCandidateReplaceSmallEdge (cpath: GraphNode[]) (graphNodes: GraphNode[]) (choosen: Candidate list) =
        let windowed = cpath |> Array.windowed 2

        let chooser (xs: GraphNode[]) =
            let edgeOfLast = xs[1].Edges.[0]
            let edgeOfFirst = xs[0].Edges.[0]

            if edgeOfLast.Length < 10.0 && edgeOfLast.Length * 5.0 < edgeOfFirst.Length then
                let candidate =
                    ("CandidateReplaceSmallEdge",
                     edgeOfFirst.Country,
                     edgeOfFirst.Line,
                     edgeOfFirst.Node,
                     edgeOfLast.Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            else if edgeOfFirst.Length < 10.0 && edgeOfFirst.Length * 5.0 < edgeOfLast.Length then
                let candidate =
                    ("CandidateReplaceSmallEdge", edgeOfLast.Country, edgeOfLast.Line, xs[0].Node, edgeOfFirst.Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            else
                None

        windowed |> Array.choose chooser |> Array.tryHead

    let private getLastEdge (cpath: GraphNode[]) =
        (cpath |> Array.last).Edges |> Array.last

    let private getLastNode (cpath: GraphNode[]) = (getLastEdge cpath).Node

    let private getCandidateSameLineWithLastNode
        (cpath: GraphNode[])
        (graphNodes: GraphNode[])
        (choosen: Candidate list)
        =
        if cpath.Length > 2 then
            let lastNode = getLastNode cpath

            let lineOfLastNode =
                match graphNodes |> Array.tryFind (fun n -> n.Node = lastNode) with
                | Some n -> n.Edges |> Array.map (fun e -> e.Country, e.Line)
                | None -> [||]

            match
                cpath
                |> Array.take (cpath.Length - 1)
                |> Array.tryFind (fun n -> lineOfLastNode |> Array.contains (n.Edges.[0].Country, n.Edges.[0].Line))
            with
            | Some node ->
                let candidate =
                    ("CandidateSameLineWithLastNode", node.Edges.[0].Country, node.Edges.[0].Line, node.Node, lastNode)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            | None -> None
        else
            None

    let private getCandidateLineToFirstNode (cpath: GraphNode[]) (graphNodes: GraphNode[]) (choosen: Candidate list) =
        if cpath.Length > 2 then
            let firstNode = cpath.[0].Node

            let existsEdgeToFirstNode (imcode: string) (line: string) =
                match graphNodes |> Array.tryFind (fun n -> n.Node = firstNode) with
                | Some n -> n.Edges |> Array.exists (fun e -> e.Country = imcode && e.Line = line)
                | None -> false

            match
                cpath
                |> Array.skip 1
                |> Array.take (System.Math.Min(cpath.Length - 1, 4))
                |> Array.tryFind (fun n -> existsEdgeToFirstNode n.Edges[0].Country n.Edges[0].Line)
            with
            | Some n ->
                let candidate =
                    ("getCandidateSameLineWithFirstNode",
                     n.Edges[0].Country,
                     n.Edges[0].Line,
                     firstNode,
                     n.Edges[0].Node)

                if choosen |> List.contains candidate then
                    None
                else
                    Some candidate
            | None -> None
        else
            None

    let private getCompactifyCandidate (path: GraphNode[]) (graphNodes: GraphNode[]) (choosen: Candidate list) =
        let cpath = getCompactPath path

        if cpath.Length > 2 then
            [| getCandidateSameLine
               getCandidateReplaceSmallEdge
               getCandidateSameLineWithLastNode
               getCandidateLineToFirstNode |]
            |> Array.tryPick (fun s -> s cpath graphNodes choosen)
        else
            None

    let private tryCompactifyPath (path: GraphNode[]) (graphNodes: GraphNode[]) (choosen: Candidate list) =
        match getCompactifyCandidate path graphNodes choosen with
        | Some candidate ->
            let (s, imcode, line, fromNode, toNode) = candidate

            let graphNodesOfLine = getGraphNodesOfLine imcode line graphNodes

            let spath = getShortestPath graphNodesOfLine [| fromNode; toNode |]

            if spath.Length > 0 then

                let fromIndex = path |> Array.tryFindIndex (fun n -> n.Edges.[0].Node = fromNode)

                let toIndex = path |> Array.tryFindIndex (fun n -> n.Node = toNode)

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

    let compactifyPath (path: GraphNode[]) (graphNodes: GraphNode[]) =
        let lengthOfOrigPath = lengthOfPath path

        let rec multiCompactifyPath path graphNodes maxDepth choosen =
            match tryCompactifyPath path graphNodes choosen with
            | Some(cpath, candidate) when System.Math.Abs(lengthOfOrigPath - (lengthOfPath cpath)) < 10.0 ->
                if maxDepth > 0 then
                    multiCompactifyPath cpath graphNodes (maxDepth - 1) (candidate :: choosen)
                else
                    cpath
            | _ -> path

        multiCompactifyPath path graphNodes 5 []

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
