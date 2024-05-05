open System
open System.IO
open System.Text.Json

open RInfGraph
open Sparql
open EraKG

let printHelp () =
    let dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "../data/"

    $"""
USAGE: EraKGLoader

OPTIONS:
    --Countries 
        load all countries

    --Build <dataDir> <countries>
        load OperationalPoints and SectionsOfLines of <countries> into <dataDir> and build graph of OperationalPoints and SectionsOfLines

        <dataDir>: directory
        <countries>: list of countries separated by semicolon

    <countries>
        load OperationalPoints and SectionsOfLines of <countries> into <dataDir> and build graph of OperationalPoints and SectionsOfLines

        <dataDir>: {dataDirectory}
        <countries>: list of countries separated by semicolon
"""

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let mutable cacheEnabled = true

let loadDataCached<'a> path name (loader: unit -> Async<string>) =
    async {
        let file = path + name

        if not cacheEnabled || not (File.Exists file) then
            let! result = loader ()
            fprintfn stderr $"{name}, {result.Length} bytes"
            File.WriteAllText(file, result)
            return readFile<'a> path name

        else
            return readFile<'a> path name
    }

let kilometerOfLine (op: OperationalPoint) (line: string) =
    op.RailwayLocations
    |> Array.tryFind (fun loc -> loc.NationalIdentNum = line)
    |> Option.map (fun loc -> loc.Kilometer)
    |> Option.defaultValue 0.0

let findOp ops opId =
    ops |> Array.find (fun op -> op.UOPID = opId)

let findOpByOPID (ops: OperationalPoint[]) opId =
    ops |> Array.tryFind (fun (op: OperationalPoint) -> op.UOPID = opId)

let getLineInfo
    (name: string)
    ops
    (tunnels: TunnelInfo[])
    (osmRoutes: OSM.Sparql.Entry[])
    (country: string)
    line
    (nodesOfLine: GraphNode list)
    =

    let firstOp = findOpByOPID ops (nodesOfLine |> List.head).Node

    let lastOp = findOpByOPID ops (nodesOfLine |> List.last).Edges.[0].Node

    match firstOp, lastOp with
    | Some firstOp, Some lastOp ->
        let uOPIDs =
            nodesOfLine
            |> List.rev
            |> List.fold
                (fun (s: string list) sol ->
                    match findOpByOPID ops sol.Node with
                    | Some op -> op.UOPID :: s
                    | None -> s)
                [ lastOp.UOPID ]
            |> List.toArray

        let tunnelsOfLine =
            tunnels
            |> Array.filter (fun t -> t.Line = line)
            |> Array.sortBy (fun t -> t.StartKm)
            |> Array.map (fun t -> t.Tunnel)
            |> Array.distinct

        let countryPrefixes = [| "DEU", "de:"; "FRA", "fr:" |]

        // i.e. "574000-1" == "574 000" should match
        let matchLine (rinfLine: string) (osmLine: string) =
            rinfLine = osmLine
            || country = "FRA"
               && rinfLine.EndsWith "-1"
               && osmLine.Length = 7
               && rinfLine.Substring(0, 3) = osmLine.Substring(0, 3)

        let wikipedia =
            osmRoutes
            |> Array.tryFind (fun r ->
                matchLine line r.Ref
                && countryPrefixes
                   |> Array.exists (fun (c, p) -> c = country && r.Wikipedia.StartsWith p))
            |> Option.map (fun r -> r.Wikipedia)

        Some
            { Line = line
              Country = country
              Name = name
              Length =
                nodesOfLine
                |> List.sumBy (fun sol -> if sol.Edges.Length > 0 then sol.Edges.[0].Length else 0.0)
                |> sprintf "%.1f"
                |> Double.Parse
              StartKm = kilometerOfLine firstOp line
              EndKm = kilometerOfLine lastOp line
              UOPIDs = uOPIDs
              Tunnels = tunnelsOfLine
              Wikipedia = wikipedia }
    | _ -> None

