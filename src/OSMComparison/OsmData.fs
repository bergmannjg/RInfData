namespace OSM

type Node =
    { ``type``: string option
      id: int64
      lat: float
      lon: float
      tags: Map<string, string> option }

type Way =
    { ``type``: string option
      id: int64
      nodes: int64 []
      tags: Map<string, string> option }

type Member =
    { ``type``: string
      ref: int64
      role: string }

type Relation =
    { ``type``: string option
      id: int64
      members: Member []
      tags: Map<string, string> option }

type Element =
    | Node of Node
    | Way of Way
    | Relation of Relation

type OSMJson = { elements: Element [] }

[<RequireQualifiedAccess>]
module Data =

    let getTagValue (key: string) (tags: Map<string, string> option) =
        match tags with
        | Some tags -> tags.TryFind key
        | None -> None

    let hasTagWithValue (key: string) (value: string) (tags: Map<string, string> option) =
        match getTagValue key tags with
        | Some v -> v = value
        | None -> false

    let existsTag (key: string) (tags: Map<string, string> option) =
        match tags with
        | Some tags -> tags.ContainsKey key
        | None -> false

    let idOf (element: Element) =
        match element with
        | Node v -> v.id
        | Way v -> v.id
        | Relation v -> v.id

    let asNode (element: Element) =
        match element with
        | Node v -> Some v
        | _ -> None

    let asWay (element: Element) =
        match element with
        | Way v -> Some v
        | _ -> None

    let asRelation (element: Element) =
        match element with
        | Relation v -> Some v
        | _ -> None

    let tryGetElement (id: int64) (elements: Element []) =
        elements |> Array.tryFind (fun e -> idOf e = id)

    let nodeOf (id: int64) (elements: Element []) =
        match tryGetElement id elements with
        | Some e -> asNode e
        | None -> None

    let wayOf (id: int64) (elements: Element []) =
        match tryGetElement id elements with
        | Some e -> asWay e
        | None -> None

    let getRelationOfMemberId (id: int64) (elements: Element []) =
        elements
        |> Array.tryFind (fun e ->
            match e with
            | Relation r -> r.members |> Array.exists (fun m -> m.ref = id)
            | _ -> false)
        |> fun e ->
            match e with
            | Some (Relation r) -> Some r
            | _ -> None

    let getWaysOfNodeId (nodeId: int64) (filter: Way -> bool) (elements: Element []) =
        elements
        |> Array.filter (fun e ->
            match e with
            | Way w ->
                filter w
                && w.nodes |> Array.exists (fun n -> n = nodeId)
            | _ -> false)
        |> Array.map (fun e ->
            match e with
            | Way w -> Some w.id
            | _ -> None)
        |> Array.choose id

    let getWayOfNodeId (id: int64) (filter: Way -> bool) (elements: Element []) =
        elements
        |> Array.tryFind (fun e ->
            match e with
            | Way w ->
                filter w
                && w.nodes |> Array.exists (fun n -> n = id)
            | _ -> false)
        |> fun e ->
            match e with
            | Some (Way w) -> Some w
            | _ -> None

    let hasWayOfId (id: int64) (filter: Way -> bool) (elements: Element []) =
        getWayOfNodeId id filter elements |> Option.isSome

    let getMembersOfRelation (id: int64) (``type``: string) (role: string) (elements: Element []) =
        match tryGetElement id elements with
        | Some (Relation r) ->
            r.members
            |> Array.filter (fun e -> e.``type`` = ``type`` && e.role = role)
        | _ -> [||]
