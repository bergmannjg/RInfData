// extra edges for germany
module ExtraEdges

open RInfGraph

let private imcode = "0080"

let private addWalkingEdge (opidFrom: string) (opidTo: string) (line: string) (graph: Map<string, GraphEdge list>) =
    graph |> Graph.addEdge opidFrom opidTo line imcode 1 10 0.0 0.1 0.1

let private addWalkingEdges (graph: Map<string, GraphEdge list>) =
    graph
    // Berlin Hauptbahnhof-Lehrter Bf  (Stadtb) to Berlin Hauptbahnhof - Lehrter Bahnhof
    |> addWalkingEdge "DE000BL" "DE00BLS" "9901"

let private addEdge opidFrom opidTo line cost maxSpeed startKm endKm length graph =
    Graph.addEdge opidFrom opidTo line imcode cost maxSpeed startKm endKm length graph

// data is generated with 'RInfLoader --Line.MissingSols'
let private addMissingEdges (graph: Map<string, GraphEdge list>) =
    graph
    |> addEdge "DE0ANMG" "DE0ANSG" "1043" 318 100 75.320 78.500 3.180
    |> addEdge "DE000HB" "DE95222" "1401" 245 100 5.101 7.552 2.451
    |> addEdge "DE00FFU" "DE0NMOT" "1733" 1145 280 234.136 266.217 32.081
    |> addEdge "DE0HOSS" "DE0HOLB" "1740" 427 160 143.442 150.276 6.834
    |> addEdge "DE95630" "DE0ELUE" "2100" 63 120 13.093 13.856 0.763
    |> addEdge "DEEDDPB" "DE0EDDP" "2120" 62 100 4.129 4.755 0.626
    |> addEdge "DE0EWIH" "DE0KGVW" "2143" 1282 100 2.647 15.471 12.824
    |> addEdge "DE0EGHN" "DE0EBTH" "2246" 541 100 6.695 12.111 5.416
    |> addEdge "DE0EEFR" "DE95882" "2280" 90 100 6.724 7.630 0.906
    |> addEdge "DE0EDHD" "DE95955" "2320" 235 100 2.431 4.784 2.353
    |> addEdge "DE00FHG" "DE00FSN" "2651" 259 100 118.832 121.430 2.598
    |> addEdge "DE0HDRG" "DE0HHRG" "2950" 2284 100 26.352 49.200 22.848
    |> addEdge "DE96666" "DE96663" "3231" 138 100 0.654 2.037 1.383
    |> addEdge "DE00SBB" "DE0SLCH" "3274" 323 100 14.140 17.370 3.230
    |> addEdge "DE0SVWG" "DE0SBOG" "3295" 223 100 13.377 15.613 2.236
    |> addEdge "DE96954" "DE00FFU" "3600" 306 160 105.655 110.567 4.912
    |> addEdge "DETSZAH" "DE97505" "4720" 161 100 7.692 9.310 1.618
    |> addEdge "DE0TPHG" "DE0TPBG" "4850" 206 100 1.312 3.379 2.067
    |> addEdge "DE000TN" "DE0TNSN" "4900" 132 120 58.160 59.752 1.592
    |> addEdge "DE98012" "DE00NSI" "5830" 450 100 2.568 7.070 4.502
    |> addEdge "DE00BHD" "DE0BHND" "6183" 28 100 19.200 19.482 0.282
    |> addEdge "DE00BPZ" "DE00BHC" "6198" 251 100 7.332 9.844 2.512
    |> addEdge "DE00DAF" "DE00DRR" "6200" 697 120 25.420 33.794 8.374
    |> addEdge "DE0DNKG" "DE0DNEG" "6216" 1128 100 24.150 35.432 11.282
    |> addEdge "DEYLLCI" "DEYLLCH" "6258" 214 120 76.021 78.600 2.579
    |> addEdge "DE00DBM" "DE0DXBB" "6270" 196 100 49.927 51.892 1.965
    |> addEdge "DEYBBMZ" "DEYBBPO" "6345" 619 160 176.700 186.608 9.908
    |> addEdge "DE00LHO" "DE0LHNZ" "6356" 379 100 13.100 16.893 3.793
    |> addEdge "DE0LLMH" "DE0LLEL" "6367" 293 120 3.903 7.427 3.524
    |> addEdge "DE0BBDW" "DE0BACO" "6519" 381 100 1.805 5.618 3.813
    |> addEdge "DE0BAWG" "DE0BZIG" "6559" 359 100 0.033 3.632 3.599
    |> addEdge "DE0BAWG" "DE0BZIG" "6560" 357 100 0.192 3.767 3.575
    |> addEdge "EU00047" "DE0DEIG" "6588" 1778 50 13.707 22.600 8.893
    |> addEdge "DE0DZAG" "DE0DADG" "6663" 1151 100 102.296 113.810 11.514

let addExtraEdges (graph: Map<string, GraphEdge list>) =
    graph |> addWalkingEdges |> addMissingEdges

// data generated with RInfGraph/analyze_graph.py
// all nodes of a line graph should have atmost 2 edges
let removableEdges =
    [| ("1421", "DEHBR A", "DE95254")
       ("1620", "DE000HO", "DE00HOR")
       ("1732", "DE00HEB", "DE0HEDM")
       ("2165", "DE0EEHT", "DE0EBDA")
       ("2250", "DE0EHOC", "DE0EBTH")
       ("2950", "DE0HHRN", "DE000HO")
       ("3260", "DE96697", "DE00SSB")
       ("4000", "DE00RAP", "DE0RAPP")
       ("4000", "DE00RAP", "DE0RWAE")
       ("4261", "DE00RAP", "DE0RAPP")
       ("4280", "DE00RAP", "DE000RO")
       ("4600", "DE0TRES", "DE00TRE")
       ("4600", "DE0TREW", "DE0TREB")
       ("4600", "DE0TTLU", "DETT  G")
       ("5703", "DE0MALD", "DE0MBEF")
       ("5900", "DE000NF", "DE0NFUH")
       ("5900", "DE0NFUH", "DE00NVA")
       ("6403", "DE000LK", "DE00LAF")
       ("6420", "DE000LK", "DE00LFZ")
       ("6443", "DE0WRKD", "DE00WRD")
       ("6443", "DE0WRSS", "DE0WRHI") |]
