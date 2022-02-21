module MissingStops

/// reason why there is no matching of rinf op and osm stop
type ReasonOfNoMatching =
    /// distance of matched rinf op and osm stop is gt than maxDistanceOfMatchedOps
    | DistanceToStop
    /// distance of matched rinf op and osm ways of line is gt than maxDistanceOfOpToWaysOfLine
    | DistanceToWaysOfLine
    /// rinf op not yet mapped, even a service_station with only one siding track should be mapped
    | OsmNotYetMapped
    /// disused station not mapped in osm
    | DisusedStation
    | Unexpected

type MissingStop =
    { opid: string
      reason: ReasonOfNoMatching }

type RailwayTag =
    | Service_station
    | Yard
    | Crossover
    | Switch
    | Disused
    | PlannedRailway
    | Mismatch
    | Undefined

type NotYetMappedStop = { id: string; rw: RailwayTag }

let private notYetMappedStops: NotYetMappedStop [] =
    [|
       (*---*)
       { id = "DE   AM"; rw = Yard }
       { id = "DE  AGR"; rw = Disused }
       { id = "DE  AHE"; rw = Disused }
       { id = "DE  ALA"; rw = Yard }
       { id = "DE  ALH"; rw = Disused }
       { id = "DE  ANN"; rw = Disused }
       { id = "DE  EOD"; rw = Disused }
       { id = "DE  EZE"; rw = Disused }
       { id = "DE  FKR"; rw = Yard }
       { id = "DE  HER"; rw = Yard }
       { id = "DE  LMR"; rw = Undefined }
       { id = "DE ABDF"; rw = Disused }
       { id = "DE AGHI"; rw = Disused }
       { id = "DE AGOE"; rw = Service_station }
       { id = "DE AHBI"; rw = Yard }
       { id = "DE AHSG"; rw = Disused }
       { id = "DE ALAA"; rw = Undefined }
       { id = "DE ALHG"; rw = Service_station }
       { id = "DE ALRA"; rw = Undefined }
       { id = "DE ANEK"; rw = Disused }
       { id = "DE ASDS"; rw = Disused }
       { id = "DE ASTK"; rw = Disused }
       { id = "DE BHPN"; rw = Undefined }
       { id = "DE BMZD"; rw = Undefined }
       { id = "DE EBKM"; rw = Yard }
       { id = "DE EBNO"; rw = Yard }
       { id = "DE EDAT"; rw = Service_station }
       { id = "DE EDLU"; rw = Yard }
       { id = "DE EDMF"; rw = Yard }
       { id = "DE EDOZ"; rw = Service_station }
       { id = "DE EHGH"; rw = Service_station }
       { id = "DE EKLU"; rw = Disused }
       { id = "DE ELIT"; rw = Service_station }
       { id = "DE ELSD"; rw = Disused }
       { id = "DE ENCH"; rw = Disused }
       { id = "DE ENDI"; rw = Disused }
       { id = "DE ERAE"; rw = Disused }
       { id = "DE ESMG"; rw = Crossover }
       { id = "DE ESPN"; rw = Disused }
       { id = "DE ESRO"; rw = Service_station }
       { id = "DE EWID"; rw = Disused }
       { id = "DE EWMN"; rw = Disused }
       { id = "DE EWSH"; rw = Service_station }
       { id = "DE HBHE"; rw = Disused }
       { id = "DE HBMA"; rw = Disused }
       { id = "DE HDRB"; rw = Disused }
       { id = "DE HDRO"; rw = Disused }
       { id = "DE HHRN"; rw = Yard }
       { id = "DE HNPL"; rw = Disused }
       { id = "DE HU G"; rw = Yard }
       { id = "DE WSGR"; rw = Undefined }
       { id = "DE WWRN"; rw = Disused }
       { id = "DEEHERT"; rw = Yard }
       { id = "DEHBR V"; rw = Undefined }
       { id = "DELEGOB"; rw = Undefined }
       { id = "DE HHRN"; rw = Yard }
       { id = "DE ERKS"; rw = Disused }
       { id = "DE HBNB"; rw = Disused }
       { id = "DE ERMH"; rw = Service_station }
       { id = "DEEOB O"; rw = Undefined }
       { id = "DEEOB N"; rw = Undefined }
       { id = "DE EHIG"; rw = Disused }
       { id = "DE KMYM"; rw = Disused }
       { id = "DE KMYM"; rw = PlannedRailway }
       { id = "DE  SST"; rw = Service_station }
       { id = "DE SELN"; rw = Disused }
       { id = "DE SKIR"; rw = Disused }
       { id = "DE  RLB"; rw = Yard }
       { id = "DE FMWG"; rw = Service_station }
       { id = "DE FGHF"; rw = Service_station }
       { id = "DE FEGW"; rw = Service_station }
       { id = "DE FWAD"; rw = Service_station }
       { id = "DE FMAB"; rw = Service_station }
       { id = "DE  FPF"; rw = Service_station }
       { id = "DE FWRY"; rw = Service_station }
       { id = "DEFFG H"; rw = Switch }
       { id = "DE FNIA"; rw = Service_station }
       { id = "DE FBTB"; rw = Service_station }
       { id = "DE FNIJ"; rw = Service_station }
       { id = "DE  ROW"; rw = Service_station }
       { id = "DE RMUG"; rw = Service_station }
       { id = "DE  RKR"; rw = Yard }
       { id = "DE  ROG"; rw = Yard }
       { id = "DE RBNB"; rw = Service_station }
       { id = "DE  RHF"; rw = Mismatch }
       { id = "DE TUDS"; rw = Service_station }
       { id = "DETLW S"; rw = Switch }
       { id = "DETLW O"; rw = Switch }
       { id = "DE TT G"; rw = Service_station }
       { id = "DE  TWS"; rw = Service_station }
       { id = "DETET N"; rw = Switch }
       { id = "DE TSUD"; rw = Service_station }
       { id = "DE NPUE"; rw = Service_station }
       { id = "DE NGAE"; rw = Service_station }
       { id = "DE  NWR"; rw = Yard }
       { id = "DE NARN"; rw = Service_station }
       { id = "DE   NT"; rw = Service_station }
       { id = "DE  NEM"; rw = Service_station }
       { id = "DE MUGH"; rw = Service_station }
       { id = "DE MSEH"; rw = Service_station }
       { id = "DE MNAR"; rw = Service_station }
       { id = "DE MPAR"; rw = Disused }
       { id = "DE  MSM"; rw = Disused }
       { id = "DE MINU"; rw = Switch }
       { id = "DE  MGO"; rw = Disused }
       { id = "DE MPGO"; rw = Yard }
       { id = "DE MH W"; rw = Yard }
       { id = "DE  MFH"; rw = Service_station }
       { id = "DEMMA A"; rw = Switch }
       { id = "DE MNFO"; rw = Crossover }
       { id = "DE MAHN"; rw = Service_station }
       { id = "DE  MFI"; rw = Disused }
       { id = "DE MHTM"; rw = Service_station }
       { id = "DE  NSI"; rw = Service_station }
       { id = "DE NSAN"; rw = Service_station }
       { id = "DE NSIN"; rw = Service_station }
       { id = "DE MMNG"; rw = Service_station }
       { id = "DE NF G"; rw = Yard }
       { id = "DE NLUH"; rw = Service_station }
       { id = "DE NHIL"; rw = Service_station }
       { id = "DE NMEI"; rw = Service_station }
       { id = "DE  NMM"; rw = Service_station }
       { id = "DE UE G"; rw = Yard }
       { id = "DE UE O"; rw = Yard }
       { id = "DE UE F"; rw = Switch }
       { id = "DE UE L"; rw = Switch }
       { id = "DE  BKD"; rw = Service_station }
       { id = "DE  WDN"; rw = Service_station }
       { id = "DE WKWA"; rw = Service_station }
       { id = "DEAHBIA"; rw = Switch }
       { id = "DE LHEL"; rw = Service_station }
       { id = "DEBATSO"; rw = Switch }
       { id = "DE BGRA"; rw = Service_station }
       { id = "DE BWKL"; rw = Service_station }
       { id = "DE DGIB"; rw = Switch }
       { id = "DE  BKP"; rw = Yard }
       { id = "DE BSTA"; rw = Service_station }
       { id = "DE DWID"; rw = Disused }
       { id = "DE DHDF"; rw = Disused }
       { id = "DE  DCF"; rw = Service_station }
       { id = "DE  BBS"; rw = Service_station }
       (*--*)
       |]

