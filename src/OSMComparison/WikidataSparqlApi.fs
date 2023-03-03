namespace Wikidata.Sparql

type Entry =
    { Station: string
      Name: string
      IBNR: string option
      stationCode: string option }

module Api =

    open Sparql

    let private endpoint = "https://qlever.cs.uni-freiburg.de/api/wikidata"

    // railway stations (Q55488) in germany (Q183) from wikidata
    let private osmQuery () =
        $"""
PREFIX wd: <http://www.wikidata.org/entity/>
PREFIX wdt: <http://www.wikidata.org/prop/direct/>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
SELECT DISTINCT ?name ?IBNR ?stationCode ?station WHERE {{
?station wdt:P31/wdt:P279* wd:Q55488 .
?station wdt:P17 wd:Q183 .
Optional {{ ?station wdt:P954 ?IBNR . }}
Optional {{
  ?station wdt:P296|wdt:P8671 ?stationCode .
  FILTER regex(?stationCode, "[A-Z]+") 
}}
?station rdfs:label ?name .
FILTER (LANG(?name) = "de")
}}
"""

    let loadOsmData () : Async<string> =
        EraKG.Request.GetAsync endpoint (osmQuery ()) EraKG.Request.applicationSparqlResults

    let ToEntries (sparql: QueryResults) : Entry [] =
        sparql.results.bindings
        |> Array.map (fun b ->
            { Station = b.["station"].value
              Name = b.["name"].value
              stationCode =
                if b.ContainsKey "stationCode" then
                    Some b.["stationCode"].value
                else
                    None
              IBNR =
                if b.ContainsKey "IBNR" then
                    Some b.["IBNR"].value
                else
                    None })
