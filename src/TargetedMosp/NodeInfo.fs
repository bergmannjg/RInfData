namespace TMosp

open System
open FSharp.Collections

type internal NodeInfo(n: Node) =
    let initialize () =
        let arr: CostArray array = Utils.resize (int Constants.DIM)
        arr |> Array.map (fun _ -> CostArray.generateWith (uint32 0))

    member val n = n
    member val reached: bool array = Utils.fill (int Constants.DIM) false
    member val potential: CostArray = CostArray.generateWith (uint32 0)
    member val private priority: uint32 array = Utils.resize (int Constants.DIM)
    member val preprocessingResult: CostArray array = initialize ()
    member this.get_priority(index: Dimension) = this.priority[int index]
    member this.set_priority(index: Dimension, prio: UInt32) = this.priority[int index] <- prio

    member this.updatePreprocessingInfo(dimOrdering: DimensionsVector, c: CostArray) =
        let mainOptIndex = dimOrdering[0]
        this.preprocessingResult[int mainOptIndex] <- c
