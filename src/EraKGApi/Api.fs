// see ERA knowledge graph  https://zenodo.org/record/6516745/files/Marina-Aguado-Pitch-@GIRO.pdf
// see sparql endpoint https://linked.ec-dataplatform.eu/sparql/
namespace EraKG

open FSharp.Collections

type RailwayLocation =
    { NationalIdentNum: string
      Kilometer: float }

type OperationalPoint =
    { Name: string
      Type: string
      Country: string
      UOPID: string
      Latitude: float
      Longitude: float
      RailwayLocations: RailwayLocation [] }

type Track =
    { id: string
      label: string
      maximumPermittedSpeed: int option
      loadCapability: string option
      contactLineSystem: string option }

type RailwayLine =
    { LineIdentification: string
      Country: string
      LineCategories: string [] }

type SectionOfLine =
    { Name: string
      Country: string
      Length: float
      LineIdentification: string
      IMCode: string
      StartOP: string
      EndOP: string
      Tracks: Track [] }

type Tunnel =
    { Name: string
      Country: string
      Length: float
      LineIdentification: string
      StartLatitude: float
      StartLongitude: float
      EndLatitude: float
      EndLongitude: float
      ContainingTracks: string [] }

module Api =

    open Sparql

    let prefixTrack = "http://data.europa.eu/949/functionalInfrastructure/tracks/"

    let prefixContactLineSystem =
        "http://data.europa.eu/949/functionalInfrastructure/contactLineSystems/"

    let propLabel = "http://www.w3.org/2000/01/rdf-schema#label"
    let propMaximumPermittedSpeed = "http://data.europa.eu/949/maximumPermittedSpeed"
    let propContactLineSystem = "http://data.europa.eu/949/contactLineSystem"
    let propLoadCapability = "http://data.europa.eu/949/loadCapability"
    let propTenClassification = "http://data.europa.eu/949/tenClassification"
    let propNetElements = "http://data.europa.eu/949/topology/netElements/"

    let private endpoint = "https://linked.ec-dataplatform.eu/sparql"

    let countrySplitChars = [| ';' |]

    /// see https://op.europa.eu/en/web/eu-vocabularies/dataset/-/resource?uri=http://publications.europa.eu/resource/dataset/country
    let private countriesQuery () =
        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>

SELECT distinct ?country
WHERE {{
  ?operationalPoint a era:OperationalPoint .
  ?operationalPoint era:inCountry ?country .
}}
"""

    let loadCountriesData () : Async<string> =
        Request.GetAsync endpoint (countriesQuery ()) Request.applicationSparqlResults


    let private toCountryQuery (item: string) (countryArg: string) : string =
        countryArg.Split countrySplitChars
        |> Array.map (fun country ->
            $"{{ {item} era:inCountry <http://publications.europa.eu/resource/authority/country/{country}> . }}")
        |> String.concat " UNION "

    let private operationalPointQuery (country: string) =
        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>

SELECT distinct ?opName, ?uopid, ?opType, ?location, ?lineReference, ?country
WHERE {{
  ?operationalPoint a era:OperationalPoint .
  ?operationalPoint rdfs:label ?opName .

  ?operationalPoint era:uopid ?uopid .
  ?operationalPoint era:lineReference ?lineReference .
  ?operationalPoint <http://www.w3.org/2003/01/geo/wgs84_pos#location> ?location .
  ?operationalPoint era:opType ?opType .
  ?operationalPoint era:inCountry ?country .
  {toCountryQuery "?operationalPoint" country}
}}
"""

    let loadOperationalPointData (country: string) : Async<string> =
        Request.GetAsync endpoint (operationalPointQuery country) Request.applicationSparqlResults

    let private sectionOfLineQuery (country: string) =
        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>

SELECT distinct ?sectionOfLine, ?length, ?solNature, ?lineNationalId , ?imCode, ?startOp, ?endOp, ?track, ?country
WHERE {{
  ?sectionOfLine a era:SectionOfLine .
  ?sectionOfLine era:lineNationalId ?lineNationalId .
  ?sectionOfLine era:length ?length .
  ?sectionOfLine era:solNature ?solNature .
  ?sectionOfLine era:imCode ?imCode .
  ?sectionOfLine era:opStart ?startOp .
  ?sectionOfLine era:opEnd ?endOp .
  ?sectionOfLine era:track ?track .
  ?sectionOfLine era:inCountry ?country .
  {toCountryQuery "?sectionOfLine" country}
}}
"""

    let loadSectionOfLineData (country: string) : Async<string> =
        Request.GetAsync endpoint (sectionOfLineQuery country) Request.applicationSparqlResults

    let private trackQuery (country: string) (n: int) =
        let filter =
            if n < 9 then
                n.ToString()
            else
                (n.ToString() + "a-z")

        let pattern = $"^[{filter}]"

        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>

DESCRIBE ?track
WHERE {{
  ?sectionOfLine a era:SectionOfLine .
  ?sectionOfLine era:track ?track .
  
  ?sectionOfLine era:lineNationalId ?lineNationalId .
  ?lineNationalId rdfs:label ?line .
  FILTER(regex(?line, "{pattern}", "i")) .
  {toCountryQuery "?sectionOfLine" country}
}}
"""

    let loadTrackData (country: string) (n: int) : Async<string> =
        async {
            try
                let! data = Request.GetAsync endpoint (trackQuery country n) Request.applicationMicrodata
                return data
            with
            | e ->
                fprintfn stderr "error: loadTrackData %s" e.Message
                return "{\"items\":[]}"
        }

    let private nationalRailwayLineQuery (country: string) =
        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>

