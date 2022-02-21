module ExtraEdges

open RInfGraph

let private addWalkingEdge (opidFrom: string) (opidTo: string) (line: string) (graph: Map<string, GraphEdge list>) =
    graph
    |> Graph.addEdge opidFrom opidTo line 1 10 0.0 0.1 0.1

let private addWalkingEdges (graph: Map<string, GraphEdge list>) =
    graph
    // Berlin Hauptbahnhof-Lehrter Bf  (Stadtb) to Berlin Hauptbahnhof - Lehrter Bahnhof
    |> addWalkingEdge "DE000BL" "DE00BLS" "9901"

let private addMissingEdges (graph: Map<string, GraphEdge list>) =
    graph
    // missing SoL 6100 Berlin-Spandau Mitte - Berlin-Spandau, length 1.0, verified by https://geovdbn.deutschebahn.com/isr
    |> Graph.addEdge "DEBSPDM" "DE0BSPD" "6100" 60 160 11.4 12.428 1.0

let addExtraEdges (graph: Map<string, GraphEdge list>) =
    graph |> addWalkingEdges |> addMissingEdges
