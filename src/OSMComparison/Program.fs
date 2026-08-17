open System
open System.IO
open System.Text.Json

open Sparql
open EraKG
open OSM.Sparql
open OSM.Comparison
open RInf.Types

let mutable verbose = false

let printHelp () =
    """
USAGE: EraKGOsmComparison

OPTIONS:

    --Osm                 <dataDir>
                          load OSM data.
    --Osm.Compare         <dataDir>
                          compare OSM data.
    --help                display this list of options.
"""

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let checkIsDir (path: string) = Directory.Exists path

let server = "qlever.cs.uni-freiburg.de"

let endpoint (server: string) = $"https://{server}/api/osm-planet"

let getOsmEntries (path: string) (file: string) (endpoint: string) : Async<Entry[]> =
    async {
        let fullpath = path + file

        try
            if not (File.Exists fullpath) then
                let! result = OSM.Sparql.Api.loadData endpoint
                fprintfn stderr $"loadOsmData, {result.Length} bytes"
                File.WriteAllText(fullpath, result)

            let result = readFile<QueryResults> path file
            return OSM.Sparql.Api.fromQueryResults result
        with e ->
            fprintfn stderr "getOsmEntries: endpoint {endpoint}, error '%s'" e.Message
            return Array.empty
    }

let checkDistance
    (line: string)
    (startOp: RInfGraph.OpInfo)
    (endOp: RInfGraph.OpInfo)
    (length: float<km>)
    (minimumHorizontalRadius: int<m>)
    : (bool * float * string) =
    let distance =
        OSM.Comparison.``calculate distance`` (startOp.Latitude, startOp.Longitude) (endOp.Latitude, endOp.Longitude)

    let difference =
        if distance < length then
            length - distance
        else
            distance - length

    let percent = difference * 100.0 / length

    let valid = 4.0<_> < length || length < 1.5<_> && percent < 16.0 || percent < 10.0

    if verbose then
        if not valid && minimumHorizontalRadius < 1000<_> then
            fprintfn
                stderr
                $"invalid but small radius, sol {line}, startOp {startOp.UOPID}, endOP {endOp.UOPID}, length {length}, minRadius {minimumHorizontalRadius}"

    let almostStraightSol = 1000<m> < minimumHorizontalRadius || length < 1.0<km>
    let valid = valid || not almostStraightSol // check only almost straight sols

    valid,
    percent,
    $"sol {line}, startOp {startOp.UOPID}, endOP {endOp.UOPID}, length {length}, minRadius {minimumHorizontalRadius}, distance %.3f{distance}, percent %.0f{percent}"

let checkDistanceWithMatchings
    (line: string)
    (length: float<km>)
    (minimumHorizontalRadius: int<m>)
    (ops: RInfGraph.OpInfo[])
    (startOpIndex: int)
    (endOpIndex: int)
    (percentOrigDifference: float)
    (matchings: Matching[])
    (infoOfCheck: string)
    =
    let validIncrease (percentOrig: float) (percent: float) =
        percentOrig < 15 && percent < 6
        || percentOrig < 25 && percent < 8
        || percent < 20

    if verbose then
        fprintfn stderr $"invalid of check, {infoOfCheck}"

    match matchings |> Array.tryFind (fun m -> m.UOPID = ops[startOpIndex].UOPID) with
    | Some m when m.Latitude.IsSome && m.Longitude.IsSome ->
        let valid, percentDifference, info =
            checkDistance
                line
                { ops[startOpIndex] with
                    Latitude = m.Latitude.Value
                    Longitude = m.Longitude.Value }
                ops[endOpIndex]
                length
                minimumHorizontalRadius

        if valid && validIncrease percentOrigDifference percentDifference then
            if verbose then
                fprintfn stderr $"valid with changed startOp {info}"

            true, Some(startOpIndex, m.Latitude.Value, m.Longitude.Value)
        else
            if verbose then
                fprintfn stderr $"invalid with changed startOp {info}"

            false, None
    | _ ->
        match matchings |> Array.tryFind (fun m -> m.UOPID = ops[endOpIndex].UOPID) with
        | Some m when m.Latitude.IsSome && m.Longitude.IsSome ->
            let valid, percentDifference, info =
                checkDistance
                    line
                    ops[startOpIndex]
                    { ops[endOpIndex] with
                        Latitude = m.Latitude.Value
                        Longitude = m.Longitude.Value }
                    length
                    minimumHorizontalRadius

            if valid && validIncrease percentOrigDifference percentDifference then
                if verbose then
                    fprintfn stderr $"valid with changed endOp {info}"

                true, Some(endOpIndex, m.Latitude.Value, m.Longitude.Value)
            else
                if verbose then
                    fprintfn stderr $"invalid with changed endOp {info}"

                false, None
        | _ -> false, None

