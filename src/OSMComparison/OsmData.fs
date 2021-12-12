namespace OSM

type Node =
    { id: int64
      lat: float
      lon: float
      tags: Map<string, string> option }

type Way =
    { id: int64
      nodes: int64 []
      tags: Map<string, string> option }

type Member =
    { ``type``: string
      ref: int64
      role: string }

type Relation =
    { id: int64
      members: Member []
      tags: Map<string, string> option }

type Element =
    | Node of Node
    | Way of Way
    | Relation of Relation

type OSMJson = { elements: Element [] }

[<RequireQualifiedAccess>]
module Data =

    let getTags (e: Element) =
        match e with
        | Node v -> v.tags
        | Way v -> v.tags
        | Relation v -> v.tags

    let getTagValue (key: string) (e: Element) =
        match getTags e with
        | Some tags -> tags.TryFind key
        | None -> None

    let hasTagWithValue (key: string) (value: string) (e: Element) =
        match getTagValue key e with
        | Some v -> v = value
        | None -> false

    let existsTag (key: string) (e: Element) =
        match getTags e with
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

    let getRelationOf (e: Element) (elements: Element []) =
        elements
        |> Array.tryFind (fun x ->
            match x with
            | Relation r ->
                r.members
                |> Array.exists (fun m -> m.ref = idOf e)
            | _ -> false)
        |> fun e ->
            match e with
            | Some (Relation r) -> Some r
            | _ -> None

    let getWaysOfId (x: int64) (filter: Way -> bool) (elements: Element []) =
        elements
        |> Array.filter (fun e ->
            match e with
            | Way w -> filter w && w.nodes |> Array.contains x
            | _ -> false)
        |> Array.choose asWay

    let getWaysOfMember (m: Member) (filter: Way -> bool) (elements: Element []) = getWaysOfId m.ref filter elements

    let getWaysOfElement (e: Element) (filter: Way -> bool) (elements: Element []) =
        getWaysOfId (idOf e) filter elements

    let getWayOf (node: Node) (filter: Way -> bool) (elements: Element []) =
        elements
        |> Array.tryFind (fun e ->
            match e with
            | Way w -> filter w && w.nodes |> Array.contains node.id
            | _ -> false)
        |> fun e ->
            match e with
            | Some (Way w) -> Some w
            | _ -> None

    let hasWayOf (node: Node) (filter: Way -> bool) (elements: Element []) =
        getWayOf node filter elements |> Option.isSome

    let getMembers (r: Relation) (``type``: string) (role: string) =
        r.members
        |> Array.filter (fun e -> e.``type`` = ``type`` && e.role = role)

    let getWaysOfMembers (r: Relation) (``type``: string) (role: string) (filter: Way -> bool) (elements: Element []) =
        getMembers r ``type`` role
        |> Array.collect (fun m -> getWaysOfMember m filter elements)

    let getWaysOfRelation (r: Relation) (elements: Element []) =
        getMembers r "way" ""
        |> Array.choose (fun m ->
            elements
            |> Array.tryFind (fun x -> idOf x = m.ref))
        |> Array.choose asWay

    let getAnyNodeOfWay (w: Way) (elements: Element []) =
        match elements
              |> Array.tryFind (fun x -> idOf x = w.nodes.[0])
            with
        | Some (Node n) -> Some n
        | _ -> None

    let getAnyNodeOfWayMapped (w: Way) (elements: Map<int64, Element>) =
        match elements |> Map.tryFind w.nodes.[0] with
        | Some (Node n) -> Some n
        | _ -> None

    let getAnyNodeOfRelation (r: Relation) (role: string) (elements: Element []) =
        let nodeMember =
            r.members
            |> Array.find (fun m -> m.``type`` = "node" && m.role = role)

        match elements
              |> Array.tryFind (fun x -> idOf x = nodeMember.ref)
            with
        | Some (Node n) -> Some n
        | _ -> None

    let toMap (elements: Element []) : Map<int64, Element> =
        elements
        |> Array.fold (fun map e -> map.Add(idOf e, e)) (Map.empty)
