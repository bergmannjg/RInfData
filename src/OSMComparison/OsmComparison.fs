module OSM.Comparison

open System
open EraKG
open OSM
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
        + Math.Sin(dLon / 2.0)
          * Math.Sin(dLon / 2.0)
          * Math.Cos(lat1)
          * Math.Cos(lat2)

    let c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a))

    r * c

let private matchType (op: OperationalPoint) =
    op.Type = "station"
    || op.Type = "passengerStop"
    || op.Type = "rinf/10"
    || op.Type = "rinf/70"

/// from railwayRef to opid
let private matchUOPID (railwayRef: string) (uOPID: string) =
    let fill (s: string) (len: int) =
        if s.Length < len then
            System.String('0', len - s.Length)
        else
            ""

    let toOPID (s: string) =
        if s.Length > 0 && s.Length <= 5 then
            "DE" + (fill s 5) + s.Replace(" ", "0")
        else
            ""

    let _matchUOPID (railwayRef: string) (uOPID: string) =
        toOPID railwayRef = uOPID.Replace(" ", "0")
        || if uOPID.Contains "  "
              && railwayRef.Length = 4
              && railwayRef.Contains " " then
               let railwayRefX = railwayRef.Replace(" ", "  ")
               toOPID railwayRefX = uOPID.Replace(" ", "0") // matches 'TU R' with 'DETU  R'
           else
               false

    railwayRef.Split [| ';' |]
    |> Array.exists (fun s -> _matchUOPID s uOPID)

let private compareByUOPID
    (operationalPoints: OperationalPoint [])
    (osmEntries: Entry [])
    : (OperationalPoint * Entry option) [] =

    operationalPoints
    |> Array.map (fun op ->
        match osmEntries
              |> Array.tryFind (fun entry ->
                  entry.RailwayRef.IsSome
                  && matchUOPID entry.RailwayRef.Value op.UOPID
                  && ``calculate distance`` (op.Latitude, op.Longitude) (entry.Latitude, entry.Longitude) < 4.0)
            with
        | Some entry -> (op, Some entry)
        | None -> (op, None))

let private filterByName
    (operationalPoints: OperationalPoint [])
    (osmEntries: Entry [])
    : (OperationalPoint * Entry) [] =
    let projection (entry: Entry) =
        match entry.RailwayRef, entry.Railway with
        | Some _, _ -> 0
        | _, Some "station" -> 1
        | _, Some "halt" -> 2
        | _ -> 10

    operationalPoints
    |> Array.map (fun op ->
        osmEntries
        |> Array.filter (fun entry ->
            entry.Name = op.Name
            && ``calculate distance`` (op.Latitude, op.Longitude) (entry.Latitude, entry.Longitude) < 1.0
            && entry.Railway.IsSome)
        |> Array.sortBy projection
        |> Array.tryHead
        |> Option.map (fun entry -> (op, entry)))
    |> Array.choose id

let private matchWithOnlineData (op: OperationalPoint) (entry: Entry) =
    let tryMatch (osmjson: OsmJson option) =
        match osmjson with
        | Some osmjson ->
            OSM.Api.ToEntries osmjson
            |> Array.exists (fun e ->
                match e.RailwayRef with
                | Some railwayRef ->
                    if matchUOPID railwayRef op.UOPID then
                        fprintfn stderr $"matchWithOnlineData {entry.Stop} railwayRef equal to UOPID {op.UOPID}"
                        true
                    else
                        false
                | None -> false)
        | _ -> false

    let prefix = "https://www.openstreetmap.org/"

    if entry.OsmType.StartsWith prefix then
        let osmType = entry.OsmType.Substring(prefix.Length)
        let id = entry.Stop.Substring(entry.OsmType.Length + 1)

        async {
            try
                let! osmjson = OSM.Api.loadOsmData osmType id
                return tryMatch osmjson
            with
            | e ->
                fprintfn stderr $"error  {e}"
                return false
        }
        |> Async.RunSynchronously
    else
        false

