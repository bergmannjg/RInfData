/// see http://jaskula.fr//blog/2016/12-19-data-structures-and-algorithms-helping-santa-claus-find-his-road-to-san-francisco/index.html
module Dijkstra

open System
open Microsoft.FSharp.Core
open FSharpx.Collections

type Edge =
    { DestinationVertexId: string
      Cost: int }

[<CustomComparison; CustomEquality>]
type Vertex =
    { Id: string
      Cost: int
      Edges: Edge list
      Path: string list }
    interface IComparable<Vertex> with
        member this.CompareTo other = compare this.Cost other.Cost

    interface IComparable with
        member this.CompareTo(obj: obj) =
            match obj with
            | :? Vertex -> compare this.Cost (unbox<Vertex> obj).Cost
            | _ -> invalidArg "obj" "Must be of type Vertex"

    override x.Equals(yobj) =
        match yobj with
        | :? Vertex as y -> (x.Id = y.Id && x.Edges = y.Edges)
        | _ -> false

    override x.GetHashCode() = hash x.Id

type Path<'a when 'a: equality> = { Nodes: 'a list; Cost: int }

let addEdge vertex e =
    { vertex with
          Edges = e :: vertex.Edges }

type Vertex with
    member this.AddEdge = addEdge this

let makeEdge (distance, destVertexId) =
    { DestinationVertexId = destVertexId
      Cost = distance }

let makeVertex vertexId edges =
    { Id = vertexId
      Cost = Int32.MaxValue
      Edges = edges |> List.map makeEdge
      Path = [] }

let shortestPath (graph: Map<string, Vertex>) (sourceId: string) (destinationId: string) =

    let mutable graph0 =
        graph.Add(sourceId, { graph.[sourceId] with Cost = 0 })

    let mutable pq = PriorityQueue.empty false
    pq <- pq.Insert(graph0.[sourceId])

    let mutable dest = Option<Vertex>.None
    let mutable visited = Map.empty

    while not pq.IsEmpty && dest.IsNone do
        let vertex, newpq = pq.Pop()
        pq <- newpq

        if
            vertex.Cost <> Int32.MaxValue
            && not (visited.ContainsKey(vertex.Id))
        then
            if vertex.Id = destinationId then
                dest <- Some(vertex)

            for edge in vertex.Edges do
                let destinationId = edge.DestinationVertexId

                if not (visited.ContainsKey(destinationId)) then
                    let newDistance = edge.Cost + vertex.Cost
                    let destination = graph.[destinationId]

                    if newDistance < destination.Cost then
                        let newDestination =
                            { destination with
                                  Cost = newDistance
                                  Path = destination.Id :: vertex.Path }

                        pq <- pq.Insert newDestination
                        graph0 <- graph0.Add(destinationId, newDestination)
                    else
                        ()
                else
                    ()

            visited <- visited.Add(vertex.Id, vertex)

    let result =
        match dest with
        | Some dest ->
            { Nodes = dest.Path |> List.rev |> fun l -> sourceId :: l
              Cost = dest.Cost }
            |> Some
        | None -> None

    result
