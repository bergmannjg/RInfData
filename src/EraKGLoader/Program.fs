open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

open RInfGraph
open Sparql
open EraKG

type Metadata =
    { Endpoint: string
      Ontology: string
      Revision: string
      Program: string
      Countries: string[]
      Date: DateTime }

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

let loadDataCached<'a> path name (loader: unit -> Async<'a>) =
    async {
        let file = path + name

        fprintfn stderr $"loading {name}"

        if not cacheEnabled || not (File.Exists file) then
            let! result = loader ()
            File.WriteAllText(file, JsonSerializer.Serialize(result))
            return readFile<'a> path name

        else
            return readFile<'a> path name
    }

let private loadPaged<'T>
    (limit: int)
    (loader: int -> int -> Async<'T>)
    (fold: 'T[] -> 'T)
    (length: 'T -> int)
    : Async<'T> =
    async {
        let maxLoop = 20
        let mutable offset = 0
        let mutable arr = Array.empty

        while 0 <= offset do
            let! data = loader limit offset
            fprintfn stderr $"loadPaged {typeof<'T>.Name}, limit {limit}, offset {offset}, length {length data}"
            arr <- Array.concat [ arr; [| data |] ]

            offset <-
                if offset < maxLoop * limit && limit = length data then
                    limit + offset
                else
                    -1

        return fold arr
    }

let loadQueryResultsPaged (loader: (int -> int -> Async<QueryResults>)) : Async<QueryResults> =
    loadPaged 10000 loader QueryResults.fold QueryResults.length

let kilometerOfLine (op: OperationalPoint) (line: string) =
    op.RailwayLocations
    |> Array.tryFind (fun loc -> loc.NationalIdentNum = line)
    |> Option.map (fun loc -> loc.Kilometer)
    |> Option.defaultValue 0.0

let findOp ops opId =
    ops |> Array.find (fun op -> op.UOPID = opId)

let findOpByOPID (ops: Collections.Generic.Dictionary<string, OperationalPoint>) opId =
    match ops.TryGetValue opId with
    | true, op -> Some op
    | _ -> None

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
            |> Array.filter (fun t -> t.Line = line && t.Country = country)
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
                |> fun d -> Math.Truncate(d * 10.0) / 10.0
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
    let dictOps: Collections.Generic.Dictionary<string, OperationalPoint> =
        ops
        |> Array.fold
            (fun acc op ->
                acc.Add(op.UOPID, op)
                acc)
            (Collections.Generic.Dictionary ops.Length)

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
        let firstOp = findOpByOPID dictOps nextNodesLists.[0].Head.Node

        let lastElem = nextNodesLists.[nextNodesLists.Length - 1] |> List.toArray

        let lastOp = findOpByOPID dictOps lastElem.[lastElem.Length - 1].Edges.[0].Node

        match firstOp, lastOp with
        | Some firstOp, Some lastOp ->
            nextNodesLists
            |> Array.map (getLineInfo (firstOp.Name + " - " + lastOp.Name) dictOps tunnels osmRoutes country line)
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

// ex. 2800_DE0EALN_opposite track_DE0EWHL
let splitTunnelContainingTrack (input: String) : (String * String * String) Option =
    let pattern = "([0-9]{4})_([0-9A-Z]{7})_.*_([0-9A-Z]{7})"
    let m = Regex.Match(input, pattern)

    if m.Success && m.Groups.Count = 4 then
        Some(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)
    else
        None

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
                t.LineIdentification = sol.LineIdentification
                && sol.Tracks
                   |> Array.exists (fun track ->
                       t.ContainingTracks
                       |> Array.exists (fun t ->
                           track.id = t
                           || match splitTunnelContainingTrack t with
                              | Some(n, op1, op2) ->
                                  n = sol.LineIdentification && op1 = sol.StartOP && op2 = sol.EndOP
                              | None -> false)))
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
                  Line = sol.LineIdentification
                  Country = sol.Country }
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
    |> Array.forall (fun s -> not (s = "Not electrified"))

let nullDefaultValue<'a when 'a: null> (defaultValue: 'a) (value: 'a) =
    if isNull value then defaultValue else value

let scale (maxSpeed: int) = 1.0

let travelTime (length: float) (maxSpeed: int) =
    let maxSpeed = if maxSpeed = 0 then 80 else maxSpeed
    length / (float maxSpeed * scale maxSpeed)

