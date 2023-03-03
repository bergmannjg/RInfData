namespace OSM.Sparql

type Entry =
    { Stop: string
      Name: string
      PublicTransport: string option
      Railway: string option
      RailwayRef: string option
      UicRef: string option
      OsmType: string }

module Api =

    open Sparql

    let private endpoint = "https://qlever.cs.uni-freiburg.de/api/osm-germany"

    let private osmQuery () =
        $"""
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
SELECT distinct ?stop ?public_transport ?railway ?name ?type ?railway_ref ?uic_ref ?railway_ref_DBAG WHERE {{
  ?stop osmkey:name ?name .
  ?stop rdf:type ?type .
  {{ ?stop osmkey:railway ?railway . }}
  UNION
  {{ ?stop osmkey:public_transport ?public_transport . }}
  OPTIONAL {{ 
      {{?stop osmkey:railway:ref ?railway_ref . }}
      UNION
      {{?stop osmkey:uic_ref ?uic_ref . }}
  }}
  OPTIONAL {{ ?stop osmkey:railway:ref:DBAG ?railway_ref_DBAG . }}
}}
"""

    let loadOsmData () : Async<string> =
        EraKG.Request.GetAsync endpoint (osmQuery ()) EraKG.Request.applicationSparqlResults

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
              OsmType = b.["type"].value })
