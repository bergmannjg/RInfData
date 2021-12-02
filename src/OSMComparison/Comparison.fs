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
    (allStops: OsmOperationalPoint [])
    (ops: OperationalPoint [])
    (rinfGraph: GraphNode [])
    (uicRefMappings: DB.UicRefMapping [])
    =

    let (stopsOfLine, opMatchings, solMatchings) =
        getRInfOsmMatching line relationOfLine elementsOfLine allStops ops rinfGraph uicRefMappings

    Result.collectResult compact line relationOfLine elementsOfLine ops stopsOfLine opMatchings solMatchings

let private filterUicRefMapping (op: OperationalPoint) (m: DB.UicRefMapping) =
    m.DS100.Split [| ',' |]
    |> Array.exists (fun s -> Transform.Op.toOPID s = op.UOPID)

// UicRefMappings may contain all active stations
let private filterUicRefMappings (uicRefMappings: DB.UicRefMapping []) (ops: OperationalPoint []) =
    ops
    |> Array.filter (fun op ->
        uicRefMappings
        |> Array.exists (filterUicRefMapping op))

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
    // |> Array.map (fun w ->
    //     printfn "way %d not in relation %d of line %d" w.id r.id line
    //     w)
    |> fun ways ->
        if ways.Length > 0 then
            printfn "  ways not in relation %d of line %d: %d " r.id line ways.Length

let private processAsync osmDataDir rinfDataDir compact useRemote area allStops uicRefMappings line =
    async {
        try
            use client = OverpassRequest.getClient ()
            let! osmData = OSMLoader.loadOsmData osmDataDir client useRemote area line

            let ops =
                (RInfLoader.loadRInfOperationalPoints line rinfDataDir)
                |> Array.filter (fun op -> not (suisseOps |> Array.contains op.UOPID))

            let filteredOps = ops |> filterUicRefMappings uicRefMappings

            if not compact then
                if ops.Length <> filteredOps.Length then
                    ops
                    |> Array.filter (fun op ->
                        not (
                            filteredOps
                            |> Array.exists (fun fOp -> fOp.UOPID = op.UOPID)
                        ))
                    |> Array.iter (fun op -> printfn "maybe inactive op: %s %s %s" op.UOPID op.Name (Result.osmUrl op))

            match OSM.Transform.SoL.getRelationOfRailwayLine line osmData.elements with
            | Some r ->
                printWaysOfLineNotInRelation line r osmData.elements

                return
                    Some(
                        compareLine
                            compact
                            line
                            r
                            osmData.elements
                            allStops
                            filteredOps
                            (RInfLoader.loadRInfGraph rinfDataDir)
                            uicRefMappings
                    )
            | None -> return None
        with
        | ex ->
            printfn "Error: %s %s" ex.Message ex.StackTrace
            return None

    }

let private take<'a> count (arr: 'a []) =
    arr |> Array.take (min count arr.Length)

let compareLineAsync
    osmDataDir
    rinfDataDir
    dbDataDir
    (line: int)
    (useRemote: bool)
    (useAllStops: bool)
    (area: string option)
    =
    let uicRefMappings = DB.DBLoader.loadMappings dbDataDir

    processAsync
        osmDataDir
        rinfDataDir
        false
        useRemote
        area
        (if useAllStops then
             OSMLoader.loadAllStops osmDataDir uicRefMappings
         else
             [||])
        uicRefMappings
        line

let compareLinesAsync osmDataDir rinfDataDir dbDataDir (count: int) =
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

        let uicRefMappings = DB.DBLoader.loadMappings dbDataDir
        let allStops = OSMLoader.loadAllStops osmDataDir uicRefMappings
        let! results = loop (processAsync osmDataDir rinfDataDir true false None allStops uicRefMappings) lines []

        Result.printCommonResult results
    }

let compareLinesListAsync osmDataDir rinfDataDir dbDataDir (linesArg: string []) =
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
            linesArg
            |> Array.map (fun l -> int l)
            |> Array.toList

        printfn "***railway lines: %d" lines.Length

        let uicRefMappings = DB.DBLoader.loadMappings dbDataDir
        let allStops = OSMLoader.loadAllStops osmDataDir uicRefMappings
        let! results = loop (processAsync osmDataDir rinfDataDir true false None allStops uicRefMappings) lines []

        Result.printCommonResult results
    }
