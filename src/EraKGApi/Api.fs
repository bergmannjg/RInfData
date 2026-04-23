// see interactive endpoint https://data-interop.era.europa.eu/endpoint
// see sparql endpoint https://data-interop.era.europa.eu/api/sparql
namespace EraKG

open FSharp.Collections
open System.Text.Json
open System.Collections.Generic

open RInf.Types

type Country =
    {
        /// ISO 3166-1 alpha-3, https://en.wikipedia.org/wiki/ISO_3166-1_alpha-3
        Code: string
        Label: string
    }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#nationalLine
type RailwayLocation =
    {
        NationalIdentNum: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#kilometer
        Kilometer: float<km>
    }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#OperationalPoint
type OperationalPoint =
    {
        Id: string
        Name: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#opType
        Type: string
        Country: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#uopid
        UOPID: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#netReference
        Latitude: float<degree>
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#netReference
        Longitude: float<degree>
        RailwayLocations: RailwayLocation[]
    }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#opType
type OpType =
    { Label: string
      Definition: string
      Value: int }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#Track
type Track =
    {
        id: string
        label: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#maximumPermittedSpeed
        maximumPermittedSpeed: int<km / h> option
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#minimumHorizontalRadius
        minimumHorizontalRadius: int<m> option
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#contactLineSystemType
        contactLineSystem: string option
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#lineCategory
        lineCategories: int[]
    }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#SectionOfLine
type SectionOfLine =
    {
        Name: string
        Country: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#lengthOfSectionOfLine
        Length: float<km>
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#nationalLine
        LineIdentification: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#opStart
        StartOP: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#opEnd
        EndOP: string
        Tracks: Track[]
    }

/// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#Tunnel
type Tunnel =
    {
        Name: string
        Country: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#lengthOfTunnel
        Length: float<m>
        /// https://rinf.data.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/lineNationalId
        LineIdentification: string
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#startLocation
        StartLatitude: float<degree>
        StartLongitude: float<degree>
        StartKm: float<km>
        /// https://rinf.data.era.europa.eu/era-vocabulary/rinf-appGuide/#endLocation
        EndLatitude: float<degree>
        EndLongitude: float<degree>
        EndKm: float<km>
    }

module Api =

    open Sparql

    let private endpoint = "https://rinf.data.era.europa.eu/api/v1/sparql/rinf"

#if DEBUG
    let private verbose =
        try
            not (isNull (System.Environment.GetEnvironmentVariable "ERAKGVerbose"))
        with _ ->
            false
#else
    let private verbose = false
