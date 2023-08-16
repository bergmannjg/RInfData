module Wikidata.Comparison

open EraKG
open Wikidata.Sparql

let private matchType (op: OperationalPoint) =
    op.Type = "station" || op.Type = "passengerStop"

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
        || if uOPID.Contains "  " && railwayRef.Length = 4 && railwayRef.Contains " " then
               let railwayRefX = railwayRef.Replace(" ", "  ")
               toOPID railwayRefX = uOPID.Replace(" ", "0") // matches 'TU R' with 'DETU  R'
           else
               false

    railwayRef.Split [| ';' |] |> Array.exists (fun s -> _matchUOPID s uOPID)

let private compareByUOPID
    (operationalPoints: OperationalPoint[])
    (osmEntries: Entry[])
    : (OperationalPoint * Entry option)[] =

    operationalPoints
    |> Array.map (fun op ->
        match
            osmEntries
            |> Array.tryFind (fun entry -> entry.stationCode.IsSome && matchUOPID entry.stationCode.Value op.UOPID)
        with
        | Some entry -> (op, Some entry)
        | None -> (op, None))

let compare (extra: bool) (allOperationalPoints: OperationalPoint[]) (osmEntries: Entry[]) =

    let operationalPoints =
        allOperationalPoints |> Array.filter (fun op -> matchType op)

    let result = compareByUOPID operationalPoints osmEntries

    let operationalPointsFoundPhase1 =
        result |> Array.filter (fun (_, entry) -> entry.IsSome)

    let operationalPointsNotFoundPhase1 =
        result
        |> Array.filter (fun (_, entry) -> entry.IsNone)
        |> Array.map (fun (op, _) -> op)

    let operationalPointsFoundPhase2 = [||]

    let operationalPointsFound =
        Array.concat
            [ operationalPointsFoundPhase1
              operationalPointsFoundPhase2 |> Array.map (fun (op, entry) -> (op, Some entry)) ]

    let countPointsFound = operationalPointsFound.Length

    let operationalPointsNotFound =
        operationalPointsNotFoundPhase1
        |> Array.filter (fun op ->
            operationalPointsFoundPhase2
            |> Array.exists (fun (op1, _) -> op.UOPID = op1.UOPID)
            |> not)

    let countPointsNotFound = operationalPointsNotFound.Length

    if extra then
        operationalPointsNotFound
        |> Array.iter (fun op -> fprintfn stderr $"{op.UOPID} {op.Name}")

    operationalPointsNotFound
    |> Array.groupBy (fun op -> op.Type)
    |> Array.iter (fun (k, l) -> fprintfn stderr $"type {k}, not found {l.Length}")

    fprintfn stderr $"total {operationalPoints.Length}, found {countPointsFound}, not found {countPointsNotFound}"