let buildLineInfo
    (ops: OperationalPoint[])
    (nodes: GraphNode[])
    (tunnels: TunnelInfo[])
    (osmRoutes: OSM.Sparql.Entry[])
    (country: string, line: string)
    : LineInfo[] =
    let solsOfLine =
        nodes
        |> Array.filter (fun sol ->
            sol.Edges
            |> Array.exists (fun e -> e.Line = line && e.Country = country && e.StartKm < e.EndKm))
        |> Array.map (fun sol ->
            { sol with
                Edges =
                    sol.Edges
                    |> Array.filter (fun e -> e.Line = line && e.Country = country && e.StartKm < e.EndKm) })
        |> Array.sortBy (fun n -> n.Edges.[0].StartKm)

    let getFirstNodes (solsOfLine: GraphNode[]) =
        solsOfLine
        |> Array.filter (fun solX -> not (solsOfLine |> Array.exists (fun solY -> solX.Node = solY.Edges.[0].Node)))

    let firstNodes = getFirstNodes solsOfLine

    let rec getNextNodes (solsOfLine: GraphNode[]) (startSol: GraphNode) (nextNodes: GraphNode list) : GraphNode list =
        let nextNode =
            solsOfLine |> Array.tryFind (fun sol -> sol.Node = startSol.Edges.[0].Node)

        match nextNode with
        | Some nextNode -> getNextNodes solsOfLine nextNode (nextNode :: nextNodes)
        | None -> nextNodes |> List.rev

    let nextNodesLists =
        firstNodes
        |> Array.map (fun firstNode -> getNextNodes solsOfLine firstNode [ firstNode ])

    if nextNodesLists.Length > 0 then
        let firstOp = findOpByOPID ops nextNodesLists.[0].Head.Node

        let lastElem = nextNodesLists.[nextNodesLists.Length - 1] |> List.toArray

        let lastOp = findOpByOPID ops lastElem.[lastElem.Length - 1].Edges.[0].Node

        match firstOp, lastOp with
        | Some firstOp, Some lastOp ->
            nextNodesLists
            |> Array.map (getLineInfo (firstOp.Name + " - " + lastOp.Name) ops tunnels osmRoutes country line)
            |> Array.choose id
        | _ -> [||]
    else
        [||]

let reduceLineInfos (lineinfos: LineInfo[]) : LineInfo[] =
    let folder =
        fun (s: LineInfo[]) ((k, g): string * LineInfo[]) ->
            match
                g
                |> Array.tryFind (fun a ->
                    g
                    |> Array.forall (fun b -> a.StartKm < a.EndKm && a.StartKm <= b.StartKm && b.EndKm <= a.EndKm))
            with
            | Some span -> Array.concat [ [| span |]; s ]
            | None -> Array.concat [ g; s ]

    lineinfos |> Array.groupBy (fun li -> li.Line) |> Array.fold folder [||]

let buildLineInfos
    (ops: OperationalPoint[])
    (nodes: GraphNode[])
    (tunnels: TunnelInfo[])
    (osmRoutes: OSM.Sparql.Entry[])
    : LineInfo[] =
    nodes
    |> Array.collect (fun sol -> sol.Edges |> Array.map (fun e -> (e.Country, e.Line)))
    |> Array.distinct
    |> Array.collect (buildLineInfo ops nodes tunnels osmRoutes)
    |> reduceLineInfos
    |> Array.sortBy (fun line -> line.Line)

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

let estimateKilometres
    (startOp: string)
    (endOp: string)
    (ops: OperationalPoint[])
    (nodes: GraphNode[])
    (tunnel: Tunnel)
    =
    match nodes |> Array.tryFind (fun node -> node.Node = startOp) with
    | Some node ->
        match node.Edges |> Array.tryFind (fun e -> e.Node = endOp) with
        | Some edge ->
            match ops |> Array.tryFind (fun op -> op.UOPID = startOp) with
            | Some op ->
                let startKm =
                    edge.StartKm
                    + ``calculate distance`` (op.Latitude, op.Longitude) (tunnel.StartLatitude, tunnel.StartLongitude)

                let endKm = startKm + tunnel.Length / 1000.0
                Some(System.Math.Round(startKm, 3)), Some(System.Math.Round(endKm, 3))
            | None -> None, None
        | None -> None, None
    | None -> None, None

