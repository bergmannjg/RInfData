namespace RInfGraph

open FSharpx.Collections

type GraphEdge =
    { Node: string
      Cost: int
      Line: string
      Length: float }

type GraphNode =
    { Node: string
      Latitude: float
      Longitude: float
      Edges: GraphEdge [] }

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

    let private toGraphNode (g: GraphNode []) node =
        match node with
        | [| n1; n2 |] ->
            let node1 = g |> Seq.find (fun n -> n.Node = n1)

            let (line, length) =
                node1.Edges
                |> Seq.tryFind (fun edge -> edge.Node = n2)
                |> Option.map (fun edge -> (edge.Line, edge.Length))
                |> Option.defaultValue ("", 0.0)

            { Node = n1
              Latitude = node1.Latitude
              Longitude = node1.Longitude
              Edges =
                  [| { Node = n2
                       Cost = 0
                       Line = line
                       Length = length } |] }
            |> Some
        | _ -> None

    let private toGraphNodes (g: GraphNode []) (path: Dijkstra.Path<string> option) =
        match path with
        | Some path ->
            path.Nodes
            |> Seq.windowed 2
            |> Seq.map (toGraphNode g)
            |> Seq.choose id
            |> Seq.toArray
        | None -> Array.empty

    let getShortestPathFromGraph (g: GraphNode []) (graph: Map<string, Dijkstra.Vertex>) (ids: string []) =

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
                    |> toGraphNodes g)
        else
            printfn "nodes not found"
            Array.empty

    let getShortestPath (g: GraphNode []) (ids: string []) =
        getShortestPathFromGraph g (toGraph g) ids

    let printShortestPath (nodes: array<GraphNode>) =
        nodes
        |> Array.iter
            (fun node ->
                printfn "%s %s %s %.1f" node.Node node.Edges.[0].Node node.Edges.[0].Line node.Edges.[0].Length)

        sprintf
            "%.1f"
            (nodes
             |> Array.sumBy (fun node -> node.Edges.[0].Length))

    let printBRouterUrl (g: GraphNode []) (nodes: array<GraphNode>) =
        if nodes.Length > 0 then
            let lonlats =
                nodes
                |> Array.map (fun node -> sprintf "%f,%f" node.Longitude node.Latitude)
                |> String.concat ";"

            let lastLonlat =
                nodes
                |> Array.last
                |> fun node ->
                    let nodeOfEdge =
                        g
                        |> Array.find (fun n -> n.Node = node.Edges.[0].Node)

                    sprintf "%f,%f" nodeOfEdge.Longitude nodeOfEdge.Latitude

            let (midLon, midLat) =
                nodes.[nodes.Length / 2]
                |> fun node -> (sprintf "%f" node.Longitude, sprintf "%f" node.Latitude)

            sprintf
                "https://brouter.de/brouter-web/#map=10/%s/%s/osm-mapnik-german_style&lonlats=%s&profile=rail"
                midLat
                midLon
                (lonlats + ";" + lastLonlat)
        else
            ""
