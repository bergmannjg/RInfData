namespace OSM

type Entry =
    { Name: string
      Railway: string
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
SELECT distinct ?name ?railway ?railway_ref ?uic_ref ?type WHERE {{
  ?stop osmkey:railway ?railway .
  ?stop osmkey:name ?name .
  ?stop rdf:type ?type .
  {{?stop osmkey:railway:ref ?railway_ref . }}
  UNION
  {{?stop osmkey:uic_ref ?uic_ref . }}
}}
"""

    let loadOsmData () : Async<string> =
        EraKG.Request.GetAsync endpoint (osmQuery ()) EraKG.Request.applicationSparqlResults

    let toEntries (sparql: QueryResults) : Entry [] =
        sparql.results.bindings
        |> Array.map (fun b ->
            { Name = b.["name"].value
              Railway = b.["railway"].value
              RailwayRef =
                if b.ContainsKey "railway_ref" then
                    Some b.["railway_ref"].value
                else
                    None
              UicRef =
                if b.ContainsKey "uic_ref" then
                    Some b.["uic_ref"].value
                else
                    None
              OsmType = b.["type"].value })
