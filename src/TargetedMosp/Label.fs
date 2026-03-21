namespace TMosp

open System

type internal Label
    (c: CostArray, n: Node, predArcId: ArcId, pathId: UInt32, targetFrontSize: UInt16, permanents: UInt32) =
    static member val count = 0 with get, set

    member val c =
        Label.count <- Label.count + 1
        c with get, set

    member val n = n with get, set
    member val predArcId = predArcId with get
    member val pathId = pathId with get
    member val knownTargetElements = targetFrontSize with get, set
    member val knownPermanentElements = permanents with get, set
    member val priority: int = 0 with get, set
    member val next: Label option = None with get, set
    member val nclChecked = false with get, set

    static member inline lexSmaller (lh: Label) (rh: Label) = CostArray.lexSmaller lh.c rh.c

type internal List() =
    member val first: Label option = None with get, set
    member val last: Label option = None with get, set
    member val size: UInt32 = 0u with get, set
    member this.empty: bool = this.size = 0u

    member this.print() =
        let mutable curr = this.first
        let mutable count = 0

        printfn $"List size {this.size}"

        while curr.IsSome && count < 100 do
            let n =
                if curr.Value.next.IsSome then
                    curr.Value.next.Value.n
                else
                    (uint32 0)

            printfn $"List node {curr.Value.n} pathId {curr.Value.pathId} {CostArray.toString curr.Value.c} next {n}"

            curr <- curr.Value.next
            count <- count + 1

        if this.size <> uint32 count then
            invalidArg "List.size" $"expected {this.size} = {count}"

    member inline this.push_back(l: Label) =
        if this.empty then
            this.first <- Some l
            this.last <- Some l
            l.next <- None
        else
            if this.first.IsNone then
                nullArg "List.first"

            if this.last.IsNone then
                nullArg "List.last"

            this.last.Value.next <- Some l
            this.last <- Some l
            l.next <- None

        this.size <- this.size + 1u

    member inline this.push_front(l: Label) =
        if this.empty then
            this.first <- Some l
            this.last <- Some l
            l.next <- None
        else
            if this.first.IsNone then
                nullArg "List.first"

            if this.last.IsNone then
                nullArg "List.last"

            l.next <- this.first
            this.first <- Some l

        this.size <- this.size + 1u

    member inline this.pop_front() =
        if this.empty then
            ()
        else if this.size = 1u then
            this.first <- None
            this.last <- None
            this.size <- 0u
            ()
        else
            if this.first.IsNone then
                nullArg "List.first"

            if this.last.IsNone then
                nullArg "List.last"

            let front = this.first
            this.first <- front.Value.next

            this.size <- this.size - 1u
            ()
