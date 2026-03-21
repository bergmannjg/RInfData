namespace TMosp

open System
#if FABLE_COMPILER
#else
open System.Diagnostics
#endif

module Api =
    let private proccess (G: Graph) (maxSolutions: int) =
        let preprocessor = Preprocessor G
        preprocessor.run (NodeInfoContainer G)
        let mda = TargetedMDA(G, preprocessor.potential)

#if FABLE_COMPILER
        let start = System.DateTime.Now
#else
        let stopWatch = new Stopwatch()
        stopWatch.Start()
#endif
        mda.run maxSolutions
#if FABLE_COMPILER
        let stop = System.DateTime.Now
#else
        stopWatch.Stop()
#endif

#if FABLE_COMPILER
        let print (s: string) = printfn $"{s}"
#else
        let print (s: string) = fprintfn stderr $"{s}"
#endif

        print $"TargetedMDA.iterations {TargetedMDA.iterations}"
        print $"TargetedMDA.permanents {TargetedMDA.permanents}"
        print $"TargetedMDA.solutions {int mda.sols}"
#if FABLE_COMPILER
        print $"TargetedMDA.duration {(stop - start).TotalMilliseconds} ms"
#else
        print $"TargetedMDA.duration {int stopWatch.Elapsed.TotalMilliseconds} ms"
#endif
        print $"TargetedMDA.maxHeapSize {TargetedMDA.maxHeapSize}"
        print $"TargetedMDA.BinaryHeapMosp.pushes {BinaryHeapMosp.pushes}"
        print $"TargetedMDA.Label.count {Label.count}"

        mda.getSolutions ()

    let fromArcs
        (arcs: (Node * Arc) array, sourceId: Node, targetId: Node, maxSolutions: int)
        : ((UInt32 * CostArray) array) array =
        let G = Graph.fromArcs (arcs, Graph.countNodes arcs, sourceId, targetId)
        proccess G maxSolutions

    let fromLines
        (lines: string array, sourceId: Node, targetId: Node, maxSolutions: int)
        : ((UInt32 * CostArray) array) array =
        let G = Graph.fromLines (lines, sourceId, targetId)
        proccess G maxSolutions

#if FABLE_COMPILER
#else
    let fromFile
        (path: string, sourceId: Node, targetId: Node, maxSolutions: int)
        : ((UInt32 * CostArray) array) array =
        let G = Graph.fromFile (path, sourceId, targetId)
        proccess G maxSolutions
#endif
