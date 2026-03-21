namespace TMosp

open System

type Arc(n: Node, c: CostArray, id: ArcId) =
    member val n = n with get, set
    member val c = c with get, set
    member val revArcIndex: NeighborhoodSize = UInt16.MaxValue with get, set
    member val id = id with get, set

    member this.print() =
        printfn $"Node {this.n}, id {this.id}, Arc {CostArray.toString this.c}"

type internal Neighborhood = Arc array

type internal NodeAdjacency(inDegree: NeighborhoodSize, outDegree: NeighborhoodSize, id: Node) =
    member val incomingArcs: Neighborhood = Utils.resize (int inDegree) with get, set
    member val outgoingArcs: Neighborhood = Utils.resize (int outDegree) with get, set
    member val id: Node = id with get, set

    member this.print() =

        ()

type internal Graph(nodesCount: Node, arcsCount: ArcId, source: Node, target: Node) =
    static member countNodes(arcs: (Node * Arc) array) =
        Array.append (arcs |> Array.map (fun (n, _) -> n)) (arcs |> Array.map (fun (_, a) -> a.n))
        |> Array.distinct
        |> Array.length
        |> uint32

    member val nodesCount = nodesCount
    member val arcsCount = arcsCount
    member val source = source
    member val target = target
    member this.outgoingArcs(nodeId: Node) = this.nodes[int nodeId].outgoingArcs
    member this.incomingArcs(nodeId: Node) = this.nodes[int nodeId].incomingArcs
    member this.node(nodeId: Node) = this.nodes[int nodeId]
    member val name: string = "" with get, set
    member val private nodes: NodeAdjacency array = Utils.resize (int nodesCount) with get, set

    member this.printNodeInfo(nodeId: Node) =
        printfn $"node {nodeId}"
        printfn $"outgoingArcs"
        Graph.printArcs (this.outgoingArcs nodeId)
        printfn $"incomingArcs"
        Graph.printArcs (this.incomingArcs nodeId)

    member this.print() =
        for i = 0 to int this.nodesCount - 1 do
            this.printNodeInfo (uint32 i)

    member this.setNodeInfo(n: Node, inDegree: NeighborhoodSize, outDegree: NeighborhoodSize) =
        this.nodes[int n] <- NodeAdjacency(inDegree, outDegree, n)

    static member printArcs(arcs: Neighborhood) =
        for arc in arcs do
            arc.print ()

    static member fromArcs(arcs: (Node * Arc) array, nodesCount: UInt32, sourceId: Node, targetId: Node) =
        if nodesCount <= sourceId then
            invalidArg "sourceId" $"sourceId {sourceId} < nodesCount {nodesCount} expected"

        if nodesCount <= targetId then
            invalidArg "targetId" $"targetId {targetId} < nodesCount {nodesCount} expected"

        let inDegree: NeighborhoodSize array = Utils.fill (int nodesCount) (uint16 0)

        let outDegree: NeighborhoodSize array = Utils.fill (int nodesCount) (uint16 0)

        for tailId, arc in arcs do
            outDegree[int tailId] <- outDegree[int tailId] + (uint16 1)
            inDegree[int arc.n] <- inDegree[int arc.n] + (uint16 1)

        let G = Graph(uint32 nodesCount, uint32 arcs.Length, sourceId, targetId)

        for i = 0 to int nodesCount - 1 do
            G.setNodeInfo (uint32 i, inDegree[i], outDegree[i])

        let incomingArcsPerNode: NeighborhoodSize array =
            Utils.fill (int nodesCount) (uint16 0)

        let outgoingArcsPerNode: NeighborhoodSize array =
            Utils.fill (int nodesCount) (uint16 0)

        for tailId, arc in arcs do
            if arc.c.Length <> int Constants.DIM then
                invalidArg "arc.c" $"CostArray length {Constants.DIM} expected"

            let mutable tail = G.node tailId
            let mutable head = G.node arc.n
            tail.id <- tailId
            head.id <- arc.n
            arc.revArcIndex <- incomingArcsPerNode[int head.id]
            tail.outgoingArcs[int outgoingArcsPerNode[int tail.id]] <- arc
            outgoingArcsPerNode[int tail.id] <- outgoingArcsPerNode[int tail.id] + (uint16 1)
            let arc = Arc(arc.n, Array.copy arc.c, arc.id)
            arc.n <- tail.id
            arc.revArcIndex <- outgoingArcsPerNode[int tail.id] - (uint16 1)
            head.incomingArcs[int incomingArcsPerNode[int head.id]] <- arc
            incomingArcsPerNode[int head.id] <- incomingArcsPerNode[int head.id] + (uint16 1)

        for i = 0 to int nodesCount - 1 do
            if inDegree[i] <> incomingArcsPerNode[i] then
                invalidArg "inDegree[i]" "value unexpected"

            if outDegree[i] <> outgoingArcsPerNode[i] then
                invalidArg "outDegree[i]" "value unexpected"

        G

    static member fromLines(lines: string array, sourceId: Node, targetId: Node) =
        let mutable nodesCount = uint32 0
        let mutable arcsCount = uint32 0
        let mutable i = 0

        while i < lines.Length && nodesCount = uint32 0 && arcsCount = uint32 0 do
            let line = lines[i]

            if line.StartsWith "p " then
                let splits = line.Split [| ' ' |]

                if splits.Length = 4 then
                    nodesCount <- System.UInt32.Parse(splits[2])
                    arcsCount <- System.UInt32.Parse(splits[3])

            i <- i + 1

        if nodesCount = uint32 0 || arcsCount = uint32 0 then
            nullArg "nodesCount or arcsCount is 0"

        let arcs: (Node * Arc) array = Utils.resize (int arcsCount)
        let mutable addedArcs = uint32 0

        while i < lines.Length do
            let line = lines[i]

            if line.StartsWith "a " then
                let splits = line.Split [| ' ' |]

                if 3 <= splits.Length then
                    let tailId = System.UInt32.Parse(splits[1])
                    let headId = System.UInt32.Parse(splits[2])

                    let mutable arcCost = Array.create<UInt32> 2 (uint32 0)

                    if 5 <= splits.Length then
                        arcCost[0] <- System.UInt32.Parse(splits[3])
                        arcCost[1] <- System.UInt32.Parse(splits[4])

                    if 6 = splits.Length then
                        Array.Resize(&arcCost, 3)
                        arcCost[2] <- System.UInt32.Parse(splits[5])
                    else if 6 < splits.Length then
                        invalidArg "CostArray DIM" "only CostArray DIM 2 or 3 is supported"

                    arcs[int addedArcs] <- tailId, Arc(headId, arcCost, addedArcs)
                    addedArcs <- addedArcs + uint32 1

            i <- i + 1

        if addedArcs <> arcsCount then
            invalidArg "arcsCount" $"addedArcs {addedArcs} <> arcsCount {arcsCount}"

        let addedNodes = Graph.countNodes arcs

        if addedNodes <> nodesCount then
            invalidArg "nodesCount" $"addedNodes {addedNodes} <> nodesCount {nodesCount}"

        Graph.fromArcs (arcs, addedNodes, sourceId, targetId)

#if FABLE_COMPILER
#else
    static member fromFile(path: string, sourceId: Node, targetId: Node) =
        Graph.fromLines (System.IO.File.ReadAllLines path, sourceId, targetId)
#endif
