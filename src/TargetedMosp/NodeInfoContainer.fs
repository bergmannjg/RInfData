namespace TMosp

open System
open FSharp.Collections

type internal NodeInfoContainer(G: Graph) =
    let initialize () =
        let arr: NodeInfo array = Utils.resize (int G.nodesCount)

        for i = 0 to int G.nodesCount - 1 do
            arr[i] <- NodeInfo(uint32 i)

        arr

    member val nodeInfos: NodeInfo array = initialize () with get, set

    member this.getInfo(n: Node) = this.nodeInfos[int n]

type internal PotentialComparison(d: DimensionsVector) =
    member this.lexSmaller (lh: NodeInfo) (rh: NodeInfo) =
        CostArray.lexSmaller (lh.preprocessingResult[int d[0]]) (rh.preprocessingResult[int d[0]])