let buildTunnelInfos
    (sols: SectionOfLine[])
    (ops: OperationalPoint[])
    (nodes: GraphNode[])
    (tunnels: Tunnel[])
    : TunnelInfo[] =
    tunnels
    |> Array.choose (fun t ->
        match
            sols
            |> Array.tryFind (fun sol ->
                sol.Tracks
                |> Array.exists (fun track -> t.ContainingTracks |> Array.exists (fun t -> track.id = t)))
        with
        | Some sol ->
            let startKm, endKm = estimateKilometres sol.StartOP sol.EndOP ops nodes t

            Some
                { Tunnel = t.Name
                  Length = t.Length / 1000.0
                  StartLong = t.StartLongitude
                  StartLat = t.StartLatitude
                  StartKm = startKm
                  StartOP = sol.StartOP
                  EndLong = t.EndLongitude
                  EndLat = t.EndLatitude
                  EndKm = endKm
                  EndOP = sol.EndOP
                  SingelTrack = t.ContainingTracks.Length = 1
                  Line = sol.LineIdentification }
        | None ->
            fprintfn stderr "sol not found for trackid: %s" t.Name
            None)
    |> Array.distinct

let getMaxSpeed (sol: SectionOfLine) (defaultValue: int) =
    sol.Tracks
    |> Array.choose (fun t -> t.maximumPermittedSpeed)
    |> Array.sortDescending
    |> Array.tryHead
    |> Option.defaultValue defaultValue

let isElectrified (sol: SectionOfLine) =
    sol.Tracks
    |> Array.choose (fun t -> t.contactLineSystem)
    |> Array.forall (fun s -> not (s.Contains "not electrified"))

let nullDefaultValue<'a when 'a: null> (defaultValue: 'a) (value: 'a) =
    if isNull value then defaultValue else value

let scale (maxSpeed: int) = 1.0

let travelTime (length: float) (maxSpeed: int) =
    length / (float (maxSpeed) * (scale maxSpeed))

let getCost (length: float) (maxSpeed: int) =
    let cost = int (10000.0 * (travelTime length maxSpeed))

    if (cost <= 0) then 1 else cost

