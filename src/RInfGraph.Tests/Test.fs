module RInfGraph.Tests

open System.IO
open System.Text.Json

open NUnit.Framework

open RInfGraph

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let TestPath (ids: string []) (expectedPath: (string * string * int) []) =
    let dir = "../../../../../rinf-data/"
    let g = readFile<GraphNode []> dir "Graph.json"
    let path = Graph.getShortestPath g ids
    let cpath = Graph.compactifyPath path g
    let ccpath = Graph.getCompactPath cpath

    printfn "Path:"
    Graph.printPath ccpath

    Assert.AreEqual(expectedPath.Length, ccpath.Length)

    Array.iter2
        (fun (fromNode, toNode, line) graphNode ->
            Assert.AreEqual(fromNode, graphNode.Node)
            Assert.AreEqual(toNode, graphNode.Edges.[0].Node)
            Assert.AreEqual(line.ToString(), graphNode.Edges.[0].Line))
        expectedPath
        ccpath

[<Test>]
let TestHHToFF () =

    TestPath [| "DE000HH"; "DE000FF" |] [|
        ("DE000HH", "DE00FFU", 1733)
        ("DE00FFU", "DE000FF", 3600)
    |]

[<Test>]
let TestHHToNN () =

    TestPath [| "DE000HH"; "DE000NN" |] [|
        ("DE000HH", "DE00NWH", 1733)
        ("DE00NWH", "DE000NF", 5910)
        ("DE000NF", "DENF  G", 5972)
        ("DENF  G", "DE000NN", 5900)
    |]

[<Test>]
let TestHHToAH () =

    TestPath [| "DE000HH"; "DE000AH" |] [|
        ("DE000HH", "DE95366", 1710)
        ("DE95366", "DE0AHAR", 1720)
        ("DE0AHAR", "DE000AH", 2200)
    |]
