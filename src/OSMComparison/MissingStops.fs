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
    | Disused
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
       { id = "DE  EPK"; rw = Service_station }
       { id = "DE  EZE"; rw = Disused }
       { id = "DE  FKR"; rw = Yard }
       { id = "DE  HAL"; rw = Service_station }
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
       { id = "DE HBNS"; rw = Service_station }
       { id = "DE HDRB"; rw = Disused }
       { id = "DE HDRE"; rw = Service_station }
       { id = "DE HDRO"; rw = Disused }
       { id = "DE HEST"; rw = Service_station }
       { id = "DE HFLL"; rw = Service_station }
       { id = "DE HGAR"; rw = Service_station }
       { id = "DE HHAG"; rw = Service_station }
       { id = "DE HHIG"; rw = Service_station }
       { id = "DE HHKF"; rw = Service_station }
       { id = "DE HHMS"; rw = Service_station }
       { id = "DE HHOE"; rw = Service_station }
       { id = "DE HHOH"; rw = Service_station }
       { id = "DE HHRN"; rw = Yard }
       { id = "DE HHWD"; rw = Service_station }
       { id = "DE HIHV"; rw = Service_station }
       { id = "DE HKHF"; rw = Service_station }
       { id = "DE HKSU"; rw = Service_station }
       { id = "DE HLMB"; rw = Service_station }
       { id = "DE HNKP"; rw = Service_station }
       { id = "DE HNPL"; rw = Disused }
       { id = "DE HNTM"; rw = Service_station }
       { id = "DE HROR"; rw = Service_station }
       { id = "DE HSVL"; rw = Service_station }
       { id = "DE HU G"; rw = Yard }
       { id = "DE WSGR"; rw = Undefined }
       { id = "DE WWRN"; rw = Disused }
       { id = "DEEHERT"; rw = Yard }
       { id = "DEHBR V"; rw = Undefined }
       { id = "DELEGOB"; rw = Undefined }
       { id = "DE HHRN"; rw = Yard }
       { id = "DE ERKS"; rw = Disused }
       { id = "DE HBNB"; rw = Disused }
       { id = "DE FTWS"; rw = Service_station }
       { id = "DE ERMH"; rw = Service_station }
       (*---*)
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
    let dumpElement (e: OSM.Element) =
        match e with
        | OSM.Node v -> sprintf "node %d, %A" v.id (OSM.Data.getTagValue OSM.Tag.Railway e)
        | OSM.Way v -> sprintf "way %d, %A" v.id (OSM.Data.getTagValue OSM.Tag.Railway e)
        | OSM.Relation v -> sprintf "relation %d, %A" v.id (OSM.Data.getTagValue OSM.Tag.Railway e)

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