let buildGraph (ops: OperationalPoint[]) (sols: SectionOfLine[]) =

    let mutable graph = Map.empty

    let addOp (op: OperationalPoint) =
        if not (graph.ContainsKey op.UOPID) then
            graph <- graph.Add(op.UOPID, List.empty)

    // opId in SectionOfLine sometime contains validity dates
    // ex: "DE/EU00059/2024-01-01_2024-12-31" or "DEASBRS/2024-01-01_2024-12-31"
    // normalize to opId
    let normalizeOpId (opId: string) =
        let splits = opId.Split "/"

        if splits.Length > 1 then
            if splits.[1].StartsWith "18" || splits.[1].StartsWith "20" then // century
                splits.[0]
            else
                splits.[1]
        else
            opId

    let findOp (opId: string) =
        let opIdNormalized = normalizeOpId opId
        ops |> Array.tryFind (fun op -> op.UOPID = opIdNormalized)

    // the field sol.Length is for some lines not correct
    // use the length data of OperationalPoints
    let length (sol: SectionOfLine) =
        if true then // sol.Length = 0
            let findRailwayLocation =
                fun op ->
                    op.RailwayLocations
                    |> Array.tryFind (fun rl -> rl.NationalIdentNum = sol.LineIdentification)

            let startLoc =
                ops
                |> Array.find (fun op -> op.UOPID = normalizeOpId sol.StartOP)
                |> findRailwayLocation

            let endLoc =
                ops
                |> Array.find (fun op -> op.UOPID = normalizeOpId sol.EndOP)
                |> findRailwayLocation

            match startLoc, endLoc with
            | Some startLoc, Some endLoc -> Math.Abs(startLoc.Kilometer - endLoc.Kilometer)
            | _, _ ->
                fprintfn stderr $"sol startop endop not found, {sol.Name}"
                0.0

        else
            sol.Length / 1000.0

    let addSol (sol: SectionOfLine) =
        match findOp sol.StartOP, findOp sol.EndOP with
        | Some(opStart), Some(opEnd) ->
            let maxSpeed = getMaxSpeed sol 100
            let cost = getCost (length sol) maxSpeed

            graph <-
                graph.Add(
                    opStart.UOPID,
                    { Node = opEnd.UOPID
                      Cost = cost
                      Line = sol.LineIdentification
                      Country = sol.Country
                      MaxSpeed = maxSpeed
                      Electrified = isElectrified sol
                      StartKm = kilometerOfLine opStart sol.LineIdentification
                      EndKm = kilometerOfLine opEnd sol.LineIdentification
                      Length = length sol }
                    :: graph.[opStart.UOPID]
                )

            graph <-
                graph.Add(
                    opEnd.UOPID,
                    { Node = opStart.UOPID
                      Cost = cost
                      Line = sol.LineIdentification
                      Country = sol.Country
                      MaxSpeed = maxSpeed
                      Electrified = isElectrified sol
                      StartKm = kilometerOfLine opEnd sol.LineIdentification
                      EndKm = kilometerOfLine opStart sol.LineIdentification
                      Length = length sol }
                    :: graph.[opEnd.UOPID]
                )
        | _ -> fprintfn stderr "addSol, not found %A or %A" sol.StartOP sol.EndOP

    ops |> Array.iter addOp

    sols |> Array.iter addSol

    fprintfn stderr "Nodes: %i, Edges: %i" graph.Count (graph |> Seq.sumBy (fun item -> item.Value.Length))

    graph
    |> Seq.map (fun kv ->
        { Node = kv.Key
          Edges = graph.[kv.Key] |> List.toArray })
    |> Seq.toArray

let chooseItem (item: Item) =
    if item.id.StartsWith EraKG.Api.prefixTrack then
        [ EraKG.Api.propLabel
          EraKG.Api.propMaximumPermittedSpeed
          EraKG.Api.propContactLineSystem
          EraKG.Api.propLoadCapability
          EraKG.Api.propTenClassification ]
        |> List.map (fun prop ->
            item.properties
            |> Map.tryPick (fun k v -> if k = prop then Some(k, v) else None))
        |> List.choose id
        |> fun properties ->
            if properties.IsEmpty then
                None
            else
                Some
                    { id = item.id
                      properties = Map<string, obj[]>(properties) }
    else
        None

let checkIsDir (path: string) = Directory.Exists path

let optCreateIsDir (path: string) =
    if not (checkIsDir path) then
        Directory.CreateDirectory path |> ignore

let allowedCountries =
    "EST;GRC;DEU;NLD;BEL;LUX;FRA;POL;LVA;HRV;HUN;FIN;SVN;ESP;BGR;AUT;ITA;NOR;SWE;DNK;ROU;SVK;CZE;CHE;GBR;PRT"

let testIsCountry (countryArg: string) : bool =
    let allCountries = allowedCountries.Split Api.countrySplitChars

    countryArg.Split Api.countrySplitChars
    |> Array.forall (fun country -> allCountries |> Array.contains country)

let checkIsCountry (path: string) (countryArg: string) : Async<string[]> =
    async {

        let! result = loadDataCached path "sparql-countries.json" (fun () -> Api.loadCountriesData ())

        let allCountries = Api.toCountries result

        let countries = countryArg.Split Api.countrySplitChars

        let isCountry =
            countries
            |> Array.forall (fun country -> allCountries |> Array.contains country)

        if not isCountry then
            raise (System.ArgumentException($"unkown country {countryArg}, should be one of '{allowedCountries}'"))

        return countries
    }

