namespace OSM.Sparql

open RInf.Types

type Entry =
    { Url: string
      Name: string
      Railway: string
      RailwayRef: string
      UicRef: string option
      Operator: string option
      Latitude: float<degree>
      Longitude: float<degree>
      OsmType: string }

module Api =

    open Sparql

#if DEBUG
    let private verbose =
        try
            not (isNull (System.Environment.GetEnvironmentVariable "QLEVERVerbose"))
        with _ ->
            false
#else
    let private verbose = false
#endif


    // search in germany and switzerland
    let private osmQuery () =
        """
PREFIX geo: <http://www.opengis.net/ont/geosparql#>
PREFIX geof: <http://www.opengis.net/def/function/geosparql/>
PREFIX osmrel: <https://www.openstreetmap.org/relation/>
PREFIX ogc: <http://www.opengis.net/rdf#>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
SELECT distinct ?id (geof:latitude(?centroid) AS ?lat) (geof:longitude(?centroid) AS ?lng) ?railway ?name ?type ?railway_ref ?uic_ref ?operator WHERE {
  { osmrel:51477 ogc:sfContains ?id . } UNION { osmrel:51701 ogc:sfContains ?id . }
  { ?id osmkey:name ?name . } UNION { ?id osmkey:disused:name ?name . }
  ?id rdf:type ?type .
  { ?id osmkey:railway ?railway . } 
  UNION 
  { ?id osmkey:disused:railway ?railway . }
  UNION 
  { ?id osmkey:proposed:railway ?railway . }
  UNION 
  { ?id osmkey:public_transport ?railway .
    FILTER (STR(?railway) = "stop_area") }
  { ?id osmkey:railway:ref ?railway_ref . } UNION { ?id osmkey:railway:ref:DBAG ?railway_ref . } UNION { ?id osmkey:disused:railway:ref ?railway_ref . }
  OPTIONAL { ?id osmkey:uic_ref ?uic_ref . }
  OPTIONAL { ?id osmkey:operator ?operator . }
  ?id geo:hasGeometry/geo:asWKT ?location .
  BIND(geof:centroid(?location) AS ?centroid)}
"""

    let loadData (endpoint: string) : Async<string> =
        let query = osmQuery ()

        if verbose then
            fprintfn stderr $"osm: endpoint {endpoint}, query {query}"

        EraKG.Request.PostAsync endpoint query

    let private toOptLiteral (b: Map<string, Rdf>) (key: string) =
        match b.TryGetValue key with
        | true, v -> Some(EraKG.Api.Properties.toLiteral v)
        | _ -> None

    let fromQueryResults (sparql: QueryResults) : Entry[] =
        sparql.results.bindings
        |> Array.map (fun b ->
            { Url = EraKG.Api.Properties.uriTypeToString b.["id"] ""
              Name = EraKG.Api.Properties.toLiteral b.["name"]
              Railway = EraKG.Api.Properties.toLiteral b.["railway"]
              RailwayRef = EraKG.Api.Properties.toLiteral b.["railway_ref"]
              UicRef = toOptLiteral b "uic_ref"
              Operator = toOptLiteral b "operator"
              Latitude = 1.0<degree> * EraKG.Api.Properties.toFloat b.["lat"]
              Longitude = 1.0<degree> * EraKG.Api.Properties.toFloat b.["lng"]
              OsmType = EraKG.Api.Properties.uriTypeToString b.["type"] "https://www.openstreetmap.org/" })
