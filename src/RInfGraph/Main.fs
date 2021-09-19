module Main

#if FABLE_COMPILER

open Fable.Core
open Fable.Core.JsInterop

open RInfGraph

[<Import("readFile", from = "fs/promises")>]
let readFile (file: string) : JS.Promise<string> = jsNative

[<EntryPoint>]
let main argv =
    promise {
        if argv.Length = 2 then
            let! json = readFile argv.[0]
            let g = JS.JSON.parse json :?> GraphNode []

            Graph.getShortestPath g (argv.[1].Split [| ';' |])
            |> Graph.printShortestPath
            |> printfn "%s"
        else
            printfn "arg expected: op1;op2"

    }
    |> Promise.start

    0

#endif
