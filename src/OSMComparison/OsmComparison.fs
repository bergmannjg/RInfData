module OSM.Comparison

open System
open EraKG
open OSM.Sparql

// see http://www.fssnip.net/7P8/title/Calculate-distance-between-two-GPS-latitudelongitude-points
let ``calculate distance`` (p1Latitude, p1Longitude) (p2Latitude, p2Longitude) =
    let r = 6371.0 // km

    let dLat = (p2Latitude - p1Latitude) * Math.PI / 180.0

    let dLon = (p2Longitude - p1Longitude) * Math.PI / 180.0

    let lat1 = p1Latitude * Math.PI / 180.0
    let lat2 = p2Latitude * Math.PI / 180.0

    let a =
        Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
        + Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(lat1) * Math.Cos(lat2)

    let c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a))

    r * c

/// match OSM railwayRef with RInf opid, i.e. 'BL' with 'DE000BL' or 'KB G' with 'DEKB  G' 
let private matchesRailwayRefWithUOPID (railwayRef: string) (uOPID: string) =
    let fill (s: string) (len: int) (c: Char) =
        if s.Length < len then String(c, len - s.Length) else ""

    let expandSpaces (s: string) =
        if s.Length = 4 then s.Replace(" ", "  ") else s

    let toOPID (s: string) = "DE" + fill s 5 '0' + s

    railwayRef.Split [| ';' |]
    |> Array.exists (fun s -> s |> expandSpaces |> toOPID = uOPID)

let private findRInfOsmMatchings
    (operationalPoints: RInfGraph.OpInfo[])
    (osmEntries: Entry[])
    : (RInfGraph.OpInfo * Entry option)[] =

    operationalPoints
    |> Array.map (fun op ->
        match
            osmEntries
            |> Array.tryFind (fun entry ->
                matchesRailwayRefWithUOPID entry.RailwayRef op.UOPID
                && ``calculate distance`` (op.Latitude, op.Longitude) (entry.Latitude, entry.Longitude) < 4.0)
        with
        | Some entry -> (op, Some entry)
        | None -> (op, None))

type Matching =
    { UOPID: string; OsmUrl: string option }

let findMatchings (operationalPoints: RInfGraph.OpInfo[]) (osmEntries: Entry[]) (verbose: bool) : Matching[] =

    if verbose then
        operationalPoints
        |> Array.groupBy (fun op -> op.RinfType)
        |> Array.sortBy (fun (k, _) -> k)
        |> Array.iter (fun (k, l) -> fprintfn stderr $"op type {k}, {l.Length} entries")

    let result = findRInfOsmMatchings operationalPoints osmEntries

    if verbose then
        result
        |> Array.filter (fun (_, entry) -> entry.IsSome)
        |> Array.groupBy (fun (op, entry) -> entry.Value.OsmType)
        |> Array.iter (fun (k, l) ->
            fprintfn stderr $"osmtype {k}, found {l.Length}"

            l
            |> Array.groupBy (fun (op, entry) -> entry.Value.Railway)
            |> Array.iter (fun (k, l) -> fprintfn stderr $"  Railway {k}, found {l.Length}"))

        result
        |> Array.choose (fun (op, entry) -> if entry.IsNone then Some op else None)
        |> Array.groupBy (fun op -> op.RinfType)
        |> Array.iter (fun (k, l) -> fprintfn stderr $"type {k}, not found {l.Length}")

    result
    |> Array.map (fun (op, entry) ->
        { UOPID = op.UOPID
          OsmUrl = Option.map (fun entry -> entry.Url) entry })
