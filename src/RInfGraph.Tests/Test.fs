module Test

open System.IO
open System.Text.Json

open NUnit.Framework

open RInfGraph

let readFile<'a> path name =
    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

[<TestFixture>]
type TestPaths() =
    let dir = "../../../../../erakg-data/"
    let g = readFile<GraphNode[]> dir "Graph.json"

    let hasCountry (country: string) =
        g
        |> Array.exists (fun n -> n.Edges |> Array.exists (fun edge -> edge.Country = country))

    let TestPath (ids: string[]) (expectedPath: (string * string * int)[]) (_: int) =
        Assert.That(ids.Length, Is.EqualTo 2)

        let moPaths = MoGraph.getShortestPath g ids[0] ids[1] 5
        Assert.That(0 < moPaths.Length)
        let moFewestLinesPath = Graph.getCompactPath moPaths[0].Path

        printfn $"Path: length {moFewestLinesPath.Length}"
        Graph.printPath moFewestLinesPath

        printfn $"Expected Path: length {expectedPath.Length}"
        expectedPath |> Array.iter (fun (f, t, _) -> printfn $"{f}, {t}")

        Assert.That(moFewestLinesPath.Length, Is.EqualTo expectedPath.Length)

        Array.iter2
            (fun ((fromNode: string), (toNode: string), line) (graphNode: GraphNode) ->
                Assert.That(graphNode.Node, Is.EqualTo fromNode)
                Assert.That(graphNode.Edges.[0].Node, Is.EqualTo toNode)
                Assert.That(graphNode.Edges.[0].Line, Is.EqualTo(line.ToString())))
            expectedPath
            moFewestLinesPath

        let path = Graph.getShortestPath g ids |> Graph.getCompactPath
        let moShortestPath = Graph.getCompactPath moPaths[moPaths.Length - 1].Path
        printfn $"shortestPath length {path.Length}, moShortestPath length {moShortestPath.Length}"
        Assert.That(path.Length >= moShortestPath.Length)

    [<Test>]
    member _.TestHHToFFU() =
        TestPath [| "DE000HH"; "DE00FFU" |] [| ("DE000HH", "DE00FFU", 1733) |] 15245

    [<Test>]
    member _.TestHHToFF() =
        TestPath [| "DE000HH"; "DE000FF" |] [| "DE000HH", "DE00FFU", 1733; "DE00FFU", "DE000FF", 3600 |] 15245

    [<Test>]
    member _.TestRKToRF() =
        TestPath [| "DE000RK"; "DE000RF" |] [| "DE000RK", "DE000RF", 4000 |] 15245

    [<Test>]
    member _.TestFFUToFF() =
        TestPath [| "DE00FFU"; "DE000FF" |] [| ("DE00FFU", "DE000FF", 3600) |] 15245

    [<Test>]
    member _.TestRMToRK() =
        TestPath [| "DE000RM"; "DE000RK" |] [| ("DE000RM", "DE000RK", 4020) |] 15245


    [<Test>]
    member _.TestHHToNN() =
        TestPath
            [| "DE000HH"; "DE000NN" |]
            [| "DE000HH", "DE00NWH", 1733
               "DE00NWH", "DE000NF", 5910
               "DE000NF", "DE000NN", 5900 |]
            18167

    [<Test>]
    member _.TestHHToAH() =
        TestPath
            [| "DE000HH"; "DE000AH" |]
            [| "DE000HH", "DE95366", 1710
               "DE95366", "DE0AHAR", 1720
               "DE0AHAR", "DE000AH", 2200 |]
            9172

    [<Test>]
    member _.TestAHToHH() =
        TestPath
            [| "DE000AH"; "DE000HH" |]
            [| "DE000AH", "DE0AHAR", 2200
               "DE0AHAR", "DE95366", 1720
               "DE95366", "DE000HH", 1710 |]
            9172

    [<Test>]
    member _.TestATWbfToATG() =
        if hasCountry "AUT" then
            TestPath [| "ATWbf"; "ATG" |] [| "ATWbf", "ATG", 10501 |] 18236

    [<Test>]
    member _.TestATSbToATMi() =
        if hasCountry "AUT" then
            TestPath [| "ATSb"; "ATMi" |] [| "ATSb", "ATKue", 10102; "ATKue", "ATMi", 13001 |] 18236