#endif

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
                if not (r.value.StartsWith prefix) then
                    raise (System.Exception $"uriTypeToString: prefix '{prefix}' value '{r.value}' unexpected")

                System.Web.HttpUtility.UrlDecode(r.value.Substring prefix.Length)
            else
                raise (System.Exception $"uriTypeToString: type {r.``type``} unexpected")

        let toOpType (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/concepts/op-types/"

        let toCountryType (r: Rdf) : string =
            uriTypeToString r "http://publications.europa.eu/resource/authority/country/"

        let toSectionsOfLineId (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/sectionOfLine/"

        let toLineCategory (r: Rdf) : int =
            let s = uriTypeToString r "http://data.europa.eu/949/concepts/line-category/"

            match System.Int32.TryParse s with
            | true, value -> value
            | _ -> raise (System.Exception $"toLineCategory int expected {r}")

        let toTrackLabel (r: Rdf) =
            uriTypeToString r "http://data.europa.eu/949/track/"

        let toUOPID (r: Rdf) : string =
            uriTypeToString r "http://data.europa.eu/949/operationalPoint/"

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

        // example label "Railway location of OP Hamburg Norderelbbrücke, km 352.474 on line 2200"
        let toRailwayLocation (netReferenceLabel: Rdf) : RailwayLocation option =
            let label = toLiteral netReferenceLabel
            let index1 = label.IndexOf " km "
            let index2 = label.IndexOf " on line "

            if 0 <= index1 && 0 <= index2 then
                let nationalIdentNum = label.Substring(index2 + 9)
                let kilometer = label.Substring(index1 + 4, index2 - (index1 + 4))

                Some
                    { NationalIdentNum = nationalIdentNum
                      Kilometer = 1.0<km> * float kilometer }
            else
                fprintfn stderr $"toRailwayLocation: string not found in '{label}'"
                None

        // return (startLongitude, startLatitude, endLongitude, endLatitude)
        let fromWKT (wkt: Rdf) : (float * float * float * float) option =
            let label = toLiteral wkt
            let pattern = "LINESTRING \(([0-9.]*)\s+([0-9.]*),\s*([0-9.]*)\s+([0-9.]*)\)"
            let m = System.Text.RegularExpressions.Regex.Match(label, pattern)

            if m.Success && m.Groups.Count = 5 then
                Some(float m.Groups[1].Value, float m.Groups[2].Value, float m.Groups[3].Value, float m.Groups[4].Value)
            else
                fprintfn stderr $"fromWKT: string not found in '{label}'"
                None

        // https://rinf.data.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/validity
        let currentlyValidIfExists (item: string) : string =
            $"""
              {{ FILTER NOT EXISTS {{ ?{item} era:validity ?validity }} }}
              UNION 
              {{ 
                ?{item} era:validity ?validity{item} .
                ?validity{item} time:hasBeginning ?timeBeginning{item} .
                ?timeBeginning{item} time:inXSDDate ?xsdTimeBeginning{item} .
                ?validity{item} time:hasEnd ?timeEnd{item} .
                ?timeEnd{item} time:inXSDDate ?xsdTimeEnd{item} .
                BIND(xsd:dateTime(NOW()) AS ?now{item}) .
                Filter(?xsdTimeBeginning{item} <= ?now{item} && ?now{item} <= ?xsdTimeEnd{item}) .
              }}
            """

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/#http://data.europa.eu/949/inCountry">Countries and territories</a>
    module Country =
        let private countriesQuery () =
            $"""
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX skos: <http://www.w3.org/2004/02/skos/core#>

                SELECT distinct ?country ?label
                WHERE {{
                  ?operationalPoint a era:OperationalPoint .
                  ?operationalPoint era:inCountry ?country .
                  ?country skos:prefLabel ?label .
              	  FILTER(lang(?label) = "en") .           
                }}
            """

        let loadData () : Async<QueryResults> =
            async {
                let query = countriesQuery ()

                if verbose then
                    fprintfn stderr $"Country query {query}"

                let! data = Request.GetAsync endpoint query
                return JsonSerializer.Deserialize data
            }

        let fromQueryResults (sparql: QueryResults) : Country[] =
            sparql.results.bindings
            |> Array.map (fun b ->
                { Code = Properties.toCountryType b.["country"]
                  Label = Properties.toLiteral b.["label"] })

        let toCountryCondition (item: string) (countries: string[]) : string =
            countries
            |> Array.map (fun country ->
                $"{{ {item} era:inCountry <http://publications.europa.eu/resource/authority/country/{country}> . }}")
            |> String.concat " UNION "

        let Cache =
            """
                [{"Code":"AUT","Label":"Austria"},{"Code":"ITA","Label":"Italy"},{"Code":"SWE","Label":"Sweden"},{"Code":"FRA","Label":"France"},{"Code":"LVA","Label":"Latvia"},{"Code":"BGR","Label":"Bulgaria"},{"Code":"DEU","Label":"Germany"},{"Code":"CHE","Label":"Switzerland"},{"Code":"SVN","Label":"Slovenia"},{"Code":"POL","Label":"Poland"},{"Code":"BEL","Label":"Belgium"},{"Code":"SVK","Label":"Slovakia"},{"Code":"FIN","Label":"Finland"},{"Code":"EST","Label":"Estonia"},{"Code":"GRC","Label":"Greece"},{"Code":"HUN","Label":"Hungary"},{"Code":"DNK","Label":"Denmark"},{"Code":"PRT","Label":"Portugal"},{"Code":"LTU","Label":"Lithuania"},{"Code":"IRL","Label":"Ireland"},{"Code":"LUX","Label":"Luxembourg"},{"Code":"ESP","Label":"Spain"},{"Code":"ROU","Label":"Romania"},{"Code":"NOR","Label":"Norway"},{"Code":"NLD","Label":"Netherlands"},{"Code":"CZE","Label":"Czechia"}]

            """

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#OperationalPoint">Operational Point</a>
    module OperationalPoint =
        let private operationalPointQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX wgs: <http://www.w3.org/2003/01/geo/wgs84_pos#> 
                PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
                PREFIX time: <http://www.w3.org/2006/time#>

                SELECT distinct ?operationalPoint ?opName ?uopid ?opType ?lat ?lon ?netReferenceLabel ?country
                WHERE {{
                  ?operationalPoint a era:OperationalPoint .
                  ?operationalPoint era:opName ?opName .
                  ?operationalPoint era:uopid ?uopid .
                  ?operationalPoint era:netReference ?netReference .
                  ?netReference rdfs:label ?netReferenceLabel .
                  ?netReference wgs:lat ?lat .
                  ?netReference wgs:long ?lon .
                  ?operationalPoint era:opType ?opType .
                  ?operationalPoint era:inCountry ?country .
                  {Country.toCountryCondition "?operationalPoint" countries}
                  {Properties.currentlyValidIfExists "operationalPoint"}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let query = operationalPointQuery countries limit offset

                if verbose && offset = 0 then
                    fprintfn stderr $"OperationalPoint query {query}"

                let! data = Request.GetAsync endpoint query

                return JsonSerializer.Deserialize data
            }

        let private folder (ops: Dictionary<string, OperationalPoint>) (b: Map<string, Rdf>) =
            let id = Properties.toUOPID b.["operationalPoint"]

            match Properties.toRailwayLocation b.["netReferenceLabel"] with
            | Some railwayLocation ->
                Utils.change (
                    ops,
                    id,
                    fun op ->
                        match op with
                        | Some op ->
                            Some
                                { op with
                                    RailwayLocations = Utils.appendElem railwayLocation op.RailwayLocations }
                        | None ->
                            Some
                                { Id = Properties.toUOPID b.["operationalPoint"]
                                  Name = Properties.toLiteral b.["opName"]
                                  Type = Properties.toOpType b.["opType"]
                                  Country = Properties.toCountryType b.["country"]
                                  UOPID = Properties.toLiteral b.["uopid"]
                                  Latitude = 1.0<degree> * Properties.toFloat b.["lat"]
                                  Longitude = 1.0<degree> * Properties.toFloat b.["lon"]
                                  RailwayLocations = [| railwayLocation |] }
                )
            | None -> ops

        let fromQueryResults (sparql: QueryResults) : OperationalPoint[] =
            sparql.results.bindings
            |> Array.fold folder (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray

    module OpTypes =
        let private opTypesQuery =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
                PREFIX era: <http://data.europa.eu/949/>
                SELECT distinct ?n ?l ?d WHERE {{
                  ?c a skos:Concept .
                  ?c skos:inScheme ?s .
                  ?s rdfs:label "Operational Point Types"@en .
                  ?c skos:notation ?n .
                  ?c skos:definition ?d .
                  ?c skos:prefLabel ?l .
                  FILTER (LANG(?l) = 'en')
                }}
                ORDER BY ?n
            """

        let loadData () : Async<QueryResults> =
            async {
                let! data = Request.GetAsync endpoint opTypesQuery

                return JsonSerializer.Deserialize data
            }

        let fromQueryResults (sparql: QueryResults) : OpType[] =
            sparql.results.bindings
            |> Array.map (fun rdf ->
                { Definition = Properties.toLiteral rdf["d"]
                  Label = Properties.toLiteral rdf["l"]
                  Value = int (Properties.toLiteral rdf["n"]) })

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#Track">Track</a>
    module Track =
        let private trackQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
                PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
                PREFIX rdf:	<http://www.w3.org/1999/02/22-rdf-syntax-ns#>
                PREFIX time: <http://www.w3.org/2006/time#>

                SELECT distinct ?track ?label ?lineCategory ?contactLineSystemTypeLabel ?maximumPermittedSpeed ?minimumHorizontalRadius
                WHERE {{
                  ?sectionOfLine a era:SectionOfLine .
                  ?sectionOfLine era:hasPart ?track .
                  ?track rdf:type era:RunningTrack .
                  
                  OPTIONAL {{ ?track rdfs:label ?label . }}
                  OPTIONAL {{ ?track era:lineCategory ?lineCategory . }}
                  OPTIONAL {{ ?track era:contactLineSystem ?contactLineSystem . 
                              ?contactLineSystem era:contactLineSystemType ?contactLineSystemType .
    						  ?contactLineSystemType skos:prefLabel ?contactLineSystemTypeLabel . }}
                  OPTIONAL {{ ?track era:maximumPermittedSpeed ?maximumPermittedSpeed . }}
                  OPTIONAL {{ ?track era:minimumHorizontalRadius ?minimumHorizontalRadius . }}

                  {Country.toCountryCondition "?sectionOfLine" countries}
                  {Properties.currentlyValidIfExists "track"}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let query = trackQuery countries limit offset

                if verbose && offset = 0 then
                    fprintfn stderr $"Track query {query}"

                let! data = Request.GetAsync endpoint query

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
                | true, rdf -> Some(1<km / h> * Properties.toInt rdf)
                | false, _ -> Some(1<km / h> * 100)
              minimumHorizontalRadius =
                match b.TryGetValue "minimumHorizontalRadius" with
                | true, rdf -> Some(1<m> * Properties.toInt rdf)
                | false, _ -> None
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

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#SectionOfLine">SectionOfLine</a>
    module SectionOfLine =
        let private sectionOfLineQuery (countries: string[]) (limit: int) (offset: int) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
                PREFIX time: <http://www.w3.org/2006/time#>

                SELECT distinct ?sectionOfLine ?length ?solNature ?lineId ?startUopid ?endUopid ?track ?country
                WHERE {{
                  ?sectionOfLine a era:SectionOfLine .
                  ?sectionOfLine era:nationalLine ?nationalLine .
                  ?nationalLine era:lineId ?lineId .
                  ?sectionOfLine era:lengthOfSectionOfLine ?length .
                  ?sectionOfLine era:solNature ?solNature .
                  ?sectionOfLine era:opStart ?startOp .
                  ?startOp era:uopid ?startUopid .
                  ?sectionOfLine era:opEnd ?endOp .
                  ?endOp era:uopid ?endUopid .
                  ?sectionOfLine era:hasPart ?track .
                  ?sectionOfLine era:inCountry ?country .
                  {Country.toCountryCondition "?sectionOfLine" countries}
                  {Properties.currentlyValidIfExists "sectionOfLine"}
                }} LIMIT {limit} OFFSET {offset}
            """

        let loadData (countries: string[]) (limit: int) (offset: int) : Async<QueryResults> =
            async {
                let query = sectionOfLineQuery countries limit offset

                if verbose && offset = 0 then
                    fprintfn stderr $"SectionOfLine query {query}"

                let! data = Request.GetAsync endpoint query

                return JsonSerializer.Deserialize data
            }

        /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#lineCategory">Category of line</>
        let private isPassengerLine (lineCategories: int array) =
            lineCategories.Length = 0 || lineCategories |> Array.exists (fun c -> c <= 60)

        let private folder
            (getTrack: Rdf -> Track option)
            (sols: Dictionary<string, SectionOfLine>)
            (b: Map<string, Rdf>)
            =
            match getTrack b.["track"] with
            | Some track ->
                let id = Properties.toSectionsOfLineId b.["sectionOfLine"]

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
                                  Length = 1.0<km> * Properties.toFloat b.["length"]
                                  LineIdentification = Properties.toLiteral b.["lineId"]
                                  StartOP = Properties.toLiteral b.["startUopid"]
                                  EndOP = Properties.toLiteral b.["endUopid"]
                                  Tracks = [| track |] }
                                |> Some
                            else
                                None
                )
            | _ -> sols

        let fromQueryResults (sparql: QueryResults) (tracks: QueryResults) : SectionOfLine[] =
            let dictTracks = Track.fromQueryResults tracks

            let getTrack (r: Rdf) =
                match dictTracks.TryGetValue(Properties.toTrackLabel r) with
                | true, track -> Some track
                | _ -> None

            sparql.results.bindings
            |> Array.fold (folder getTrack) (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray

    /// see <a href="https://data-interop.era.europa.eu/era-vocabulary/rinf-appGuide/#tunnel">Tunnel</a>
    module Tunnel =
        let private tunnelQuery (countries: string[]) =
            $"""
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                PREFIX era: <http://data.europa.eu/949/>
                PREFIX wgs: <http://www.w3.org/2003/01/geo/wgs84_pos#> 
                PREFIX rdf:	<http://www.w3.org/1999/02/22-rdf-syntax-ns#>
                PREFIX geo: <http://www.opengis.net/ont/geosparql#> 

                SELECT distinct ?tunnel ?tunnelIdentification ?length  ?labelStartsAt ?labelEndsAt ?wkt ?country
                WHERE {{
                  ?tunnel a era:Tunnel.

                  ?tunnel era:tunnelIdentification ?tunnelIdentification .
                  ?tunnel era:lengthOfTunnel ?length .
                  ?tunnel era:netReference ?netReference .
                  ?netReference geo:hasGeometry ?geo .
  				  ?geo geo:asWKT ?wkt .
				  ?netReference era:endsAt ?endsAt .
  				  ?endsAt era:hasLrsCoordinate ?endsAtCoordinate .
                  ?endsAtCoordinate rdfs:label ?labelEndsAt .
				  ?netReference era:startsAt ?startsAt .
  				  ?startsAt era:hasLrsCoordinate ?startsAtCoordinate .
                  ?startsAtCoordinate rdfs:label ?labelStartsAt .
                  ?tunnel era:isPartOf ?track .
                  ?track rdf:type era:RunningTrack .
				  ?tunnel era:isPartOf ?track .
				  ?track era:isPartOf ?sectionOfLine .
                  ?sectionOfLine era:inCountry ?country .
                  {Country.toCountryCondition "?sectionOfLine" countries}
                }}
            """

        let loadData (countries: string[]) : Async<QueryResults> =
            async {
                let query = tunnelQuery countries

                if verbose then
                    fprintfn stderr $"SectionOfLine query {query}"

                let! data = Request.GetAsync endpoint query
                return JsonSerializer.Deserialize data
            }

        let private folder (sols: SectionOfLine array) (tunnels: Dictionary<string, Tunnel>) (b: Map<string, Rdf>) =
            let name = Properties.toLiteral b.["tunnelIdentification"]

            Utils.change (
                tunnels,
                name,
                fun op ->
                    match op with
                    | Some op -> Some op
                    | None ->
                        match
                            Properties.toRailwayLocation b.["labelStartsAt"],
                            Properties.toRailwayLocation b.["labelEndsAt"],
                            Properties.fromWKT b.["wkt"]
                        with
                        | Some startsAt, Some endsAt, Some(startLongitude, startLatitude, endLongitude, endLatitude) ->
                            Some
                                { Name = name
                                  Country = Properties.toCountryType b.["country"]
                                  LineIdentification = startsAt.NationalIdentNum
                                  Length = 1.0<m> * Properties.toFloat b.["length"]
                                  StartLatitude = 1.0<degree> * startLatitude
                                  StartLongitude = 1.0<degree> * startLongitude
                                  StartKm = startsAt.Kilometer
                                  EndLatitude = 1.0<degree> * endLatitude
                                  EndLongitude = 1.0<degree> * endLongitude
                                  EndKm = endsAt.Kilometer }
                        | _ -> None // line is no passengerLine
            )

        let fromQueryResults (sparql: QueryResults) (sols: SectionOfLine array) : Tunnel[] =
            sparql.results.bindings
            |> Array.fold (folder sols) (Dictionary sparql.results.bindings.Length)
            |> _.Values
            |> Seq.toArray
