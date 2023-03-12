namespace OSM.Sparql

type Entry =
    { Stop: string
      Name: string
      PublicTransport: string option
      Railway: string option
      RailwayRef: string option
      UicRef: string option
      Operator: string option
      Latitude: float
      Longitude: float
      OsmType: string }

module Api =

    open Sparql

    let private osmQuery () =
        """
PREFIX geo: <http://www.opengis.net/ont/geosparql#>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
SELECT distinct ?stop ?location ?public_transport ?railway ?name ?type ?railway_ref ?uic_ref ?railway_ref_DBAG ?operator WHERE {
  ?stop osmkey:name ?name .
  ?stop rdf:type ?type .
  ?stop geo:hasGeometry ?location .
  { ?stop osmkey:railway ?railway . }
  UNION
  { ?stop osmkey:public_transport ?public_transport . }
  OPTIONAL { 
      {?stop osmkey:railway:ref ?railway_ref . }
      UNION
      {?stop osmkey:uic_ref ?uic_ref . }
  }
  OPTIONAL { ?stop osmkey:railway:ref:DBAG ?railway_ref_DBAG . }
  OPTIONAL { ?stop osmkey:operator ?operator . }
}
"""

    type Point = { Latitude: float; Longitude: float }

    // point in WKT format
    let toFirstPoint (s: string) : Point =
        try
            let split =
                if s.StartsWith "LINESTRING(" then
                    s
                        .Replace("LINESTRING(", "")
                        .Replace(",", " ")
                        .Split [| ' ' |]
                else if s.StartsWith "MULTIPOLYGON(((" then
                    s
                        .Replace("MULTIPOLYGON(((", "")
                        .Replace(",", " ")
                        .Split [| ' ' |]
                else
                    s
                        .Replace("POINT(", "")
                        .Replace(")", "")
                        .Split [| ' ' |]

            { Latitude = float split.[1]
              Longitude = float split.[0] }
        with
        | _ ->
            fprintfn stderr $"error parse point '{s}'"
            { Latitude = 0.0; Longitude = 0.0 }

    let loadOsmData (endpoint : string) : Async<string> =
        EraKG.Request.PostAsync endpoint (osmQuery ()) EraKG.Request.applicationSparqlResults

    let ToEntries (sparql: QueryResults) : Entry [] =
        sparql.results.bindings
        |> Array.map (fun b ->
            { Stop = b.["stop"].value
              Name = b.["name"].value
              PublicTransport =
                if b.ContainsKey "public_transport" then
                    Some b.["public_transport"].value
                else
                    None
              Railway =
                if b.ContainsKey "railway" then
                    Some b.["railway"].value
                else
                    None
              RailwayRef =
                if b.ContainsKey "railway_ref" then
                    Some b.["railway_ref"].value
                else if b.ContainsKey "railway_ref_DBAG" then
                    Some b.["railway_ref_DBAG"].value
                else
                    None
              UicRef =
                if b.ContainsKey "uic_ref" then
                    Some b.["uic_ref"].value
                else
                    None
              Operator =
                if b.ContainsKey "operator" then
                    Some b.["operator"].value
                else
                    None
              Latitude =
                if b.ContainsKey "location" then
                    (toFirstPoint b.["location"].value).Latitude
                else
                    0.0
              Longitude =
                if b.ContainsKey "location" then
                    (toFirstPoint b.["location"].value).Longitude
                else
                    0.0
              OsmType = b.["type"].value })
