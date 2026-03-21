namespace TMosp

open System

type internal SolutionsList() =
    member val solutions: Label array = [||] with set, get

    member this.push_back(l: Label) =
        this.solutions <- Array.append this.solutions [| l |]