let osmCompare (pathInput: string) (pathOutput: string) =
    // operational points with type station or passenger stop in germany
    let ops =
        readFile<RInfGraph.OpInfo[]> pathInput "OpInfos.json"
        |> Array.filter (fun op -> op.UOPID.StartsWith "DE" && (op.RinfType = 10 || op.RinfType = 70))

    fprintfn stderr $"kg operationalPoints: {ops.Length}"

    let g = readFile<RInfGraph.GraphNode[]> pathInput "Graph.json"
    fprintfn stderr $"kg graph nodes: {g.Length}"

    let opsFiltered =
        ops
        |> Array.filter (fun op ->
            match g |> Array.tryFind (fun n -> n.Node = op.UOPID) with
            | Some n -> 0 < n.Edges.Length
            | None ->
                fprintfn stderr $"opid {op.UOPID} not found in graph"
                false)

    fprintfn stderr $"kg operationalPoints filtered: {opsFiltered.Length}"

    let osmEntries = readFile<Entry[]> pathInput "OsmEntries.json"
    fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

    let result = findMatchings opsFiltered osmEntries false
    let opsNotFound = result |> Array.filter _.OsmUrl.IsNone

    fprintfn
        stderr
        $"total {opsFiltered.Length}, found {opsFiltered.Length - opsNotFound.Length}, not found {opsNotFound.Length} with maxRInfOsmDistance {maxRInfOsmDistance}"

    File.WriteAllText(pathOutput + "RInfOsmMatchings.json", JsonSerializer.Serialize(result))

type OpInfoChange =
    { orig: RInfGraph.OpInfo
      changed: RInfGraph.OpInfo
      distance: float<km>
      lineLengthToOpGeoInPercent: float }

/// <summary>Check if the length of a section of line is almost the same with the geo distance of the given operational points for a almost straight section of line</summary>
/// <remarks>
/// Assumptions
/// * the length value is correct
/// * the geo values of the ops may be incorrect
/// * the geo value of an osm element with the same uopid is correct if it exists
/// </remarks>
/// <param name="line">line identification</param>
/// <param name="startOp">uopid of start operational point of section of line</param>
/// <param name="endOp">uopid of end operational point of section of line</param>
/// <param name="length">length of section of line</param>
/// <param name="minimumHorizontalRadius">minimum horizontal radius of section of line</param>
/// <param name="ops">all operational points</param>
/// <param name="matchings">matchimgs of operational points and osm data, generated by <see>osmCompare</see></param>
/// <returns>OpInfoChange if geo of start op or end op is not valid and geo data of matching osm data would make op valid</returns>
let rinfCheckSectionOfLine
    (line: string)
    (startOp: string)
    (endOp: string)
    (length: float<km>)
    (minimumHorizontalRadius: int<m>)
    (ops: RInfGraph.OpInfo array)
    (matchings: Matching[])
    : OpInfoChange option =
    match
        ops |> Array.tryFindIndex (fun op -> startOp = op.UOPID), ops |> Array.tryFindIndex (fun op -> endOp = op.UOPID)
    with
    | Some startOpIndex, Some endOpIndex ->
        let valid, percent, info =
            checkDistance line ops[startOpIndex] ops[endOpIndex] length minimumHorizontalRadius

        if not valid then
            let valid, res =
                checkDistanceWithMatchings
                    line
                    length
                    minimumHorizontalRadius
                    ops
                    startOpIndex
                    endOpIndex
                    percent
                    matchings
                    info

            if valid && res.IsSome then
                let index, lat, lon = res.Value
                let orig = ops[index]

                let changed =
                    { orig with
                        Latitude = lat
                        Longitude = lon }

                let distance =
                    OSM.Comparison.``calculate distance``
                        (orig.Latitude, orig.Longitude)
                        (changed.Latitude, changed.Longitude)

                if 0.2<_> < distance then
                    Array.set ops index changed

                    Some
                        { orig = orig
                          changed = changed
                          distance = round (distance, 3)
                          lineLengthToOpGeoInPercent = Math.Round percent }
                else
                    None
            else
                None
        else
            None
    | _, _ -> None


