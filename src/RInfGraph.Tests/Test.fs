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
        Assert.That((expectedCost = cost))

    Assert.That((expectedPath.Length = ccpath.Length))

    Array.iter2
        (fun (fromNode, toNode, line) graphNode ->
            Assert.That((fromNode = graphNode.Node))
            Assert.That((toNode = graphNode.Edges.[0].Node))
            Assert.That((line.ToString() = graphNode.Edges.[0].Line)))
        expectedPath
        ccpath

let sources = [ Source.EraKGdata ]

let switch (source: Source) =
    if source = Source.Rinfdata then "DE00FFU" else "DE95441" // op new in era kg

[<TestCaseSource(nameof (sources))>]
let TestHHToFFU (source: Source) =
    TestPath source [| "DE000HH"; "DE00FFU" |] [| ("DE000HH", "DE00FFU", 1733) |] 15245

[<TestCaseSource(nameof (sources))>]
let TestHHToFF (source: Source) =
    TestPath source [| "DE000HH"; "DE000FF" |] [| "DE000HH", "DE00FFU", 1733; "DE00FFU", "DE000FF", 3600 |] 15245

[<TestCaseSource(nameof (sources))>]
let TestRKToRF (source: Source) =
    TestPath source [| "DE000RK"; "DE000RF" |] [| "DE000RK", "DE00RRA", 4020; "DE00RRA", "DE000RF", 4000 |] 15245

[<TestCaseSource(nameof (sources))>]
let TestFFUToFF (source: Source) =
    TestPath source [| "DE00FFU"; "DE000FF" |] [| ("DE00FFU", "DE000FF", 3600) |] 15245

[<TestCaseSource(nameof (sources))>]
let TestRMToRK (source: Source) =
    TestPath source [| "DE000RM"; "DE000RK" |] [| ("DE000RM", "DE000RK", 4020) |] 15245


[<TestCaseSource(nameof (sources))>]
let TestHHToNN (source: Source) =
    TestPath
        source
        [| "DE000HH"; "DE000NN" |]
        [| ("DE000HH", "DE00NWH", 1733)
           ("DE00NWH", "DE000NF", 5910)
           ("DE000NF", "DE000NN", 5900) |]
        18167

[<TestCaseSource(nameof (sources))>]
let TestHHToAH (source: Source) =
    TestPath
        source
        [| "DE000HH"; "DE000AH" |]
        [| "DE000HH", "DE95366", 1710
           "DE95366", "DE95173", 1720
           "DE95173", "DE95174", 1259
           "DE95174", "DE000AH", 2200 |]
        9172

[<TestCaseSource(nameof (sources))>]
let TestAHToHH (source: Source) =
    TestPath
        source
        [| "DE000AH"; "DE000HH" |]
        [| "DE000AH", "DE95174", 2200
           "DE95174", "DE95173", 1259
           "DE95173", "DE95366", 1720
           "DE95366", "DE000HH", 1710 |]
        9172