let getCountries () : Async<string> =
    async {

        let! result = Api.loadCountriesData ()

        let allCountries =
            Api.toCountries (JsonSerializer.Deserialize<QueryResults>(result))

        return String.concat ";" allCountries
    }

let getOsmRoutesFrom (loader: string -> Async<string>) : Async<OSM.Sparql.Entry[]> =
    async {

        try
            let! result = loader EraKG.Request.applicationSparqlResults

            return OSM.Sparql.Api.fromQueryResults (JsonSerializer.Deserialize<QueryResults>(result))
        with e ->
            fprintfn stderr "getOsmRoutesFrom, error: %s" e.Message
            return Array.empty
    }

let getOsmRoutes () : Async<string> =
    async {
        let! entries1 = getOsmRoutesFrom OSM.Sparql.Api.loadWikipediaArticles
        let! entries2 = getOsmRoutesFrom OSM.Sparql.Api.loadWikidataArticles

        return JsonSerializer.Serialize(Array.append entries1 entries2)
    }

let execGraphBuild (path: string) : Async<string> =
    async {
        let ops = readFile<OperationalPoint[]> path "OperationalPoints.json"

        let sols = readFile<SectionOfLine[]> path "SectionsOfLines.json"

        let g = buildGraph ops sols

        return JsonSerializer.Serialize g
    }

let execOpInfohBuild (path: string) : Async<string> =
    async {
        let ops =
            readFile<OperationalPoint[]> path "OperationalPoints.json"
            |> Array.map (fun op ->
                { UOPID = op.UOPID
                  Name = op.Name
                  RinfType = System.Int32.Parse(op.Type.Substring 5)
                  Latitude = op.Latitude
                  Longitude = op.Longitude })

        return JsonSerializer.Serialize ops
    }

let execLineInfoBuild (path: string) : Async<string> =
    async {
        let ops = readFile<OperationalPoint[]> path "OperationalPoints.json"

        let nodes = readFile<GraphNode[]> path "Graph.json"

        let tunnels = readFile<TunnelInfo[]> path "TunnelInfos.json"

        let osmRoutes = readFile<OSM.Sparql.Entry[]> path "OsmRoutes.json"

        let lineInfos = buildLineInfos ops nodes tunnels osmRoutes

        return JsonSerializer.Serialize lineInfos
    }

let execTunnelInfoBuild (path: string) : Async<string> =
    async {
        let ops = readFile<OperationalPoint[]> path "OperationalPoints.json"

        let nodes = readFile<GraphNode[]> path "Graph.json"

        let sols = readFile<SectionOfLine[]> path "SectionsOfLines.json"

        let tunnels = readFile<Tunnel[]> path "Tunnels.json"

        let tunnelInfos = buildTunnelInfos sols ops nodes tunnels

        return JsonSerializer.Serialize tunnelInfos
    }

let execOperationalPointsBuild (path: string) (countriesArg: string) : Async<string> =
    async {
        let! countries = checkIsCountry path countriesArg

        let filename = "sparql-operationalPoints.json"

        let! result = loadDataCached path filename (fun () -> Api.loadOperationalPointData countriesArg)

        let kgOps = Api.toOperationalPoints result countries

        fprintfn stderr $"kg ops: {kgOps.Length}"

        return JsonSerializer.Serialize kgOps
    }

let execRailwayLineBuild (path: string) (countriesArg: string) : Async<string> =
    async {
        let! _ = checkIsCountry path countriesArg

        let! result =
            loadDataCached path "sparql-railwayline.json" (fun () -> Api.loadNationalRailwayLineData countriesArg)

        let railwaylines = EraKG.Api.toRailwayLines result
        fprintfn stderr $"kg railwaylines: {railwaylines.Length}"

        return JsonSerializer.Serialize railwaylines
    }

