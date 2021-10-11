namespace RInfGraph

open FSharpx.Collections

type Location = { Latitude: float; Longitude: float }

type OpInfo =
    { UOPID: string
      Name: string
      Latitude: float
      Longitude: float }

type LineInfo =
    { Line: string
      Name: string
      Length: float
      StartKm: float
      EndKm: float
      UOPIDs: string [] }

type GraphEdge =
    { Node: string
      Cost: int
      Line: string
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

    let toGraph (g: GraphNode []) =
        g
        |> Array.fold
            (fun (graph: Map<string, Dijkstra.Vertex>) n ->
                let v = toVertex n
                graph.Add(v.Id, v))
            (Map.empty)

    let private toGraphNode (g: GraphNode []) (line: string option) nodes =
        match nodes with
        | [| n1; n2 |] ->
            let node1 = g |> Seq.find (fun n -> n.Node = n1)

            let edges =
                node1.Edges
                |> Array.filter
                    (fun edge ->
                        edge.Node = n2
                        && match line with
                           | Some line -> line = edge.Line
                           | None -> true)
                |> Array.sortBy (fun e -> e.Cost)

            let (cost, line, maxSpeed, startKm, endKm, length) =
                edges
                |> Seq.tryHead
                |> Option.map (fun edge -> (edge.Cost, edge.Line, edge.MaxSpeed, edge.StartKm, edge.EndKm, edge.Length))
                |> Option.defaultValue (0, "", 0, 0.0, 0.0, 0.0)

            { Node = n1
              Edges =
                  [| { Node = n2
                       Cost = cost
                       Line = line
                       MaxSpeed = maxSpeed
                       StartKm = startKm
                       EndKm = endKm
                       Length = length } |] }
            |> Some
        | _ -> None

    let private toGraphNodes (g: GraphNode []) (line: string option) (path: Dijkstra.Path option) =
        match path with
        | Some path ->
            path.Nodes
            |> Seq.windowed 2
            |> Seq.map (toGraphNode g line)
            |> Seq.choose id
            |> Seq.toArray
        | None -> Array.empty

    let private getShortestPathFromGraphWithCond
        (g: GraphNode [])
        (graph: Map<string, Dijkstra.Vertex>)
        (ids: string [])
        (line: string option)
        =

        let nodes =
            ids
            |> Array.map (fun id -> g |> Seq.tryFind (fun node -> node.Node = id))
            |> Array.choose id

        if nodes.Length = ids.Length && ids.Length > 1 then

            nodes
            |> Array.windowed 2
            |> Array.collect
                (fun n2 ->
                    Dijkstra.shortestPath graph n2.[0].Node n2.[1].Node
                    |> toGraphNodes g line)
        else
            printfn "nodes not found"
            Array.empty

    let getShortestPathFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex>) (ids: string []) =
        getShortestPathFromGraphWithCond g graph ids None

    let getShortestPath (g: GraphNode []) (ids: string []) =
        getShortestPathFromGraph g (toGraph g) ids

    let getPathOfLineFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex>) (line: LineInfo) =
        getShortestPathFromGraphWithCond g graph line.UOPIDs (Some line.Line)

    let getPathOfLine (g: GraphNode []) (line: LineInfo) =
        getPathOfLineFromGraph g (toGraph g) line

    let private compact (n1: GraphNode) (n2: GraphNode) : (GraphNode * GraphNode option) =
        let edge1 = n1.Edges.[0]
        let edge2 = n2.Edges.[0]

        if edge1.Line = edge2.Line && n1.Node <> edge2.Node then

            let edge =
                { Node = edge2.Node
                  Cost = edge1.Cost + edge2.Cost
                  Line = edge1.Line
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

    let getCompactPath (path: GraphNode []) =
        let s =
            path
            |> Array.fold
                (fun (s: State) p ->
                    match s.Node with
                    | Some n ->
                        match compact n p with
                        | n, Some p -> { Node = Some p; Nodes = n :: s.Nodes }
                        | n, None -> { Node = Some n; Nodes = s.Nodes }
                    | _ -> { Node = Some p; Nodes = s.Nodes })
                { Node = None; Nodes = [] }

        match s.Node with
        | Some node -> (node :: s.Nodes)
        | None -> s.Nodes
        |> List.rev
        |> List.toArray

    let printPath (path: GraphNode []) =
        path
        |> Array.iter
            (fun node ->
                printfn
                    "%s %s %s %.3f %.3f %.1f %i"
                    node.Node
                    node.Edges.[0].Node
                    node.Edges.[0].Line
                    node.Edges.[0].StartKm
                    node.Edges.[0].EndKm
                    node.Edges.[0].Length
                    node.Edges.[0].Cost)

        printfn
            "%.1f"
            (path
             |> Array.sumBy (fun node -> node.Edges.[0].Length))

    let isWalkingPath (node: GraphNode) = node.Edges.[0].Line.StartsWith("99")

    let splitNodes (cond: GraphNode -> bool) (path: GraphNode []) =
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
                |> Array.map
                    (fun node ->
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
