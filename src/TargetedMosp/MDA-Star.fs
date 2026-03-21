namespace TMosp

open System
open Microsoft.FSharp.Collections
open System.Collections.Generic

type internal TargetedMDA(G: Graph, potential: CostArray array) =

#if DEBUG
    let verbose =
        try
            not (isNull (System.Environment.GetEnvironmentVariable "MDAVerbose"))
        with _ ->
            false
#else
    let verbose = false
#endif

    let initializeTruncatedFront () =
        let arr: TruncatedFront array = Utils.resize (int G.nodesCount)

        for i = 0 to int G.nodesCount - 1 do
            arr[i] <- TruncatedFront()

        arr

    let initializeCandidateLabels () =
        let arr: TMosp.List array = Utils.resize (int G.arcsCount)

        for i = 0 to int G.arcsCount - 1 do
            arr[i] <- TMosp.List()

        arr

    let printLabel (text: string) (label: Label) =
        printfn $"{text} node {label.n} pathId {label.pathId} predArcId {label.predArcId} {CostArray.toString label.c}"

    let printNextCandidateLabel (text: string) (arc: Arc) (label: Label) =
        let n = if label.next.IsSome then label.next.Value.n else (uint32 0)

        let pathId =
            if label.next.IsSome then
                label.next.Value.pathId
            else
                (uint32 0)

        printfn
            $"{text} arcid {arc.id} node {label.n} pathId {label.pathId} predArcId {label.predArcId} {CostArray.toString label.c}, next node {n} pathId {pathId}"

    member val G = G
    member val truncatedFront: TruncatedFront array = initializeTruncatedFront ()
    member val permanentCounter: UInt32 array = Utils.fill (int G.nodesCount) (uint32 0)
    member val nextCandidateLabels: TMosp.List array = initializeCandidateLabels ()
    member val heapLabels: Dictionary<Node, Label> = Dictionary<Node, Label>() with set, get
    member val permanentLabels: Permanents = Permanents()
    member val sols = uint16 0 with set, get
    member val maxHeapLabelsSize = uint32 0 with set, get
    member val solutions = SolutionsList() with set, get
    static member val iterations = 0 with get, set
    static member val permanents = 0 with get, set
    static member val maxHeapSize = 0 with get, set

    member inline this.dominatedAtTarget(l: Label) =
        if this.sols = l.knownTargetElements then
            false
        else
            let dominated = this.truncatedFront[int this.G.target].truncatedDominance l.c

            l.knownTargetElements <- this.sols
            dominated

    member this.nextQueuePath (minLabel: Label) (currentFront: TruncatedFront) (heap: BinaryHeapMosp) =
        let n = minLabel.n
        let incomingArcs = this.G.incomingArcs n
        let mutable newLabel: Label option = None
        let mutable success = false
        let mutable minCandidateLabels: TMosp.List option = None
        let mutable minCandidateLabelsArcId: ArcId option = None

        for a in incomingArcs do
            let candidateLabels = this.nextCandidateLabels[int a.id]

            if not candidateLabels.empty then
                if verbose then
                    printfn $"nextQueuePath: candidateLabels of arcid {a.id}, candidateLabels {candidateLabels.size}"
                    candidateLabels.print ()

                let mutable l = candidateLabels.first

                while l.IsSome do

                    if verbose then
                        printfn
                            $"nextQueuePath: l.Value node {l.Value.n}, pathId {l.Value.pathId}, {CostArray.toString l.Value.c}"

                    if newLabel.IsSome && not (CostArray.lexSmaller l.Value.c newLabel.Value.c) then
                        if
                            CostArray.dominates newLabel.Value.c l.Value.c
                            || CostArray.dominates minLabel.c l.Value.c
                        then
                            candidateLabels.pop_front ()

                            if verbose then
                                printfn $"nextQueuePath: pop_front of arcid {a.id} at line {__LINE__}"
                                candidateLabels.print ()

                        l <- None

                    if l.IsSome then
                        if not (CostArray.dominates minLabel.c l.Value.c) then
                            if not (this.dominatedAtTarget l.Value) then
                                if
                                    not l.Value.nclChecked
                                    && this.permanentCounter[int n] - uint32 l.Value.knownPermanentElements = uint32 1
                                then
                                    l.Value.nclChecked <- true

                                if l.Value.nclChecked || not (currentFront.truncatedDominance l.Value.c) then
                                    l.Value.nclChecked <- true
                                    l.Value.knownTargetElements <- this.sols
                                    success <- true
                                    newLabel <- l
                                    minCandidateLabels <- Some candidateLabels
                                    minCandidateLabelsArcId <- Some a.id
                                    l <- None

                        if l.IsSome then
                            candidateLabels.pop_front ()

                            if verbose then
                                printfn $"nextQueuePath: pop_front of arcid {a.id} at line {__LINE__}"
                                candidateLabels.print ()

                            l <- candidateLabels.first

        if success then
            heap.push newLabel.Value

            if newLabel.Value.n <> n then
                invalidArg "newLabel.Value.n" $"expected {newLabel.Value.n} = {n}"

            this.heapLabels.[n] <- newLabel.Value

            if verbose then
                printLabel "nextQueuePath: push label" newLabel.Value

            match minCandidateLabels with
            | Some minCandidateLabels ->
                minCandidateLabels.pop_front ()

                if verbose then
                    let id = minCandidateLabelsArcId |> Option.defaultValue (uint32 0)
                    printfn $"nextQueuePath: pop_front of arcid {id} at line {__LINE__}"
                    minCandidateLabels.print ()
            | None -> ()
        else
            this.heapLabels.Remove n |> ignore

            if verbose then
                printfn $"nextQueuePath: remove heap label node {n}"

    member this.propagate (minLabel: Label) (source_n_costs: CostArray) (heap: BinaryHeapMosp) =
        let n = minLabel.n
        let outgoingArcs = this.G.outgoingArcs (n)
        let predPathIndex = this.permanentLabels.getCurrentIndex
        let mutable expanded = false

        if verbose then
            printfn $"propagate: minLabel node {minLabel.n} predPathIndex {predPathIndex}"

        for a in outgoingArcs do
            let mutable costVector = Array.copy source_n_costs
            let successorNode = a.n

            if verbose then
                printfn $"propagate: check successorNode {successorNode} of arc {a.id}"

            CostArray.addInPlace costVector a.c
            expanded <- true
            let found, it = this.heapLabels.TryGetValue successorNode
            //Check if there is a label for the current successor node already in the heap.
            if found then
                let successorLabel = it

                if verbose then
                    printfn $"propagate: found label of successorNode {successorNode}"

                if CostArray.lexSmaller costVector successorLabel.c then
                    if
                        this.truncatedFront[int this.G.target].truncatedDominance costVector
                        || this.truncatedFront[int successorNode].truncatedDominance costVector
                    then
                        if verbose then
                            printfn $"truncatedDominance {CostArray.toString costVector} at line {__LINE__}"

                        () // continue
                    else
                        expanded <- true

                        let l =
                            Label(
                                costVector,
                                successorNode,
                                uint32 a.revArcIndex,
                                predPathIndex,
                                this.sols,
                                this.permanentCounter[int successorNode]
                            )

                        this.heapLabels[successorNode] <- l
                        heap.decreaseKey (successorLabel, l)

                        if CostArray.dominatesLexSmaller costVector successorLabel.c then
                            if verbose then
                                printfn $"dominatesLexSmaller {CostArray.toString costVector} at line {__LINE__}"

                            () // continue
                        else
                            let revArc = this.G.incomingArcs(successorNode)[int successorLabel.predArcId]

                            if verbose then
                                printNextCandidateLabel "propagate: push_front" revArc successorLabel

                            this.nextCandidateLabels[int revArc.id].push_front successorLabel

                            if verbose then
                                this.nextCandidateLabels[int revArc.id].print ()

                else if CostArray.dominatesLexSmaller successorLabel.c costVector then

                    if verbose then
                        printfn $"dominatesLexSmaller {CostArray.toString costVector} at line {__LINE__}"

                    () // continue
                else
                    expanded <- true
                    let revArc = this.G.incomingArcs(successorNode)[int a.revArcIndex]

                    let l =
                        Label(costVector, successorNode, uint32 a.revArcIndex, predPathIndex, uint16 0, uint32 0)

                    if verbose then
                        printNextCandidateLabel "propagate: push_back" revArc l

                    this.nextCandidateLabels[int revArc.id].push_back l

                    if verbose then
                        this.nextCandidateLabels[int revArc.id].print ()
            else if
                this.truncatedFront[int this.G.target].truncatedDominance costVector
                || this.truncatedFront[int successorNode].truncatedDominance costVector
            then
                if verbose then
                    printfn $"truncatedDominance {CostArray.toString costVector} at line {__LINE__}"

                () // continue
            else
                expanded <- true

                let successorLabel =
                    Label(
                        costVector,
                        successorNode,
                        uint32 a.revArcIndex,
                        predPathIndex,
                        this.sols,
                        this.permanentCounter[int successorNode]
                    )

                this.heapLabels[successorNode] <- successorLabel
                heap.push successorLabel

                if verbose then
                    printLabel "propagate: push label" successorLabel

        if expanded then
            this.permanentLabels.addElement (minLabel.pathId, minLabel.predArcId)

            if verbose then
                printfn
                    $"propagate: permanentLabels.addElement node {minLabel.n}, currentindex {this.permanentLabels.getCurrentIndex}"

        if this.heapLabels.Count > int this.maxHeapLabelsSize then
            this.maxHeapLabelsSize <- uint32 this.heapLabels.Count

    member this.run(maxSols: int) =
        let mutable target = G.target

        let mutable startLabel =
            Label(potential[int G.source], G.source, Constants.INVALID_ARC, Constants.MAX_PATH, uint16 0, uint32 0)

        let heap = BinaryHeapMosp(uint64 2000)
        heap.push startLabel
        this.heapLabels[G.source] <- startLabel

        if verbose then
            printLabel "run: push label" startLabel

        while heap.size <> 0 && this.solutions.solutions.Length < maxSols do
            let minLabel = heap.pop ()

            if verbose then
                printLabel "run: pop label" minLabel

            let n = minLabel.n

            let currentFront = this.truncatedFront[int n].truncatedInsertion minLabel.c

            if verbose then
                printfn $"run: truncatedInsertion of node {n}"
                currentFront.print ()

            this.nextQueuePath minLabel currentFront heap
            TargetedMDA.iterations <- TargetedMDA.iterations + 1

            if n = target then

                if verbose then
                    printLabel $"run: solution {this.solutions.solutions.Length} label" minLabel
                    printfn ""

                this.solutions.push_back minLabel
                this.sols <- this.sols + (uint16 1)
            else
                let source_n_costs = CostArray.substract (minLabel.c, potential[int n])
                this.propagate minLabel source_n_costs heap

        TargetedMDA.permanents <- int (this.permanentLabels.size ())
        TargetedMDA.maxHeapSize <- heap.maxSize

    member this.getSolutions() : ((UInt32 * CostArray) array) array =
        this.solutions.solutions
        |> Array.map (fun label ->
            let solution = label
            let mutable path: (Node * CostArray) array = [||]
            let mutable pathId = solution.pathId
            let mutable lastArcId = solution.predArcId
            let mutable costs = solution.c
            let mutable n = solution.n

            while pathId <> Constants.MAX_PATH do
                path <- Array.append [| n, costs |] path
                let predArc = G.incomingArcs(n)[int lastArcId]
                costs <- CostArray.substract (costs, predArc.c)
                n <- predArc.n
                let fst, snd = this.permanentLabels.getElement (pathId)
                pathId <- fst
                lastArcId <- snd

            path <- Array.append [| n, costs |] path

            path |> Array.map (fun (n, c) -> n, c))
