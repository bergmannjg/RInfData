module Test

open System.IO
open System.Text.Json

open NUnit.Framework

open RInfGraph

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

let TestPath (ids: string[]) (expectedPath: (string * string * int)[]) (_: int) =
    Assert.That(ids.Length = 2)
    let dir = "../../../../../erakg-data/"

    let g = readFile<GraphNode[]> dir "Graph.json"
    let moPaths = MoGraph.getShortestPath g ids[0] ids[1] 5
    Assert.That(0 < moPaths.Length)
    let moFewestLinesPath = Graph.getCompactPath moPaths[0].Path

    printfn $"Path: length {moFewestLinesPath.Length}"
    Graph.printPath moFewestLinesPath

    printfn $"Expected Path: length {expectedPath.Length}"
    expectedPath |> Array.iter (fun (f, t, _) -> printfn $"{f}, {t}")

    Assert.That((expectedPath.Length = moFewestLinesPath.Length))

    Array.iter2
        (fun (fromNode, toNode, line) (graphNode: GraphNode) ->
            Assert.That((fromNode = graphNode.Node))
            Assert.That((toNode = graphNode.Edges.[0].Node))
            Assert.That((line.ToString() = graphNode.Edges.[0].Line)))
        expectedPath
        moFewestLinesPath

    let path = Graph.getShortestPath g ids |> Graph.getCompactPath
    let moShortestPath = Graph.getCompactPath moPaths[moPaths.Length - 1].Path
    printfn $"shortestPath length {path.Length}, moShortestPath length {moShortestPath.Length}"   
    Assert.That(path.Length >= moShortestPath.Length)

[<Test>]
let TestHHToFFU () =
    TestPath [| "DE000HH"; "DE00FFU" |] [| ("DE000HH", "DE00FFU", 1733) |] 15245

[<Test>]
let TestHHToFF () =
    TestPath [| "DE000HH"; "DE000FF" |] [| "DE000HH", "DE00FFU", 1733; "DE00FFU", "DE000FF", 3600 |] 15245

[<Test>]
let TestRKToRF () =
    TestPath [| "DE000RK"; "DE000RF" |] [| "DE000RK", "DE000RF", 4000 |] 15245

[<Test>]
let TestFFUToFF () =
    TestPath [| "DE00FFU"; "DE000FF" |] [| ("DE00FFU", "DE000FF", 3600) |] 15245

[<Test>]
let TestRMToRK () =
    TestPath [| "DE000RM"; "DE000RK" |] [| ("DE000RM", "DE000RK", 4020) |] 15245


[<Test>]
let TestHHToNN () =
    TestPath
        [| "DE000HH"; "DE000NN" |]
        [| "DE000HH", "DE00NWH", 1733
           "DE00NWH", "DE000NF", 5910
           "DE000NF", "DE000NN", 5900 |]
        18167

[<Test>]
let TestHHToAH () =
    TestPath
        [| "DE000HH"; "DE000AH" |]
        [| "DE000HH", "DE95366", 1710
           "DE95366", "DE0AHAR", 1720
           "DE0AHAR", "DE000AH", 2200 |]
        9172

[<Test>]
let TestAHToHH () =
    TestPath
        [| "DE000AH"; "DE000HH" |]
        [| "DE000AH", "DE0AHAR", 2200
           "DE0AHAR", "DE95366", 1720
           "DE95366", "DE000HH", 1710 |]
        9172
