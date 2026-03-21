namespace TMosp

open System
open FSharp.Collections

type Node = UInt32
type NeighborhoodSize = UInt16
type internal ArcId = UInt32
type CostType = UInt32
type internal Dimension = UInt16
type internal LabelPtr = UInt32

module Constants =
#if DIM3
    let DIM = uint16 3
#else
#if DIM2
    let DIM: Dimension = uint16 2
#else
    [<CompilerMessage("DIM2 or DIM3 define expected", 1234, IsError = true)>]
    let DIM: Dimension = uint16 0
#endif
#endif

    let INVALID_NODE: Node = UInt32.MaxValue
    let MAX_DEGREE: NeighborhoodSize = UInt16.MaxValue
    let INVALID_ARC: ArcId = UInt32.MaxValue
    let MAX_COST: CostType = UInt32.MaxValue
    let MAX_PATH: UInt32 = UInt32.MaxValue

type CostArray = CostType array

#if DIM2
type internal TruncatedCosts= CostType
#else
type internal TruncatedCosts(c0: CostType, c1: CostType) =
    member val c0 = c0
    member val c1 = c1
#endif

type internal DimensionsVector = Dimension array

module internal Utils =
    let inline checkSize<'T> (argName: string) (arr: 'T array) (size: int) =
        if arr.Length <> size then
            invalidArg argName $"size {size} expected"

    let resize<'T> (size: int) =
        let mutable arr: 'T array = null
        Array.Resize(&arr, size)
        arr

    let fill<'T> (size: int) (value: 'T) = Array.create<'T> size value

module CostArray =
    open Utils

    let inline internal generateWith (c: CostType) : CostArray =
        if Constants.DIM = uint16 2 then
            [| c; c |]
        else
            [| c; c; c |]

    let inline internal generate () : CostArray = generateWith Constants.MAX_COST

    let toString (c: CostArray) : string =
        if Constants.DIM = uint16 2 then
            $"cost ({c[0]},{c[1]})"
        else if Constants.DIM = uint16 3 then
            $"cost ({c[0]},{c[1]},{c[2]})"
        else
            ""

    let inline internal truncate (c: CostArray) : TruncatedCosts =
        checkSize "c" c (int Constants.DIM)
#if DIM2
        c[1]
#else
        TruncatedCosts(c[1], c[2])
#endif
    let inline internal lexSmaller (lhs: CostArray) (rhs: CostArray) : bool =
        checkSize "lh" lhs (int Constants.DIM)
        checkSize "rh" rhs (int Constants.DIM)

        if Constants.DIM = uint16 2 then
            lhs[0] < rhs[0] || lhs[0] = rhs[0] && lhs[1] < rhs[1]
        else
            lhs[0] < rhs[0]
            || lhs[0] = rhs[0] && lhs[1] < rhs[1]
            || lhs[0] = rhs[0] && lhs[1] = rhs[1] && lhs[2] <= rhs[2]

    let internal substract (rhs: CostArray, lhs: CostArray) : CostArray =
        checkSize "lhs" lhs (int Constants.DIM)
        checkSize "rhs" rhs (int Constants.DIM)

        Array.map2 (fun r l -> r - l) rhs lhs

    let inline internal add (rhs: CostArray) (lhs: CostArray) : CostArray =
        checkSize "lhs" lhs (int Constants.DIM)
        checkSize "rhs" rhs (int Constants.DIM)

        Array.map2 (fun r l -> r + l) rhs lhs

    let internal addInPlace (rhs: CostArray) (lhs: CostArray) =
        checkSize "lhs" lhs (int Constants.DIM)
        checkSize "rhs" rhs (int Constants.DIM)

        for i = 0 to int Constants.DIM - 1 do
            rhs[i] <- rhs[i] + lhs[i]

    let internal addWithDimOrdering (rhs: CostArray) (lhs: CostArray) (dimOrdering: DimensionsVector) : CostArray =
        checkSize "lhs" lhs (int Constants.DIM)
        checkSize "rhs" rhs (int Constants.DIM)

        Array.mapi2 (fun i r _ -> r + lhs[int dimOrdering[i]]) rhs lhs

    let internal max (rhs: CostArray) (lhs: CostArray) (dimOrdering: DimensionsVector) : CostArray =
        checkSize "lhs" lhs (int Constants.DIM)
        checkSize "rhs" rhs (int Constants.DIM)
        let max (x: UInt32, y: UInt32) = if x < y then y else x
        Array.mapi2 (fun i _ l -> max (rhs[int dimOrdering[i]], l)) rhs lhs

    let inline internal dominates (lhs: CostArray) (rhs: CostArray) =
        if Constants.DIM = uint16 2 then
            lhs[0] <= rhs[0] && lhs[1] <= rhs[1]
        else
            lhs[0] <= rhs[0] && lhs[1] <= rhs[1] && lhs[2] <= rhs[2]

    let inline internal dominatesLexSmaller (lhs: CostArray) (rhs: CostArray) : bool = dominates lhs rhs

    let inline internal tc_dominates (lhs: TruncatedCosts) (rhs: CostArray) =
        checkSize "rhs" rhs (int Constants.DIM)

#if DIM2
        lhs <= rhs[1]
#else
        lhs.c0 <= rhs[1] && lhs.c1 <= rhs[2]
#endif

module internal TruncatedCosts =
    open Utils

    let toString (c: TruncatedCosts) : string =
#if DIM2
        $"  truncated cost ({c})"
#else
        $"  truncated cost ({c.c0},{c.c1})"
#endif

    let inline lexSmaller (lhs: TruncatedCosts) (rhs: TruncatedCosts) =
#if DIM2
        lhs < rhs
#else
        lhs.c0 < rhs.c0 || lhs.c0 = rhs.c0 && lhs.c1 < rhs.c1
#endif
    let inline lexSmallerOrEquiv (lhs: TruncatedCosts) (rhs: CostArray) =
        checkSize "rh" rhs (int Constants.DIM)

#if DIM2
        lhs <= rhs[1]
#else
        lhs.c0 < rhs[1] || lhs.c0 = rhs[1] && lhs.c1 <= rhs[2]
#endif

    let inline tc_dominates (lhs: TruncatedCosts) (rhs: TruncatedCosts) =
#if DIM2
        lhs <= rhs
#else
        lhs.c0 <= rhs.c0 && lhs.c1 <= rhs.c1
#endif