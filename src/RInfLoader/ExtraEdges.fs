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
    // missing SoL 1733 Fulda - Mottgers, length 32.0, verified by https://geovdbn.deutschebahn.com/isr
    |> Graph.addEdge "DE00FFU" "DE0NMOT" "1733" 680 280 234.136 266.217 32.0
    // missing SoL 3600 Fulda - StrUeb_3600_3824, length 4.9, verified by https://geovdbn.deutschebahn.com/isr
    |> Graph.addEdge "DE96954" "DE00FFU" "3600" 278 160 105.655 110.567 4.9

let addExtraEdges (graph: Map<string, GraphEdge list>) =
    graph |> addWalkingEdges |> addMissingEdges
