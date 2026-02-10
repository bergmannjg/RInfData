// see interactive endpoint https://data-interop.era.europa.eu/endpoint
// see sparql endpoint https://data-interop.era.europa.eu/api/sparql
namespace EraKG

open FSharp.Collections
open System.Text.Json
open System.Collections.Generic

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
      RailwayLocations: RailwayLocation[] }

type OpType =
    { Label: string
      Definition: string
      Value: int }

type Track =
    { id: string
      label: string
      maximumPermittedSpeed: int option
      contactLineSystem: string option
      lineCategories: int[] }

type SectionOfLine =
    { Name: string
      Country: string
      Length: float
      LineIdentification: string
      IMCode: string
      StartOP: string
      EndOP: string
      Tracks: Track[] }

type Tunnel =
    { Name: string
      Country: string
      Length: float
      LineIdentification: string
      StartLatitude: float
      StartLongitude: float
      EndLatitude: float
      EndLongitude: float
      ContainingTracks: string[] }

module Api =

    open Sparql

    let private endpoint = "https://data-interop.era.europa.eu/api/sparql"

    module private Utils =
        let appendElem<'T when 'T: equality> (elem: 'T) (arr: 'T array) =
            if Array.contains elem arr then
                arr
            else
                Array.append [| elem |] arr

        let optAppendElem<'T when 'T: equality> (elem: 'T option) (arr: 'T array) =
            match elem with
            | Some elem ->
                if Array.contains elem arr then
                    arr
                else
                    Array.append [| elem |] arr
            | None -> arr

        /// <summary> like
        ///  <see href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-mapmodule.html#change">Map.change</see>
        /// </summary>
        let change<'TKey, 'TValue>
            (dict: Dictionary<'TKey, 'TValue>, key: 'TKey, f: (option<'TValue> -> option<'TValue>))
            =
            match dict.TryGetValue key with
            | true, v ->
                match f (Some v) with
                | Some v ->
                    dict[key] <- v
                    dict
                | None -> dict
            | _ ->
                match f None with
                | Some v ->
                    dict.Add(key, v)
                    dict
                | None -> dict

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#overv">Object Properties</a>
    module Properties =
        let uriTypeToString (r: Rdf) (prefix: string) : string =
            if r.``type`` = "uri" then
                System.Web.HttpUtility.UrlDecode(r.value.Substring prefix.Length)
            else
                raise (System.Exception $"uriTypeToString: type {r.``type``} unexpected")

        let toOpType (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/concepts/op-types/"

        let toCountryType (r: Rdf) : string =
            uriTypeToString r "http://publications.europa.eu/resource/authority/country/"

        let toSectionsOfLineId (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/sectionsOfLine/"

        let toLineCategory (r: Rdf) : int =
            let s = uriTypeToString r "http://data.europa.eu/949/concepts/line-category/rinf/"

            match System.Int32.TryParse s with
            | true, value -> value
            | _ -> raise (System.Exception $"toLineCategory int expected {r}")

        let toTrackLabel (r: Rdf) =
            uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/tracks/"

        let toUOPID (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/functionalInfrastructure/operationalPoints/"

        let toLiteral (r: Rdf) : string =
            if r.``type`` = "literal" then
                r.value
            else
                raise (System.Exception $"toLiteral unexpected datatype {r}")

        let toInt (r: Rdf) : int =
            if r.datatype = Some "http://www.w3.org/2001/XMLSchema#integer" then
                try
                    int r.value
                with error ->
                    raise (System.Exception $"toInt {error.Message} {r}")
            else
                raise (System.Exception $"toInt unexpected datatype {r}")

        let toFloat (r: Rdf) : float =
            if
                r.datatype = Some "http://www.w3.org/2001/XMLSchema#double"
                || r.datatype = Some "http://www.w3.org/2001/XMLSchema#decimal"
            then
                try
                    float r.value
                with error ->
                    raise (System.Exception $"toFloat {error.Message} {r}")
            else
                raise (System.Exception $"toFloat unexpected datatype {r}")

    /// see <a href="https://op.europa.eu/en/web/eu-vocabularies/dataset/-/resource?uri=http://publications.europa.eu/resource/dataset/country">Countries and territories</a>
    module Country =
        let private countriesQuery () =
            $"""
                PREFIX era: <http://data.europa.eu/949/>

                SELECT distinct ?country
                WHERE {{
                  ?operationalPoint a era:OperationalPoint .
                  ?operationalPoint era:inCountry ?country .
                }}
            """

        let loadData () : Async<QueryResults> =
            async {
                let! data = Request.GetAsync endpoint (countriesQuery ()) Request.applicationSparqlResults
                return JsonSerializer.Deserialize data
            }

        let fromQueryResults (sparql: QueryResults) : string[] =
            sparql.results.bindings
            |> Array.map (fun b -> Properties.toCountryType b.["country"])

        let toCountryCondition (item: string) (countries: string[]) : string =
            countries
            |> Array.map (fun country ->
                $"{{ {item} era:inCountry <http://publications.europa.eu/resource/authority/country/{country}> . }}")
            |> String.concat " UNION "

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/OperationalPoint">Operational Point</a>
    module OperationalPoint =
        let private operationalPointQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX wgs: <http://www.w3.org/2003/01/geo/wgs84_pos#> 

                SELECT distinct ?opName ?uopid ?opType ?lat ?lon ?lineNationalLabel ?kilometer ?country
                WHERE {{
                  ?operationalPoint a era:OperationalPoint .
                  ?operationalPoint rdfs:label ?opName .

                  ?operationalPoint era:uopid ?uopid .
                  ?operationalPoint era:lineReference ?lineReference .
                  ?lineReference era:lineNationalId ?lineNationalId.
                  ?lineNationalId  rdfs:label ?lineNationalLabel.
                  ?lineReference era:kilometer ?kilometer .
                  ?operationalPoint wgs:location ?location .
                  ?location wgs:lat ?lat .
                  ?location wgs:long ?lon .
                  ?operationalPoint era:opType ?opType .
                  ?operationalPoint era:inCountry ?country .
                  {Country.toCountryCondition "?operationalPoint" countries}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let! data =
                    Request.GetAsync
                        endpoint
                        (operationalPointQuery countries limit offset)
                        Request.applicationSparqlResults

                return JsonSerializer.Deserialize data
            }

        let private toRailwayLocation (line: Rdf) (km: Rdf) : RailwayLocation =
            { NationalIdentNum = Properties.toLiteral line
              Kilometer = Properties.toFloat km }

        let private concatName (op: OperationalPoint) (s: string) =
            if op.Type = "rinf/90" && not (op.Name.Contains s) then
                op.Name + " - " + s
            else
                op.Name

        let private folder (ops: Dictionary<string, OperationalPoint>) (b: Map<string, Rdf>) =
            let uopid = Properties.toLiteral b.["uopid"]
            let railwayLocation = toRailwayLocation b.["lineNationalLabel"] b.["kilometer"]

            Utils.change (
                ops,
                uopid,
                fun op ->
                    match op with
                    | Some op ->
                        Some
                            { op with
                                Name = concatName op (Properties.toLiteral b.["opName"])
                                RailwayLocations = Utils.appendElem railwayLocation op.RailwayLocations }
                    | None ->
                        Some
                            { Name = Properties.toLiteral b.["opName"]
                              Type = Properties.toOpType b.["opType"]
                              Country = Properties.toCountryType b.["country"]
                              UOPID = uopid
                              Latitude = Properties.toFloat b.["lat"]
                              Longitude = Properties.toFloat b.["lon"]
                              RailwayLocations = [| railwayLocation |] }
            )

        let fromQueryResults (sparql: QueryResults) : OperationalPoint[] =
            sparql.results.bindings
            |> Array.fold folder (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray

    module OpTypes =
        let private opTypesQuery =
            $"""
                PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
                PREFIX era: <http://data.europa.eu/949/>
                SELECT distinct ?n ?l ?d WHERE {{
                  ?c a skos:Concept .
                  ?c skos:inScheme ?s .
                  ?s era:rinfIndex "1.2.0.0.0.4" .
                  ?c skos:notation ?n .
                  ?c skos:definition ?d .
                  ?c skos:prefLabel ?l .
                  FILTER (LANG(?l) = 'en')
                }}
                ORDER BY ?n
            """

        let loadData () : Async<QueryResults> =
            async {
                let! data = Request.GetAsync endpoint opTypesQuery Request.applicationSparqlResults

                return JsonSerializer.Deserialize data
            }

        let fromQueryResults (sparql: QueryResults) : OpType[] =
            sparql.results.bindings
            |> Array.map (fun rdf ->
                { Definition = Properties.toLiteral rdf["d"]
                  Label = Properties.toLiteral rdf["l"]
                  Value = int (Properties.toLiteral rdf["n"]) })

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/Track">Track</a>
    module Track =
        let private trackQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX skos: <http://www.w3.org/2004/02/skos/core#>

                SELECT distinct ?track ?label ?lineCategory ?contactLineSystemTypeLabel ?maximumPermittedSpeed
                WHERE {{
                  ?sectionOfLine a era:SectionOfLine .
                  ?sectionOfLine era:track ?track .
                  
                  OPTIONAL {{ ?track rdfs:label ?label . }}
                  OPTIONAL {{ ?track era:lineCategory ?lineCategory . }}
                  OPTIONAL {{ ?track era:contactLineSystem ?contactLineSystem . 
                              ?contactLineSystem era:contactLineSystemType ?contactLineSystemType .
    						  ?contactLineSystemType skos:prefLabel ?contactLineSystemTypeLabel . }}
                  OPTIONAL {{ ?track era:maximumPermittedSpeed ?maximumPermittedSpeed . }}

                  {Country.toCountryCondition "?sectionOfLine" countries}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let! data =
                    Request.GetAsync endpoint (trackQuery countries limit offset) Request.applicationSparqlResults

                return JsonSerializer.Deserialize data
            }

        let private tryGetLineCategory (r: Map<string, Rdf>) : int option =
            match r.TryGetValue "lineCategory" with
            | true, b -> Some(Properties.toLineCategory b)
            | _ -> None

        let private toTrackEntry (b: Map<string, Rdf>) : Track =
            { id = Properties.toTrackLabel b["track"]
              label = Properties.toLiteral b["label"]
              maximumPermittedSpeed =
                match b.TryGetValue "maximumPermittedSpeed" with
                | true, rdf -> Some(Properties.toInt rdf)
                | false, _ -> Some 100
              contactLineSystem =
                match b.TryGetValue "contactLineSystemTypeLabel" with
                | true, rdf -> Some(Properties.toLiteral rdf)
                | false, _ -> None
              lineCategories =
                match tryGetLineCategory b with
                | Some lineCategory -> [| lineCategory |]
                | None -> [||] }

        let private folder (acc: Dictionary<string, Track>) (b: Map<string, Rdf>) =
            Utils.change (
                acc,
                Properties.toTrackLabel b.["track"],
                fun track ->
                    match track with
                    | Some track ->
                        { track with
                            lineCategories = Utils.optAppendElem (tryGetLineCategory b) track.lineCategories }
                    | None -> toTrackEntry b
                    |> Some
            )

        let fromQueryResults (tracks: QueryResults) : Dictionary<string, Track> =
            tracks.results.bindings
            |> Array.fold folder (Dictionary tracks.results.bindings.Length)

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/SectionOfLine">SectionOfLine</a>
    module SectionOfLine =
        let private sectionOfLineQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>

                SELECT distinct ?sectionOfLine ?length ?solNature ?lineNationalLabel ?imCode ?startOp ?endOp ?track ?country
                WHERE {{
                  ?sectionOfLine a era:SectionOfLine .
                  ?sectionOfLine era:lineNationalId ?lineNationalId .
                  ?lineNationalId rdfs:label ?lineNationalLabel .
                  ?sectionOfLine era:length ?length .
                  ?sectionOfLine era:solNature ?solNature .
                  ?sectionOfLine era:imCode ?imCode .
                  ?sectionOfLine era:opStart ?startOp .
                  ?sectionOfLine era:opEnd ?endOp .
                  ?sectionOfLine era:track ?track .
                  ?sectionOfLine era:inCountry ?country .
                  {Country.toCountryCondition "?sectionOfLine" countries}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let! data =
                    Request.GetAsync
                        endpoint
                        (sectionOfLineQuery countries limit offset)
                        Request.applicationSparqlResults

                return JsonSerializer.Deserialize data
            }

        /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#lineCategory">Category of line</>
        let private isPassengerLine (lineCategories: int array) =
            lineCategories.Length = 0 || lineCategories |> Array.exists (fun c -> c <= 60)

        let private folder (getTrack: Rdf -> Track) (sols: Dictionary<string, SectionOfLine>) (b: Map<string, Rdf>) =
            let id = Properties.toSectionsOfLineId b.["sectionOfLine"]
            let track = getTrack b.["track"]

            Utils.change (
                sols,
                id,
                fun sol ->
                    match sol with
                    | Some sol ->
                        { sol with
                            Tracks = Utils.appendElem track sol.Tracks }
                        |> Some
                    | None ->
                        if isPassengerLine track.lineCategories then
                            { Name = id
                              Country = Properties.toCountryType b.["country"]
                              Length = Properties.toFloat b.["length"]
                              LineIdentification = Properties.toLiteral b.["lineNationalLabel"]
                              IMCode = Properties.toLiteral b.["imCode"]
                              StartOP = Properties.toUOPID b.["startOp"]
                              EndOP = Properties.toUOPID b.["endOp"]
                              Tracks = [| track |] }
                            |> Some
                        else
                            None
            )

        let fromQueryResults (sparql: QueryResults) (tracks: QueryResults) : SectionOfLine[] =
            let dictTracks = Track.fromQueryResults tracks

            let getTrack (r: Rdf) =
                match dictTracks.TryGetValue(Properties.toTrackLabel r) with
                | true, track -> track
                | _ -> raise (System.Exception $"getTrack track not found {r}")

            sparql.results.bindings
            |> Array.fold (folder getTrack) (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/Tunnel">Tunnel</a>
    module Tunnel =
        let private tunnelQuery (countries: string[]) =
            $"""
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX wgs: <http://www.w3.org/2003/01/geo/wgs84_pos#> 

                SELECT distinct ?tunnel ?tunnelIdentification ?length ?startLat ?startLon ?endLat ?endLon ?track ?country
                WHERE {{
                  ?tunnel a era:Tunnel.

                  ?tunnel era:tunnelIdentification ?tunnelIdentification .
                  ?tunnel era:length ?length .
                  ?tunnel era:startLocation ?startlocation .
  				  ?startlocation wgs:lat ?startLat .
  				  ?startlocation wgs:long ?startLon .
                  ?tunnel era:endLocation ?endlocation .
  				  ?endlocation wgs:lat ?endLat .
  				  ?endlocation wgs:long ?endLon .
                  ?track era:passesThroughTunnel ?tunnel.
                  ?tunnel era:inCountry ?country .
                  {Country.toCountryCondition "?tunnel" countries}
                }}
            """

        let loadData (countries: string[]) : Async<QueryResults> =
            async {
                let! data = Request.GetAsync endpoint (tunnelQuery countries) Request.applicationSparqlResults
                return JsonSerializer.Deserialize data
            }

        let private findLine (track: string) (sols: SectionOfLine array) : string option =
            sols
            |> Array.tryFind (fun sol -> sol.Tracks |> Array.exists (fun t -> t.id = track))
            |> Option.map _.LineIdentification

        let private folder (sols: SectionOfLine array) (tunnels: Dictionary<string, Tunnel>) (b: Map<string, Rdf>) =
            let id = Properties.toLiteral b.["tunnelIdentification"]
            let trackLabel = Properties.toTrackLabel b.["track"]

            Utils.change (
                tunnels,
                id,
                fun op ->
                    match op with
                    | Some op ->
                        Some
                            { op with
                                ContainingTracks = Utils.appendElem trackLabel op.ContainingTracks }
                    | None ->
                        match findLine trackLabel sols with
                        | Some lineIdentification ->
                            Some
                                { Name = id
                                  Country = Properties.toCountryType b.["country"]
                                  LineIdentification = lineIdentification
                                  Length = Properties.toFloat b.["length"]
                                  StartLatitude = Properties.toFloat b.["startLat"]
                                  StartLongitude = Properties.toFloat b.["startLon"]
                                  EndLatitude = Properties.toFloat b.["endLat"]
                                  EndLongitude = Properties.toFloat b.["endLon"]
                                  ContainingTracks = [| trackLabel |] }
                        | None -> None // line is no passengerLine
            )

        let fromQueryResults (sparql: QueryResults) (sols: SectionOfLine array) : Tunnel[] =
            sparql.results.bindings
            |> Array.fold (folder sols) (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray
