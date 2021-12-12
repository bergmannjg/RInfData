module RInfLoader

open System.IO
open System.Text.Json

open RInf
open RInfGraph

let private readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let loadRInfGraph (rinfDataDir) =
    readFile<GraphNode []> rinfDataDir "Graph.json"

let loadRInfLines (rinfDataDir) =
    readFile<LineInfo []> rinfDataDir "LineInfos.json"
    |> Array.map (fun li -> int li.Line)
    |> Array.distinct
    |> Array.sort

let loadRInfOperationalPoints (line: int) rinfDataDir =
    let now = System.DateTime.Now

    let fixErrors (m: OperationalPoint) =
        if m.UOPID = "DELE  H" then
            { m with UOPID = "DE LE H" } // according to DB Netze Infrastrukturregister
        else
            m
    readFile<OperationalPoint []> rinfDataDir "OperationalPoints.json"
    |> Array.filter (fun op ->
        op.Type <> "junction"
        && op.Type <> "switch"
        && op.Type <> "domestic border point"
        && op.RailwayLocations
           |> Array.exists (fun loc -> loc.NationalIdentNum = line.ToString()))
    |> Array.groupBy (fun op -> op.UOPID)
    |> Array.map (fun (k, ops) ->
        match ops
              |> Array.tryFind (fun op -> now < op.ValidityDateEnd)
            with
        | Some op -> fixErrors op
        | None -> ops.[0])
    |> Array.sortBy (fun op ->
        op.RailwayLocations
        |> Array.find (fun loc -> loc.NationalIdentNum = line.ToString())
        |> fun loc -> loc.Kilometer)
