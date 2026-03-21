namespace TMosp

open System

type internal BinaryHeapMosp(size: UInt64) =
    member val lastElementIndex: int = 0 with get, set
    member val heapElements: Label array = Utils.resize (int size) with get, set
    member val elemCount: int = 0 with get, set
    member val maxElemCount: int = 0 with get, set
    member this.size = this.lastElementIndex
    member this.maxSize = this.maxElemCount
    static member val pushes = 0 with get, set

    member this.print() =
        for i = 0 to this.lastElementIndex - 1 do
            printfn $"BinaryHeapMosp {i}, node {this.heapElements[i].n}, {CostArray.toString this.heapElements[i].c}"

    member this.decreaseKey(lOld: Label, lNew: Label) =
        lNew.priority <- lOld.priority
        this.heapElements[int lNew.priority] <- lNew
        this.up lNew.priority

    member inline this.push(lNew: Label) =
        if int this.lastElementIndex + 1 > this.heapElements.Length then
            let mutable tmp = this.heapElements
            Array.Resize(&tmp, this.heapElements.Length * 2)
            this.heapElements <- tmp

        let priority = this.lastElementIndex
        this.heapElements[int priority] <- lNew

        lNew.priority <- priority
        this.lastElementIndex <- this.lastElementIndex + 1
        this.up priority
        this.elemCount <- this.elemCount + 1

        if this.elemCount > this.maxElemCount then
            this.maxElemCount <- this.elemCount

        BinaryHeapMosp.pushes <- BinaryHeapMosp.pushes + 1

    member inline this.pop() =
        if this.lastElementIndex = 0 then
            nullArg "lastElementIndex"

        let minElement = this.heapElements[0]
        this.lastElementIndex <- this.lastElementIndex - 1

        if int this.lastElementIndex > 0 then
            this.heapElements[0] <- this.heapElements[int this.lastElementIndex]
            this.heapElements[0].priority <- 0
            this.down 0

        this.elemCount <- this.elemCount - 1
        minElement

    member this.up(index: int) =
        if not (index < this.lastElementIndex) then
            invalidArg "index" $"{index} < {this.lastElementIndex} expected"

        let mutable index = index

        while index > 0 do
            let parent = index - 1 >>> 1 //Shift right dividing by 2

            if Label.lexSmaller this.heapElements[index] this.heapElements[parent] then
                this.swap parent index
                index <- parent
            else
                index <- 0

    member this.down(index: int) =
        let first_leaf_index = this.lastElementIndex >>> 1
        let mutable index = index

        while index < first_leaf_index do
            let child1 = (index <<< 1) + 1
            let child2 = (index <<< 1) + 2
            let mutable swapCandidate = child1

            if
                child2 < this.lastElementIndex
                && Label.lexSmaller this.heapElements[child2] this.heapElements[child1]
            then
                swapCandidate <- child2

            if Label.lexSmaller this.heapElements[swapCandidate] this.heapElements[index] then
                this.swap swapCandidate index
                index <- swapCandidate
            else
                index <- first_leaf_index

    member this.swap (id1: int) (id2: int) =
        if not (id1 < this.lastElementIndex && id2 < this.lastElementIndex) then
            invalidArg "idx" $"{id1} < {this.lastElementIndex} && {id2} < {this.lastElementIndex} expected"

        let temp = this.heapElements[id1]
        this.heapElements[id1] <- this.heapElements[id2]
        this.heapElements[id2] <- temp
        this.heapElements[id1].priority <- id1
        this.heapElements[id2].priority <- id2
