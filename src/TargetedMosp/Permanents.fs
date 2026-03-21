namespace TMosp

open System
open Microsoft.FSharp.Collections

type internal PredLabel = UInt32 * UInt32

type internal Permanents() =
    let IncrSize = 5000
    member val private elements: PredLabel array = Utils.resize IncrSize with set, get
    member val private currentIndex = uint32 0 with set, get
    member this.size() = this.currentIndex

    member private this.increaseIndex() =
        if int this.currentIndex + 1 = this.elements.Length then
            let mutable tmp = this.elements
            Array.Resize(&tmp, this.elements.Length + IncrSize)
            this.elements <- tmp

        this.currentIndex <- this.currentIndex + uint32 1

    member this.addElement(predIndex: UInt32, predArcId: ArcId) =
        this.elements[int this.currentIndex] <- (predIndex, predArcId)
        this.increaseIndex ()

    member this.getElement(index: UInt32) : PredLabel = this.elements[int index]

    member this.getCurrentIndex: UInt32 = this.currentIndex