let missingStops: MissingStop [] =
    notYetMappedStops
    |> Array.map (fun s ->
        { opid = s.id
          reason =
            if s.rw = Disused then
                ReasonOfNoMatching.DisusedStation
            else
                ReasonOfNoMatching.OsmNotYetMapped })

let private checkMissingStop (ops: RInf.OperationalPoint []) (ms: MissingStop) =
    let dumpRailwayTag (e: OSM.Element) =
        [| OSM.Tag.Railway
           OSM.Tag.PlannedRailway
           OSM.Tag.DisusedRailway
           OSM.Tag.HistoricRailway |]
        |> Array.tryPick (fun tag -> OSM.Data.getTagValue tag e)

    let dumpElement (e: OSM.Element) =
        match e with
        | OSM.Node v -> sprintf "node %d, %A" v.id (dumpRailwayTag e)
        | OSM.Way v -> sprintf "way %d, %A" v.id (dumpRailwayTag e)
        | OSM.Relation v -> sprintf "relation %d, %A" v.id (dumpRailwayTag e)

    let printScript (s: NotYetMappedStop) (op: RInf.OperationalPoint) =
        printfn
            "add_node(%f, %f, '%s', '%s', '%s');"
            op.Latitude
            op.Longitude
            op.Name
            "service_station"
            (OSM.Transform.Op.toRailwayRef op.UOPID)

    async {
        use client = OSM.OverpassRequest.getClient ()

        try
            let! osm = OSMLoader.queryRailwaRef client (OSM.Transform.Op.toRailwayRef ms.opid)

            match ops
                  |> Array.tryFind (fun op -> op.UOPID = ms.opid)
                with
            | Some op ->
                osm.elements
                |> Array.iter (fun e -> printfn "found %s, %s" ms.opid (dumpElement e))

                match notYetMappedStops
                      |> Array.tryFind (fun s -> s.id = ms.opid)
                    with
                | Some s when s.rw = Service_station -> printScript s op
                | _ -> ()

            | None -> printfn "op %s not found" ms.opid

            return (ms, Some osm.elements)
        with
        | ex ->
            printfn "Error: %s %s" ex.Message ex.StackTrace
            return (ms, None)
    }

let checkMissingStops (rinfDataDir) =
    let rec loop (work: 'a -> Async<'b>) (l: 'a list) (results: 'b list) =
        async {
            match l with
            | h :: t ->
                let! res = work h
                return! loop work t (res :: results)
            | [] -> return results |> List.rev
        }

    let ops = RInfLoader.loadRInfOperationalPoints 0 rinfDataDir

    async {
        let! results = loop (checkMissingStop ops) (missingStops |> Array.toList) []
        printfn "done"
    }