let rinfCheck (pathInput: string) (pathOutput: string) (line: string option) (change: bool) =
    let matchings = readFile<Matching[]> pathInput "RInfOsmMatchings.json"

    let ops = readFile<RInfGraph.OpInfo[]> pathInput "OpInfos.json"
    let mutable changes: OpInfoChange[] = [||]

    let sols =
        readFile<EraKG.SectionOfLine[]> pathInput "SectionsOfLines.json"
        |> Array.filter (fun sol ->
            match line with
            | Some line -> sol.LineIdentification = line
            | None -> true)

    let tuples =
        sols
        |> Array.map (fun sol ->
            let minimumHorizontalRadius =
                sol.Tracks
                |> Array.map _.minimumHorizontalRadius
                |> Array.choose id
                |> fun arr -> if 0 < arr.Length then Array.min arr else 0<_>

            sol.StartOP, sol.EndOP, sol.LineIdentification, sol.Length, minimumHorizontalRadius)
        |> Array.sortBy (fun (_, _, line, length, _) -> (line, length))

    let mutable madeValid = 0

    tuples
    |> Array.iter (fun (startOp, endOp, line, length, minimumHorizontalRadius) ->
        match rinfCheckSectionOfLine line startOp endOp length minimumHorizontalRadius ops matchings with
        | Some change ->
            changes <- Array.append changes [| change |]
            madeValid <- madeValid + 1
        | None -> ())

    let changes = changes |> Array.sortBy (fun c -> c.orig.Name)
    fprintfn stderr $"madeValid {madeValid}"
    File.WriteAllText(pathInput + "OpInfoChanges.json", JsonSerializer.Serialize changes)

    if change && 0 < madeValid then
        File.WriteAllText(pathInput + "OpInfos.json", JsonSerializer.Serialize ops)
        osmCompare pathInput pathOutput

[<EntryPoint>]
let main argv =
    try
        verbose <- argv |> Array.contains "--verbose"
        let argv = argv |> Array.filter (fun arg -> arg <> "--verbose")

        let getServer () =
            if argv.Length > 2 then argv.[2] else server

        let getEndpoint () = endpoint <| getServer ()

        if argv.Length = 0 then
            async { return printHelp () }
        else if argv.[0] = "--Osm" && argv.Length > 1 && checkIsDir argv.[1] then
            async {
                let! entries = getOsmEntries argv.[1] "sparql-osm.json" (getEndpoint ())
                fprintfn stderr $"getOsmEntries, count {entries.Length}"
                File.WriteAllText(argv.[1] + "OsmEntries.json", JsonSerializer.Serialize entries)

                return ""
            }
        else if argv.[0] = "--Osm.Compare" && argv.Length > 1 && checkIsDir argv.[1] then
            async {
                let pathInput = argv.[1]

                // operational points with type station or passenger stop in germany
                let ops =
                    readFile<RInfGraph.OpInfo[]> pathInput "OpInfos.json"
                    |> Array.filter (fun op -> op.UOPID.StartsWith "DE" && (op.RinfType = 10 || op.RinfType = 70))

                fprintfn stderr $"kg operationalPoints: {ops.Length}"

                let osmEntries = readFile<Entry[]> pathInput "OsmEntries.json"
                fprintfn stderr $"kg osmEntries: {osmEntries.Length}"

                let result = findMatchings ops osmEntries false
                let opsNotFound = result |> Array.filter _.OsmUrl.IsNone

                fprintfn
                    stderr
                    $"total {ops.Length}, found {ops.Length - opsNotFound.Length}, not found {opsNotFound.Length}"

                return JsonSerializer.Serialize result
            }
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
