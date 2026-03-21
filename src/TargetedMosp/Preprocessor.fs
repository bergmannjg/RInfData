namespace TMosp

open System

type internal Preprocessor(G: Graph) =
    // see https://dev.to/ducaale/computing-permutations-of-a-list-in-f-1n6k
    let permute list =
        let rec inserts e =
            function
            | [] -> [ [ e ] ]
            | x :: xs as list -> (e :: list) :: (inserts e xs |> List.map (fun xs' -> x :: xs'))

        List.fold (fun accum x -> List.collect (inserts x) accum) [ [] ] list
        |> fun l -> l |> List.sortBy (fun x -> x[0], x[1])

    member val private G = G
    member val targetUb: CostArray = CostArray.generateWith (uint32 0) with set, get
    member val potential: CostArray array = Utils.resize (int G.nodesCount) with set, get

    /// computes `targetUb` and `potential` and updates all arcs in `G` with corresponding potential
    member this.run(nic: NodeInfoContainer) =
        // //fill dimOrderings vector from 0 to dimOrdering.size()-1
        let dimOrdering: DimensionsVector =
            Utils.resize (int Constants.DIM) |> Array.mapi (fun i _ -> uint16 i)

        let processedMainDimensions: bool array = Utils.fill (int Constants.DIM) false
        let permutes = dimOrdering |> Array.toList |> permute |> List.map List.toArray

        permutes
        |> List.iter (fun dimOrdering ->
            if not processedMainDimensions[int dimOrdering[0]] then
                let dijkstra = LexDijkstra(this.G, nic)
                let result = dijkstra.run dimOrdering
                this.targetUb <- CostArray.max this.targetUb result dimOrdering
                processedMainDimensions[int dimOrdering[0]] <- true)

        for i = 0 to int G.nodesCount - 1 do
            for a in G.outgoingArcs (uint32 i) do
                let pi = nic.getInfo(a.n).potential
                a.c <- CostArray.add a.c pi

            this.potential[i] <- nic.getInfo(uint32 i).potential