let getCost (length: float) (maxSpeed: int) =
    let cost = int (10000.0 * travelTime length maxSpeed)

    if cost <= 0 then 1 else cost

let buildGraph (ops: OperationalPoint[]) (sols: SectionOfLine[]) =

    let mutable graph = Map.empty

    let addOp (op: OperationalPoint) =
        if not (graph.ContainsKey op.UOPID) then
            graph <- graph.Add(op.UOPID, List.empty)

    let findOp (opId: string) =
        ops |> Array.tryFind (fun op -> op.UOPID = opId)

    let getLength (sol: SectionOfLine) = sol.Length / 1000.0

    let addSol (sol: SectionOfLine) =
        match findOp sol.StartOP, findOp sol.EndOP with
        | Some(opStart), Some(opEnd) ->
            let maxSpeed = getMaxSpeed sol 100
            let currLength = Math.Round(getLength sol, 2)
            let cost = getCost currLength maxSpeed

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
                      Length = currLength }
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
                      Length = currLength }
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

let checkIsDir (path: string) = Directory.Exists path

let optCreateIsDir (path: string) =
    if not (checkIsDir path) then
        Directory.CreateDirectory path |> ignore

let allowedCountries =
    "EST;GRC;DEU;NLD;BEL;LUX;FRA;POL;LVA;HRV;HUN;FIN;SVN;ESP;BGR;AUT;ITA;NOR;SWE;DNK;ROU;SVK;CZE;CHE;GBR;PRT"

let countrySplitChars = [| ';' |]

let testIsCountry (countries: string) : bool =
    let allCountries = allowedCountries.Split countrySplitChars

    countries.Split countrySplitChars
    |> Array.forall (fun country -> allCountries |> Array.contains country)

let checkIsCountry (path: string) (countries: string[]) : Async<string[]> =
    async {

        let! result = loadDataCached path "sparql-countries.json" (fun () -> Api.Country.loadData ())

        let allCountries = Api.Country.fromQueryResults result

        let isCountry =
            countries
            |> Array.forall (fun country -> allCountries |> Array.contains country)

        if not isCountry then
            raise (System.ArgumentException($"unkown country {countries}, should be one of '{allowedCountries}'"))

        return countries
    }

let getCountries () : Async<string> =
    async {

        let! result = Api.Country.loadData ()

        let allCountries = Api.Country.fromQueryResults result

        return String.concat ";" allCountries
    }