SELECT distinct ?label, ?lineCategory, ?country
WHERE {{
  ?nationalRailwayLine a era:NationalRailwayLine.
  ?nationalRailwayLine rdfs:label ?label .
  OPTIONAL {{
     ?nationalRailwayLine era:lineCategory ?lineCategory .
     FILTER(!STRSTARTS(STR(?lineCategory), 'http://data.europa.eu/949/concepts/line-category/rinf/')).
  }} .
  ?nationalRailwayLine era:inCountry ?country .
  {toCountryQuery "?nationalRailwayLine" country}
}}
"""

    let loadNationalRailwayLineData (country: string) : Async<string> =
        Request.GetAsync endpoint (nationalRailwayLineQuery country) Request.applicationSparqlResults

    let private tunnelQuery (country: string) =
        $"""
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>

SELECT distinct ?tunnel, ?tunnelIdentification, ?length, ?startlocation, ?endlocation, ?netElement, ?country
WHERE {{
  ?tunnel a era:Tunnel.

  ?tunnel era:tunnelIdentification ?tunnelIdentification .
  ?tunnel era:length ?length .
  ?tunnel era:startLocation ?startlocation .
  ?tunnel era:endLocation ?endlocation .
  ?tunnel era:netElement ?netElement.
  ?tunnel era:inCountry ?country .
  {toCountryQuery "?tunnel" country}
}}
"""

    let loadTunnelData (country: string) : Async<string> =
        Request.GetAsync endpoint (tunnelQuery country) Request.applicationSparqlResults

    let private uriTypeToString (r: Rdf) (prefix: string) : string =
        if r.``type`` = "uri" then
            System.Web.HttpUtility.UrlDecode(r.value.Substring(prefix.Length))
        else
            raise (System.Exception($"uriTypeToString: type {r.``type``} unexpected"))

    let private toOpType (r: Rdf) : string =
        uriTypeToString r "http://data.europa.eu/949/concepts/op-types/"

    let private toCountryType (r: Rdf) : string =
        uriTypeToString r "http://publications.europa.eu/resource/authority/country/"

    let private toSectionsOfLineId (r: Rdf) : string =
        uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/sectionsOfLine/"

    let private toNationalLines (r: Rdf) : string =
        uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/nationalLines/"

    let private toUOPID (r: Rdf) : string =
        uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/operationalPoints/"

    let private toLocation (r: Rdf) : (float * float) =
        let splits =
            (uriTypeToString r "http://data.europa.eu/949/locations/")
                .Split [| '/' |]

        (float splits.[0], float splits.[1])

    let private toRailwayLocation (r: Rdf) : RailwayLocation =
        let splits =
            (uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/lineReferences/")
                .Split [| '_' |]

        { NationalIdentNum = splits.[0]
          Kilometer = float splits.[1] }

    let private toFloat (r: Rdf) : float =
        try
            float r.value
        with
        | error ->
            fprintfn stderr "%s" error.Message
            0.0

    let toOperationalPoints (sparql: QueryResults) (countries: string []) : OperationalPoint [] =
        sparql.results.bindings
        |> Array.fold
            (fun (ops: Map<string, OperationalPoint>) b ->
                let country = toCountryType b.["country"]

                if countries |> Array.contains country then
                    ops.Change(
                        b.["uopid"].value,
                        fun op ->
                            match op with
                            | Some op ->
                                let candidate = toRailwayLocation b.["lineReference"]

                                if op.RailwayLocations |> Array.contains candidate then
                                    Some op
                                else
                                    Some
                                        { op with
                                            RailwayLocations =
                                                op.RailwayLocations
                                                |> Array.append [| candidate |] }
                            | None ->
                                let lon, lat = toLocation b.["location"]

                                Some
                                    { Name = b.["opName"].value
                                      Type = toOpType b.["opType"]
                                      Country = country
                                      UOPID = b.["uopid"].value
                                      Latitude = lat
                                      Longitude = lon
                                      RailwayLocations = [| toRailwayLocation b.["lineReference"] |] }
                    )
                else
                    ops)
            Map.empty
        |> Map.values
        |> Seq.toArray

    let toCountries (sparql: QueryResults) : string [] =
        sparql.results.bindings
        |> Array.map (fun b -> toCountryType b.["country"])

    let toRailwayLines (sparql: QueryResults) : RailwayLine [] =
        sparql.results.bindings
        |> Array.fold
            (fun (lines: Map<string, RailwayLine>) b ->
                let addLineCategory (b: Map<string, Rdf>) (lineCategories: string []) : string [] =
                    if b |> Map.containsKey "lineCategory" then
                        lineCategories
                        |> Array.append [| (uriTypeToString
                                                b.["lineCategory"]
                                                "http://data.europa.eu/949/concepts/line-category/") |]
                    else
                        lineCategories

                lines.Change(
                    b.["label"].value,
                    fun line ->
                        match line with
                        | Some line -> Some { line with LineCategories = addLineCategory b line.LineCategories }
                        | None ->
                            Some
                                { LineIdentification = b.["label"].value
                                  Country = toCountryType b.["country"]
                                  LineCategories = addLineCategory b [||] }
                ))
            Map.empty
        |> Map.values
        |> Seq.toArray

    let toTrack (id: string) (tracks: Microdata) : Track =
        match tracks.items
              |> Array.tryFind (fun item -> item.id = id)
            with
        | Some item ->
            let prefixLoadCapabilities = "http://data.europa.eu/949/concepts/load-capabilities/"

            let getValue (prop: string) =
                if item.properties.ContainsKey prop then
                    Some(item.properties.[prop].[0].ToString())
                else
                    None

            { id = System.Web.HttpUtility.UrlDecode(id.Substring(prefixTrack.Length))
              label = getValue propLabel |> Option.defaultValue ""
              maximumPermittedSpeed =
                getValue propMaximumPermittedSpeed
                |> Option.map int
              loadCapability =
                getValue propLoadCapability
                |> Option.map (fun s -> s.Substring(prefixLoadCapabilities.Length))
              contactLineSystem =
                getValue propContactLineSystem
                |> Option.map (fun s ->
                    (System.Web.HttpUtility.UrlDecode(s))
                        .Substring(prefixContactLineSystem.Length)) }
        | None ->
            { id = System.Web.HttpUtility.UrlDecode(id.Substring(prefixTrack.Length))
              label = "directional track"
              maximumPermittedSpeed = Some 100
              loadCapability = Some "rinf/90"
              contactLineSystem = None }

    let toTunnels (sparql: QueryResults) : Tunnel [] =
        sparql.results.bindings
        |> Array.fold
            (fun (ops: Map<string, Tunnel>) b ->
                let toTrackLabel (netElement: string) =
                    System.Web.HttpUtility.UrlDecode(netElement.Substring(propNetElements.Length))

                ops.Change(
                    b.["tunnelIdentification"].value,
                    fun op ->
                        match op with
                        | Some op ->
                            let candidate = toTrackLabel b.["netElement"].value

                            if op.ContainingTracks |> Array.contains candidate then
                                Some op
                            else
                                Some
                                    { op with
                                        ContainingTracks =
                                            op.ContainingTracks
                                            |> Array.append [| candidate |] }
                        | None ->
                            let startlon, startlat = toLocation b.["startlocation"]
                            let endlon, endlat = toLocation b.["endlocation"]
                            let trackLabel = toTrackLabel b.["netElement"].value

                            Some
                                { Name = b.["tunnelIdentification"].value
                                  Country = toCountryType b.["country"]
                                  LineIdentification = trackLabel.Substring(0, 4)
                                  Length = float b.["length"].value
                                  StartLatitude = startlat
                                  StartLongitude = startlon
                                  EndLatitude = endlat
                                  EndLongitude = endlon
                                  ContainingTracks = [| trackLabel |] }
                ))
            Map.empty
        |> Map.values
        |> Seq.toArray

    let private hasPassendgerLineCategory (lineIdentification: string) (lines: RailwayLine []) =
        match lines
              |> Array.tryFind (fun l -> l.LineIdentification = lineIdentification)
            with
        | Some line ->
            line.LineCategories.Length = 0
            || line.LineCategories
               |> Array.exists (fun cat -> cat.StartsWith "P")
        | None ->
            fprintfn stderr $"line {lineIdentification} not found"
            false

    let toSectionsOfLine (sparql: QueryResults) (lines: RailwayLine []) (tracks: Microdata) : SectionOfLine [] =
        sparql.results.bindings
        |> Array.fold
            (fun (sols: Map<string, SectionOfLine>) b ->
                let lineNationalId = toNationalLines b.["lineNationalId"]
                let sectionsOfLineId = toSectionsOfLineId b.["sectionOfLine"]

                if hasPassendgerLineCategory lineNationalId lines then
                    sols.Change(
                        sectionsOfLineId,
                        fun sol ->
                            match sol with
                            | Some op ->
                                let candidate = toTrack b.["track"].value tracks

                                if op.Tracks |> Array.contains candidate then
                                    Some op
                                else
                                    Some { op with Tracks = op.Tracks |> Array.append [| candidate |] }
                            | None ->
                                Some
                                    { Name = sectionsOfLineId
                                      Country = toCountryType b.["country"]
                                      Length = toFloat b.["length"]
                                      LineIdentification = toNationalLines b.["lineNationalId"]
                                      IMCode = b.["imCode"].value
                                      StartOP = toUOPID b.["startOp"]
                                      EndOP = toUOPID b.["endOp"]
                                      Tracks = [| toTrack b.["track"].value tracks |] }
                    )
                else
                    sols)
            Map.empty
        |> Map.values
        |> Seq.toArray