let execTunnelBuild (path: string) (countriesArg: string) : Async<string> =
    async {
        let! _ = checkIsCountry path countriesArg

        let! result = loadDataCached path "sparql-tunnel.json" (fun () -> Api.loadTunnelData countriesArg)

        let tunnels =
            EraKG.Api.toTunnels result
            |> fun tunnels -> // filter double entries
                tunnels
                |> Array.filter (fun tunnel ->
                    tunnels
                    |> Array.filter (fun t ->
                        t.Length = tunnel.Length
                        && ``calculate distance``
                            (t.StartLatitude, t.StartLongitude)
                            (tunnel.StartLatitude, tunnel.StartLongitude) < 0.05)
                    |> Array.sortBy (fun t -> t.Name.Length)
                    |> fun filtered -> tunnel.Name = filtered.[0].Name)

        fprintfn stderr $"kg tunnels: {tunnels.Length}"

        return JsonSerializer.Serialize tunnels
    }

// adhoc, add missing sols
let missingSols: SectionOfLine[] =
    [| { Name = "3600_DE0FSUE_DE00FFD"
         Country = "DEU"
         Length = 11.0
         LineIdentification = "3600"
         IMCode = "0080"
         StartOP = "DE0FSUE"
         EndOP = "DE00FFD"
         Tracks = [||] } |]

let execSectionsOfLineBuild (path: string) (countriesArg: string) : Async<string> =
    async {
        let! _ = checkIsCountry path countriesArg

        let filename = "sparql-sectionsOfLine.json"

        let! result = loadDataCached path filename (fun () -> Api.loadSectionOfLineData countriesArg)

        let tracks = readFile<Microdata> path "sparql-tracks.json"
        fprintfn stderr $"kg tracks: {tracks.items.Length}"

        let railwaylines = readFile<RailwayLine[]> path "Railwaylines.json"
        fprintfn stderr $"kg railwaylines: {railwaylines.Length}"

        let sols = EraKG.Api.toSectionsOfLine result railwaylines tracks

        let missingSols =
            missingSols
            |> Array.filter (fun msol ->
                sols
                |> Array.exists (fun sol -> sol.StartOP = msol.StartOP && sol.EndOP = msol.EndOP)
                |> not)

        let missingSols =
            missingSols |> Array.filter (fun sol -> countriesArg.Contains sol.Country)

        if missingSols.Length > 0 then
            missingSols
            |> Array.iter (fun sol ->
                fprintfn stderr $"add missing sol: {sol.LineIdentification} {sol.StartOP} {sol.EndOP}")

        let sols = Array.concat [ sols; missingSols ]

        let! sols = EraKG.Api.remapTracksOfLines sols

        fprintfn stderr $"kg sols: {sols.Length}"

        return JsonSerializer.Serialize sols
    }

let execTracksOfLinesLoad (path: string) (linePrefix: string) (country: string) : Async<string> =
    async {
        let lineInfos =
            readFile<LineInfo[]> path "LineInfos.json"
            |> Array.filter (fun line -> line.Line.StartsWith(linePrefix) && line.Country = country)
            |> Array.map (fun line -> line.Line)

        fprintfn stderr $"lineInfos {lineInfos.Length}"

        let! data = EraKG.Api.loadTracksOfLines lineInfos country

        return JsonSerializer.Serialize data
    }

let execTrackLoad (path: string) (trackId: string) : Async<string> =
    async {
        let tracks =
            readFile<Microdata> path "sparql-tracks.json"
            |> EraKG.Api.toTracks
            |> Array.filter (fun track -> track.id.EndsWith(trackId))

        fprintfn stderr $"tracks {tracks.Length}"

        let! data =
            if tracks.Length > 0 then
                async {
                    let! track = EraKG.Api.reloadTrack tracks[0]
                    return Some track
                }
            else
                async { return None }

        return JsonSerializer.Serialize data
    }


