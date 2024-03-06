module RInfGraph.Tests

open System.IO
open System.Text.Json

open NUnit.Framework

open RInfGraph

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

type Source =
    | Rinfdata
    | EraKGdata

let TestPath (source: Source) (ids: string[]) (expectedPath: (string * string * int)[]) (expectedCost: int) =
    let dir =
        if source = Source.Rinfdata then
            "../../../../../rinf-data/"
        else
            "../../../../../erakg-data/"

    let g = readFile<GraphNode[]> dir "Graph.json"
    let path = Graph.getShortestPath g ids
    let cpath = Graph.compactifyPath path g
    let ccpath = Graph.getCompactPath cpath

    printfn "Path:"
    Graph.printPath ccpath

    let cost = Graph.costOfPath ccpath
    printfn "Cost: %d" cost

    if source = Source.Rinfdata then
        Assert.AreEqual(expectedCost, cost)

    Assert.AreEqual(expectedPath.Length, ccpath.Length)

    Array.iter2
        (fun (fromNode, toNode, line) graphNode ->
            Assert.AreEqual(fromNode, graphNode.Node)
            Assert.AreEqual(toNode, graphNode.Edges.[0].Node)
            Assert.AreEqual(line.ToString(), graphNode.Edges.[0].Line))
        expectedPath
        ccpath

let sources = [ Source.EraKGdata ]

let switch (source: Source) =
    if source = Source.Rinfdata then "DE00FFU" else "DE95441" // op new in era kg

[<TestCaseSource(nameof (sources))>]
let TestHHToFFU (source: Source) =
    TestPath
        source
        [| "DE000HH"; "DE00FFU" |]
        [| ("DE000HH", "DE00FFU", 1733) |]
        15245

[<TestCaseSource(nameof (sources))>]
let TestFFUToFF (source: Source) =
    TestPath
        source
        [| "DE00FFU"; "DE000FF" |]
        [| ("DE00FFU", "DE000FF", 3600) |]
        15245

[<TestCaseSource(nameof (sources))>]
let TestHHToNN (source: Source) =
    TestPath
        source
        [| "DE000HH"; "DE000NN" |]
        [| 
           // ("DE000HH", "DE00NWH", 1733)
           // ("DE00NWH", "DE000NF", 5910)
           // ("DE000NF", "DE000NN", 5900)
           
           ("DE000HH", "DE00NWH", 1733)
           ("DE00NWH", "DE97637", 5209)
           ("DE97637", "DE0NRTD", 5102)
           ("DE0NRTD", "DE000NF", 5910)
           ("DE000NF", "DENF  G", 5972)
           ("DENF  G", "DE000NN", 5900)
            |]
        18167

[<TestCaseSource(nameof (sources))>]
let TestHHToAH (source: Source) =
    TestPath
        source
        [| "DE000HH"; "DE000AH" |]
        [| ("DE000HH", "DE95366", 1710)
           ("DE95366", "DE0AHAR", 1720)
           ("DE0AHAR", "DE000AH", 2200) |]
        9172

[<TestCaseSource(nameof (sources))>]
let TestAHToHH (source: Source) =
    TestPath
        source
        [| "DE000AH"; "DE000HH" |]
        [| ("DE000AH", "DE0AHAR", 2200)
           ("DE0AHAR", "DE95366", 1720)
           ("DE95366", "DE000HH", 1710) |]
        9172