let getOpTypes () : Async<string> =
    async {

        let! result = Api.OpTypes.loadData ()
        let opTypes = Api.OpTypes.fromQueryResults result

        return JsonSerializer.Serialize opTypes
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
        fprintfn stderr "getOsmRoutes from wikiarticles, count %d" entries1.Length
        fprintfn stderr "getOsmRoutes from wikidata, count %d" entries2.Length
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

let execOperationalPointsBuild (path: string) (countries: string[]) : Async<string> =
    async {
        let! countries = checkIsCountry path countries

        let filename = "sparql-operationalPoints.json"

        let! result =
            loadDataCached path filename (fun () -> loadQueryResultsPaged (Api.OperationalPoint.loadData countries))

        let kgOps = Api.OperationalPoint.fromQueryResults result

        return JsonSerializer.Serialize kgOps
    }

let execTunnelBuild (path: string) (countries: string[]) : Async<string> =
    async {
        let! _ = checkIsCountry path countries

        let! result = loadDataCached path "sparql-tunnel.json" (fun () -> Api.Tunnel.loadData countries)

        let sols = readFile<SectionOfLine[]> path "SectionsOfLines.json"

        let tunnels =
            EraKG.Api.Tunnel.fromQueryResults result sols
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

        return JsonSerializer.Serialize tunnels
    }

let execSectionsOfLineBuild (path: string) (countries: string[]) : Async<string> =
    async {
        let! _ = checkIsCountry path countries

        let filename = "sparql-sectionsOfLine.json"

        let! result =
            loadDataCached path filename (fun () -> loadQueryResultsPaged (Api.SectionOfLine.loadData countries))

        let tracks = readFile<QueryResults> path "sparql-tracks.json"

        let sols = EraKG.Api.SectionOfLine.fromQueryResults result tracks

        return JsonSerializer.Serialize sols
    }

let execTracksBuild (path: string) (countries: string[]) : Async<string> =
    async {
        let! _ = checkIsCountry path countries

        let! _ =
            loadDataCached<QueryResults> path $"sparql-tracks.json" (fun () ->
                loadQueryResultsPaged (Api.Track.loadData countries))

        return ""
    }

let execBuild (path: string) (countries: string[]) (useCache: bool) : Async<string> =
    async {
        cacheEnabled <- useCache
        let now = fun () -> DateTime.Now.ToLongTimeString()

        fprintfn stderr $"execTracksBuild {now ()}"
        let! _ = execTracksBuild path countries

        fprintfn stderr $"execSectionsOfLineBuild {now ()}"
        let! result = execSectionsOfLineBuild path countries
        File.WriteAllText(path + "SectionsOfLines.json", result)

        fprintfn stderr $"execOperationalPointsBuild {now ()}"
        let! result = execOperationalPointsBuild path countries
        File.WriteAllText(path + "OperationalPoints.json", result)

        fprintfn stderr $"execTunnelBuild {now ()}"
        let! result = execTunnelBuild path countries
        File.WriteAllText(path + "Tunnels.json", result)

        fprintfn stderr $"execOpInfohBuild {now ()}"
        let! result = execOpInfohBuild path
        File.WriteAllText(path + "OpInfos.json", result)

        fprintfn stderr $"execGraphBuild {now ()}"
        let! result = execGraphBuild path
        File.WriteAllText(path + "Graph.json", result)

        fprintfn stderr $"execTunnelInfoBuild {now ()}"
        let! result = execTunnelInfoBuild path
        File.WriteAllText(path + "TunnelInfos.json", result)

        fprintfn stderr $"getOsmRoutes"
        let! result = getOsmRoutes ()
        File.WriteAllText(path + "OsmRoutes.json", result)

        fprintfn stderr $"execLineInfoBuild {now ()}"
        let! result = execLineInfoBuild path
        File.WriteAllText(path + "LineInfos.json", result)

        fprintfn stderr $"buildMetadata  {now ()}"

        let metadata: Metadata =
            { Endpoint = "https://data-interop.era.europa.eu/endpoint"
              Ontology = "https://data-interop.era.europa.eu/era-vocabulary"
              Revision = "v3.1.6"
              Program = "https://github.com/bergmannjg/RInfData/tree/main/src/EraKGLoader"
              Countries = countries
              Date = DateTime.Now }

        File.WriteAllText(path + "Metadata.json", JsonSerializer.Serialize metadata)

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
            async { return! execBuild dataDirectory (argv.[0].Split countrySplitChars) useCache }
        else if argv.[0] = "--Countries" then
            async { return! getCountries () }
        else if argv.[0] = "--OpTypes" then
            async { return! getOpTypes () }
        else if argv.[0] = "--OsmRoutes" then
            async { return! getOsmRoutes () }
        else if
            argv.[0] = "--Build"
            && checkIsDir argv.[1]
            && argv.Length >= 3
            && testIsCountry argv.[2]
        then
            async { return! execBuild argv.[1] (argv.[2].Split countrySplitChars) useCache }
        else if argv.[0] = "--Graph.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execGraphBuild argv.[1] }
        else if argv.[0] = "--OpInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execOpInfohBuild argv.[1] }
        else if argv.[0] = "--LineInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execLineInfoBuild argv.[1] }
        else if argv.[0] = "--TunnelInfo.Build" && argv.Length > 1 && checkIsDir argv.[1] then
            async { return! execTunnelInfoBuild argv.[1] }
        else if argv.[0] = "--OperationalPoints" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execOperationalPointsBuild argv.[1] (argv.[2].Split countrySplitChars) }
        else if argv.[0] = "--OperationalPoints" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execOperationalPointsBuild argv.[1] (argv.[2].Split countrySplitChars) }
        else if argv.[0] = "--Tunnel" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execTunnelBuild argv.[1] (argv.[2].Split countrySplitChars) }
        else if argv.[0] = "--SectionsOfLine" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execSectionsOfLineBuild argv.[1] (argv.[2].Split countrySplitChars) }
        else if argv.[0] = "--Tracks" && argv.Length > 2 && checkIsDir argv.[1] then
            async { return! execTracksBuild argv.[1] (argv.[2].Split countrySplitChars) }
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
