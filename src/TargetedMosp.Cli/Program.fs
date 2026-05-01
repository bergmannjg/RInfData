#if FABLE_COMPILER
module Program
#endif

open TMosp

#if FABLE_COMPILER
open Node.Api
#endif

[<EntryPoint>]
let main argv =
    try
        if argv.Length = 3 then
#if FABLE_COMPILER
            let text = fs.readFileSync (argv[0], "utf8")
            let lines = text.Split([| '\n' |])
            let solutions = Api.fromLines (lines, uint32 argv[1], uint32 argv[2], 1000)
#else
            let solutions = Api.fromFile (argv[0], uint32 argv[1], uint32 argv[2], 1000)
#endif
            if false then
                printfn $"solutions {solutions.Length}"

            if true then
                solutions
                |> Array.iteri (fun i arr ->
                    printfn $"solution {i}:"
                    arr |> Array.iter (fun (n, c) -> printfn $"node {n} {CostArray.toString c}"))
    with e ->
        printfn "error: %s %s" e.Message e.StackTrace

    0