let compare (allOperationalPoints: OperationalPoint []) (osmEntries: Entry []) =

    allOperationalPoints
    |> Array.groupBy (fun op -> op.Type)
    |> Array.sortBy (fun (k, _) -> k)
    |> Array.iter (fun (k, l) -> fprintfn stderr $"op type {k}, {l.Length} entries")

    let operationalPoints =
        allOperationalPoints
        |> Array.filter (fun op -> matchType op)

    let result = compareByUOPID operationalPoints osmEntries

    let operationalPointsFound =
        result
        |> Array.filter (fun (_, entry) -> entry.IsSome)

    operationalPointsFound
    |> Array.map (fun (op, entry) ->
        match entry with
        | Some entry ->
            let dist =
                ``calculate distance`` (op.Latitude, op.Longitude) (entry.Latitude, entry.Longitude)

            (op.UOPID, dist)
        | None -> ("", 0.0))
    |> Array.groupBy (fun (_, d) -> int (Math.Floor d))
    |> Array.sortBy (fun (k, _) -> k)
    |> Array.iter (fun (k, l) -> printfn $"km distance {k}, count {l.Length}, first entry {l.[0]}")

    let operationalPointsNotFound =
        result
        |> Array.filter (fun (_, entry) -> entry.IsNone)
        |> Array.map (fun (op, _) -> op)

    let countPointsFound = operationalPointsFound.Length

    let countPointsNotFound = operationalPointsNotFound.Length

    operationalPointsFound
    |> Array.groupBy (fun (op, entry) -> entry.Value.OsmType)
    |> Array.iter (fun (k, l) ->
        fprintfn stderr $"osmtype {k}, found {l.Length}"

        l
        |> Array.filter (fun (op, entry) -> entry.Value.Railway.IsSome)
        |> Array.groupBy (fun (op, entry) -> entry.Value.Railway.Value)
        |> Array.iter (fun (k, l) -> fprintfn stderr $"  Railway {k}, found {l.Length}")

        l
        |> Array.filter (fun (op, entry) ->
            entry.Value.Railway.IsNone
            && entry.Value.PublicTransport.IsSome)
        |> Array.groupBy (fun (op, entry) -> entry.Value.PublicTransport.Value)
        |> Array.iter (fun (k, l) -> fprintfn stderr $"  PublicTransport {k}, found {l.Length}"))

    operationalPointsNotFound
    |> Array.groupBy (fun op -> op.Type)
    |> Array.iter (fun (k, l) -> fprintfn stderr $"type {k}, not found {l.Length}")

    fprintfn stderr $"total {operationalPoints.Length}, found {countPointsFound}, not found {countPointsNotFound}"

    operationalPointsNotFound

let analyze (extra: bool) (operationalPointsNotFound: OperationalPoint []) (osmEntries: Entry []) =
    let allOpsFoundByName = filterByName operationalPointsNotFound osmEntries

    if not extra then
        fprintfn stderr $"operationalPoints found by name (with maybe wrong RailwayRef):"

        allOpsFoundByName
        |> Array.iter (fun (op, entry) ->
            if entry.RailwayRef.IsSome then
                fprintfn
                    stderr
                    $"  op: {op.Name}, UOPID: '{op.UOPID}', RailwayRef: '{entry.RailwayRef.Value}', Railway: '{entry.Railway}'")

    let opsFoundByName =
        allOpsFoundByName
        |> Array.filter (fun (op, entry) ->
            entry.RailwayRef.IsNone
            && not (matchWithOnlineData op entry))

    let foundByName = opsFoundByName.Length

    if extra then
        let toString (f: float) = f.ToString().Replace(",", ".")

        let idOfStop (stop: string) =
            let nodPrefix = "https://www.openstreetmap.org/node/"

            if stop.StartsWith nodPrefix then
                stop.Substring(nodPrefix.Length)
            else
                stop

        let fromUOPID (uOPID: string) = uOPID.Substring(2).TrimStart([| '0' |])

        let rinflink =
            "https://linked.ec-dataplatform.eu/describe/?url=http://data.europa.eu/949/functionalInfrastructure/operationalPoints"

        let osmquery = "https://www.openstreetmap.org/search?whereami=1&query"

        fprintfn stderr ""

        opsFoundByName
        |> Array.iter (fun (op, entry) ->
            fprintfn stderr $"{op.Name} {op.Type} UOPID: '{op.UOPID}' Railway: '{entry.Railway}'"
            fprintfn stderr $"  osm node: {entry.Stop}"
            fprintfn stderr $"  rinf op: {rinflink}/{op.UOPID}"
            fprintfn stderr $"  add_railwayref({idOfStop entry.Stop}, 'node', '{fromUOPID op.UOPID}')")

        fprintfn stderr ""

        operationalPointsNotFound
        |> Array.iter (fun op ->
            fprintfn stderr $"{op.Name} {op.Type} UOPID: '{op.UOPID}'"
            fprintfn stderr $"  osm query: {osmquery}={toString (op.Latitude)},{toString (op.Longitude)}"
            fprintfn stderr $"  rinf op: {rinflink}/{op.UOPID}")

        fprintfn stderr ""

    if not extra then
        fprintfn stderr $"operationalPoints found by name (without RailwayRef):"

        opsFoundByName
        |> Array.iter (fun (op, entry) ->
            if entry.RailwayRef.IsNone then
                fprintfn stderr $"  op: {op.Name}, UOPID: '{op.UOPID}', Railway: '{entry.Railway}'")

        fprintfn stderr $"operationalPointsNotFound:"

        operationalPointsNotFound
        |> Array.groupBy (fun op -> op.Type)
        |> Array.iter (fun (k, l) -> fprintfn stderr $"  type {k}, not found {l.Length}")

    fprintfn stderr $"total not found {operationalPointsNotFound.Length}, from that foundByName {foundByName}"