let execTracksBuild (path: string) (countriesArg: string) : Async<string> =
    let operations =
        [ 0..9 ]
        |> List.map (fun n ->
            loadDataCached<Microdata> path $"sparql-tracks-{n}.json" (fun () -> Api.loadTrackData countriesArg n))

    async {
        let! _ = checkIsCountry path countriesArg

        let! _ =
            loadDataCached<Microdata> path $"sparql-tracks.json" (fun () ->
                async {
                    let items =
                        operations
                        |> Async.Sequential
                        |> Async.RunSynchronously
                        |> Array.collect (fun result -> result.items |> Array.choose chooseItem)

                    return JsonSerializer.Serialize({ items = items })
                })

        return ""
    }

let execBuild (path: string) (countriesArg: string) (useCache: bool) : Async<string> =
    async {
        cacheEnabled <- useCache
        let! _ = execTracksBuild path countriesArg

        let! result = execRailwayLineBuild path countriesArg
        File.WriteAllText(path + "Railwaylines.json", result)

        let! result = execSectionsOfLineBuild path countriesArg
        File.WriteAllText(path + "SectionsOfLines.json", result)

        let! result = execOperationalPointsBuild path countriesArg
        File.WriteAllText(path + "OperationalPoints.json", result)

        let! result = execTunnelBuild path countriesArg
        File.WriteAllText(path + "Tunnels.json", result)

        let! result = execOpInfohBuild path
        File.WriteAllText(path + "OpInfos.json", result)

        let! result = execGraphBuild path
        File.WriteAllText(path + "Graph.json", result)

        let! result = execTunnelInfoBuild path
        File.WriteAllText(path + "TunnelInfos.json", result)

        let! result = getOsmRoutes ()
        File.WriteAllText(path + "OsmRoutes.json", result)

        let! result = execLineInfoBuild path
        File.WriteAllText(path + "LineInfos.json", result)

        return ""
    }

[<EntryPoint>]
let main argv =
    try
        let dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "../data/"

        let useCache = argv |> Array.exists (fun arg -> arg = "--useCache")

        if argv.Length = 0 then
            async { return printHelp () }
        else if checkIsDir dataDirectory && testIsCountry argv.[0] then
            async { return! execBuild dataDirectory argv.[0] useCache }
        else if argv.[0] = "--Countries" then
            async { return! getCountries () }
        else if argv.[0] = "--OsmRoutes" then
            async { return! getOsmRoutes () }
        else if argv.[0] = "--Build" && checkIsDir argv.[1] && argv.Length >= 3 then
            async { return! execBuild argv.[1] argv.[2] useCache }
        else if argv.[0] = "--Graph.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execGraphBuild argv.[1] }
        else if argv.[0] = "--OpInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execOpInfohBuild argv.[1] }
        else if argv.[0] = "--LineInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execLineInfoBuild argv.[1] }
        else if argv.[0] = "--TunnelInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execTunnelInfoBuild argv.[1] }
        else if argv.[0] = "--OperationalPoints" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execOperationalPointsBuild argv.[1] argv.[2] }
        else if argv.[0] = "--RailwayLine" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execRailwayLineBuild argv.[1] argv.[2] }
        else if argv.[0] = "--Tunnel" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execTunnelBuild argv.[1] argv.[2] }
        else if argv.[0] = "--SectionsOfLine" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execSectionsOfLineBuild argv.[1] argv.[2] }
        else if argv.[0] = "--TracksOfLinesLoad" && argv.Length > 3 && checkIsDir argv.[1] then
            async { return! execTracksOfLinesLoad argv.[1] argv.[2] argv.[3] }
        else if argv.[0] = "--TrackLoad" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execTrackLoad argv.[1] argv.[2] }
        else if argv.[0] = "--Tracks" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execTracksBuild argv.[1] argv.[2] }
        else
            async {
                fprintfn stderr $"{argv.[0]} unexpected"
                return printHelp ()
            }
        |> Async.RunSynchronously
        |> fprintfn stdout "%s"

    with e ->
        fprintfn stderr "error: %s %s" e.Message e.StackTrace

    0
