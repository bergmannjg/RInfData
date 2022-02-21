module Comparison

open System

open OSM
open RInf
open RInfGraph
open Matching

let private compareLine
    (compact: bool)
    (line: int)
    (relationOfLine: Relation)
    (elementsOfLine: Element [])
    (stopsOfLine: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (matchings: OpMatch seq)
    =

    let (opMatchings, solMatchings) =
        getRInfOsmMatching line elementsOfLine ops rinfGraph matchings

    Result.collectResult compact line relationOfLine elementsOfLine ops stopsOfLine opMatchings solMatchings

let private suisseOps =
    [| "DE  RNK"
       "DE  RBE"
       "DE RBEF"
       "DE RNHN"
       "DE RHRB" |]

let private printWaysOfLineNotInRelation (line: int) (r: Relation) (elementsOfLine: Element []) =
    elementsOfLine
    |> Array.choose (Data.asWay)
    |> Array.filter (fun w ->
        OSM.Data.hasTagWithValue Tag.Railway "rail" (Way w)
        && OSM.Data.existsTag Tag.Usage (Way w)
        && OSM.Data.hasTagWithValue Tag.Ref (line.ToString()) (Way w)
        && not (r.members |> Array.exists (fun m -> m.ref = w.id)))
    |> Array.map (fun w ->
        printfn "way %d not in relation %d of line %d" w.id r.id line
        w)
    |> fun ways ->
        if ways.Length > 0 then
            printfn "  ways not in relation %d of line %d: %d " r.id line ways.Length

let private processLineAsync osmDataDir rinfDataDir compact useRemote area allStops mapAllStops line =
    async {
        try
            use client = OverpassRequest.getClient ()
            let! osmData = OSMLoader.loadOsmData osmDataDir client useRemote area line

            let ops =
                (RInfLoader.loadRInfOperationalPoints line rinfDataDir)
                |> Array.filter (fun op -> not (suisseOps |> Array.contains op.UOPID))

            let rinfGraph = RInfLoader.loadRInfGraph rinfDataDir

            match OSM.Transform.SoL.getRelationOfRailwayLine line osmData.elements with
            | Some relationOfLine ->
                let stopsOfLine =
                    Array.concat [ (Transform.Op.nodeStopsToOsmOperationalPoints osmData.elements)
                                   (Transform.Op.wayStopsToOsmOperationalPoints osmData.elements)
                                   (Transform.Op.relationStopsToOsmOperationalPoints osmData.elements) ]

                let matchingByOpId =
                    mkMatchingByOpId relationOfLine stopsOfLine osmData.elements allStops mapAllStops

                let matchingByName =
                    mkMatchingByName relationOfLine stopsOfLine osmData.elements allStops

                let dbDataDir = "../../db-data/"

                let uicRefs = DB.DBLoader.loadMappings dbDataDir

                let matchingByUicRef =
                    mkMatchingByUicRef relationOfLine stopsOfLine osmData.elements allStops uicRefs

                return
                    Some(
                        compareLine
                            compact
                            line
                            relationOfLine
                            osmData.elements
                            stopsOfLine
                            ops
                            rinfGraph
                            [ matchingByOpId
                              matchingByUicRef
                              matchingByName ]
                    )
            | None -> return None
        with
        | ex ->
            printfn "Error: %s %s" ex.Message ex.StackTrace
            return None

    }

let private take<'a> count (arr: 'a []) =
    arr |> Array.take (min count arr.Length)

let compareLineAsync osmDataDir rinfDataDir dbDataDir line useRemote useAllStops (area: string option) =
    let allStops =
        if useAllStops then
            OSMLoader.loadAllStops osmDataDir
        else
            [||]

    let mapAllStops = Transform.Op.toRailwayRefMap allStops (Map.empty)

    processLineAsync osmDataDir rinfDataDir false useRemote area allStops mapAllStops line

let compareLinesAsync osmDataDir rinfDataDir dbDataDir count =
    let rec loop (work: 'a -> Async<'b>) (l: 'a list) (results: 'b list) =
        async {
            match l with
            | h :: t ->
                let! res = work h
                return! loop work t (res :: results)
            | [] -> return results |> List.rev
        }

    async {
        let lines =
            RInfLoader.loadRInfLines rinfDataDir
            |> take count
            |> Array.toList

        printfn "***RInf passenger railway lines: %d" lines.Length

        let allStops = OSMLoader.loadAllStops osmDataDir
        let mapAllStops = Transform.Op.toRailwayRefMap allStops (Map.empty)

        let! results = loop (processLineAsync osmDataDir rinfDataDir true false None allStops mapAllStops) lines []

        Result.printCommonResult results
    }
