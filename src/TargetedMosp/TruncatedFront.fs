namespace TMosp

open FSharp.Core.CompilerServices

type internal TruncatedFront() =
    member val tcs: TruncatedCosts list = [] with get, set

    member inline this.truncatedDominance(c: CostArray) =
        this.tcs
        |> List.exists (fun f -> TruncatedCosts.lexSmallerOrEquiv f c && CostArray.tc_dominates f c)

    // Updates the front of truncated costs with the new cost vector c.
    member this.truncatedInsertion(c: CostArray) =
        let tc = CostArray.truncate c

        match this.tcs with
        | [] -> this.tcs <- [ tc ]
        | _ ->
            let mutable tcAdded = false
            let mutable coll = ListCollector()
            let mutable curr = this.tcs

            while not (List.isEmpty curr) do
                match curr with
                | [] -> nullArg "unexpected empty list"
                | h :: t ->
                    if not tcAdded then
                        if TruncatedCosts.lexSmaller h tc then
                            coll.Add h
                            curr <- t
                        else
                            coll.Add tc
                            tcAdded <- true
                    else if not (TruncatedCosts.tc_dominates tc h) then
                        coll.AddMany curr
                        curr <- []
                    else
                        curr <- t

            if not tcAdded then
                coll.Add tc

            this.tcs <- coll.Close()

        this

    member this.print() =
        this.tcs |> List.iter (fun f -> printfn $"{TruncatedCosts.toString f}")
