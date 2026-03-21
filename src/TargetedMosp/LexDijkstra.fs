namespace TMosp

open System

// Classical MIN binary heap implementation including a decreaseKey operation
type internal BinaryHeapPreprocessing(size: int, dimOrdering: DimensionsVector) =
    member val private elementsContainer: NodeInfo array = Utils.resize size with set, get
    member val private comparator = (PotentialComparison dimOrdering).lexSmaller
    member val private lastElementIndex = uint32 0 with set, get
    member val private mainOptIndex = dimOrdering[0]
    member this.size() = this.lastElementIndex

    member this.decreaseKey(value: NodeInfo) =
        if not (value.get_priority (this.mainOptIndex) < this.lastElementIndex) then
            nullArg "mainOptIndex"

        this.heapifyUp (value.get_priority this.mainOptIndex)

    member this.push(value: NodeInfo) =
        if int this.lastElementIndex + 1 > this.elementsContainer.Length then
            let mutable tmp = this.elementsContainer
            Array.Resize(&tmp, this.elementsContainer.Length * 2)
            this.elementsContainer <- tmp

        let priority = this.lastElementIndex
        this.elementsContainer[int priority] <- value
        value.set_priority (this.mainOptIndex, priority)
        this.lastElementIndex <- this.lastElementIndex + uint32 1
        this.heapifyUp priority

    member this.pop() : NodeInfo =
        if this.lastElementIndex = uint32 0 then
            nullArg "lastElementIndex"

        let ans = this.elementsContainer[0]
        this.lastElementIndex <- this.lastElementIndex - uint32 1

        if 0 < int this.lastElementIndex then
            this.elementsContainer[0] <- this.elementsContainer[int this.lastElementIndex]
            this.elementsContainer[0].set_priority (this.mainOptIndex, uint32 0)
            this.heapifyDown (uint32 0)

        ans

    member this.contains(n: NodeInfo) =
        let priority = n.get_priority this.mainOptIndex

        if priority < this.lastElementIndex && n = this.elementsContainer[int priority] then
            true
        else
            false

    member private this.heapifyUp(index: uint32) =
        if not (index < this.lastElementIndex) then
            nullArg "lastElementIndex"

        let mutable index = index

        while int index > 0 do
            let parent = index - uint32 1 >>> 1 //Shift right dividing by 2

            if this.comparator this.elementsContainer[int index] this.elementsContainer[int parent] then
                this.swap (parent, index)
                index <- parent
            else
                index <- uint32 0

    member private this.heapifyDown(index: uint32) =
        let first_leaf_index = this.lastElementIndex >>> 1
        let mutable index = index

        while index < first_leaf_index do
            let child1 = (index <<< 1) + uint32 1
            let child2 = (index <<< 1) + uint32 2
            let mutable swapCandidate = child1

            if
                child2 < this.lastElementIndex
                && this.comparator this.elementsContainer[int child2] this.elementsContainer[int child1]
            then
                swapCandidate <- child2

            if this.comparator this.elementsContainer[int swapCandidate] this.elementsContainer[int index] then
                this.swap (index, swapCandidate)
                index <- swapCandidate
            else
                index <- first_leaf_index

    member private this.swap(id1: uint32, id2: uint32) =
        let tempElement = this.elementsContainer[int id1]
        this.elementsContainer[int id1] <- this.elementsContainer[int id2]
        this.elementsContainer[int id2] <- tempElement
        this.elementsContainer[int id1].set_priority (this.mainOptIndex, id1)
        this.elementsContainer[int id2].set_priority (this.mainOptIndex, id2)


type internal LexDijkstra(G: Graph, tracker: NodeInfoContainer) =
    member val private G = G
    member val private tracker = tracker

    member this.run(dimOrdering: DimensionsVector) : CostArray =
        let firstCostComponent = dimOrdering[0]

        let sumCosts = fun (rhs, lhs) -> CostArray.addWithDimOrdering rhs lhs dimOrdering

        let heapNew = BinaryHeapPreprocessing(1, dimOrdering)
        let startNode = this.tracker.getInfo G.target
        startNode.preprocessingResult[int dimOrdering[0]] <- CostArray.generateWith (uint32 0)
        let targetNode = this.tracker.getInfo G.source
        let mutable targetCosts = CostArray.generate ()
        heapNew.push startNode

        while uint32 0 < heapNew.size () do
            let minElement = heapNew.pop ()
            minElement.reached[int firstCostComponent] <- true
            minElement.potential[int firstCostComponent] <- minElement.preprocessingResult[int firstCostComponent][0]
            let s_t_costs = minElement.preprocessingResult[int firstCostComponent]

            if minElement.n = targetNode.n then
                targetCosts <- Array.copy s_t_costs

            let outgoingArcs = G.incomingArcs minElement.n

            for a in outgoingArcs do
                let successorNode = a.n
                let successorInfo = this.tracker.getInfo successorNode

                if not successorInfo.reached[int firstCostComponent] then
                    let expandedCosts = sumCosts (s_t_costs, a.c)

                    if heapNew.contains successorInfo then
                        let currentSuccessorCosts =
                            successorInfo.preprocessingResult[int firstCostComponent]

                        if CostArray.lexSmaller expandedCosts currentSuccessorCosts then
                            successorInfo.updatePreprocessingInfo (dimOrdering, expandedCosts)
                            heapNew.decreaseKey successorInfo
                    else
                        successorInfo.updatePreprocessingInfo (dimOrdering, expandedCosts)
                        heapNew.push successorInfo

        targetCosts
