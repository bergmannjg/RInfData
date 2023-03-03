module OSM.Comparison

open EraKG
open OSM
open OSM.Sparql

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
                  && matchUOPID entry.RailwayRef.Value op.UOPID)
            with
        | Some entry -> (op, Some entry)
        | None -> (op, None))

let private filterByName
    (operationalPoints: OperationalPoint [])
    (osmEntries: Entry [])
    : (OperationalPoint * Entry) [] =
    let projection (entry: Entry) =
        match entry.Railway with
        | Some "station" -> 0
        | Some "halt" -> 1
        | _ -> 10

    operationalPoints
    |> Array.map (fun op ->
        let filtered =
            osmEntries
            |> Array.filter (fun entry -> entry.Name = op.Name && entry.Railway.IsSome)
            |> Array.sortBy projection

        if filtered.Length > 0 then
            let entry = filtered.[0]
            Some(op, entry)
        else
            None)
    |> Array.choose id

let private compareWithOnlineData (op: OperationalPoint) (entry: Entry) =
    let tryMatch (osmjson: OsmJson option) =
        match osmjson with
        | Some osmjson ->
            OSM.Api.ToEntries osmjson
            |> Array.exists (fun e ->
                match e.RailwayRef with
                | Some railwayRef ->
                    if matchUOPID railwayRef op.UOPID then
                        fprintfn stderr $"compareWithOnlineData {entry.Stop} railwayRef equal to UOPID {op.UOPID}"
                        true
                    else
                        fprintfn
                            stderr
                            $"compareWithOnlineData {entry.Stop} railwayRef {railwayRef} distinct from UOPID {op.UOPID}'"

                        true
                | None -> false)
        | _ -> false

    let prefix = "https://www.openstreetmap.org/"

    if entry.OsmType.StartsWith prefix then
        let osmType = entry.OsmType.Substring(prefix.Length)
        let id = entry.Stop.Substring(entry.OsmType.Length + 1)

        async {
            let! osmjson = OSM.Api.loadOsmData osmType id

            return tryMatch osmjson
        }
        |> Async.RunSynchronously
    else
        false

let compare (extra: bool) (allOperationalPoints: OperationalPoint []) (osmEntries: Entry []) =

    allOperationalPoints
    |> Array.groupBy (fun op -> op.Type)
    |> Array.sortBy (fun (k, _) -> k)
    |> Array.iter (fun (k, l) -> fprintfn stderr $"op type {k}, {l.Length} entries")

    let operationalPoints =
        allOperationalPoints
        |> Array.filter (fun op -> matchType op)

    let result = compareByUOPID operationalPoints osmEntries

    let operationalPointsFoundPhase1 =
        result
        |> Array.filter (fun (_, entry) -> entry.IsSome)

    let operationalPointsNotFoundPhase1 =
        result
        |> Array.filter (fun (_, entry) -> entry.IsNone)
        |> Array.map (fun (op, _) -> op)

    let allOpsFoundByName = filterByName operationalPointsNotFoundPhase1 osmEntries

    let operationalPointsFoundPhase2 =
        allOpsFoundByName
        |> Array.filter (fun (op, entry) ->
            if entry.RailwayRef.IsSome then
                fprintfn
                    stderr
                    $"found by name {entry.Name}, RailwayRef '{entry.RailwayRef.Value}' distinct from UOPID '{op.UOPID}'"

                true
            else
                compareWithOnlineData op entry)

    let operationalPointsFound =
        Array.concat [ operationalPointsFoundPhase1
                       operationalPointsFoundPhase2
                       |> Array.map (fun (op, entry) -> (op, Some entry)) ]

    let countPointsFound = operationalPointsFound.Length

    let operationalPointsNotFound =
        operationalPointsNotFoundPhase1
        |> Array.filter (fun op ->
            operationalPointsFoundPhase2
            |> Array.exists (fun (op1, _) -> op.UOPID = op1.UOPID)
            |> not)

    let countPointsNotFound = operationalPointsNotFound.Length

    let opsFoundByName =
        allOpsFoundByName
        |> Array.filter (fun (op, entry) ->
            operationalPointsFoundPhase2
            |> Array.exists (fun (op1, _) -> op.UOPID = op1.UOPID)
            |> not)

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

    fprintfn
        stderr
        $"total {operationalPoints.Length}, found {countPointsFound}, not found {countPointsNotFound}, from that foundByName {foundByName}"
