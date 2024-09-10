namespace OSM.Sparql

type Entry =
    { Url: string
      Ref: string
      Wikipedia: string }

module Api =

    open Sparql

    // https://qlever.cs.uni-freiburg.de/osm-planet
    let endpointPlanet = $"https://qlever.cs.uni-freiburg.de/api/osm-planet"

    // get wikipedia article of railway routes in germany
    let private osmWikipediaQuery () =
        """
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
PREFIX osmrel: <https://www.openstreetmap.org/relation/>
PREFIX ogc: <http://www.opengis.net/rdf#>
SELECT distinct ?id ?ref ?wikipedia WHERE {
  osmrel:51477 ogc:sfContains ?id .
  ?id osmkey:type 'route' .
  { ?id osmkey:route 'tracks' . }
  UNION
  { ?id osmkey:route 'railway' . }
  ?id osmkey:ref ?ref .
  ?id osmkey:wikipedia ?wikipedia .
} LIMIT 2000
"""

    // get wikipedia article via wikidata
    let private osmWikidataQuery () =
        """
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX osmrel: <https://www.openstreetmap.org/relation/>
PREFIX wd: <http://www.wikidata.org/entity/>
PREFIX wdt: <http://www.wikidata.org/prop/direct/>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX schema: <http://schema.org/>
PREFIX osm: <https://www.openstreetmap.org/>
SELECT distinct ?id ?ref ?wikidata ?wikipedia WHERE {
  ?id osmkey:type 'route' .
  { ?id osmkey:route 'tracks' . }
  UNION
  { ?id osmkey:route 'railway' . }
  ?id osmkey:ref ?ref .
  MINUS { ?id osmkey:wikipedia ?_ . }
  ?id osm:wikidata ?wikidata .
  SERVICE <https://qlever.cs.uni-freiburg.de/api/wikidata> {
    ?wikipedia schema:about ?wikidata .
    ?wikipedia schema:isPartOf <https://de.wikipedia.org/> .
  }
} LIMIT 2000
"""

    type QleverResults = { query: string; res: (string[])[] }

    let loadWikipediaArticles (format: string) : Async<string> =
        EraKG.Request.PostAsync endpointPlanet (osmWikipediaQuery ()) format

    let loadWikidataArticles (format: string) : Async<string> =
        EraKG.Request.PostAsync endpointPlanet (osmWikidataQuery ()) format

    let fromQueryResults (sparql: QueryResults) : Entry[] =
        sparql.results.bindings
        |> Array.map (fun b ->
            { Url = b.["id"].value
              Ref = b.["ref"].value
              Wikipedia = b.["wikipedia"].value.Replace("https://de.wikipedia.org/wiki/", "de:") })

    let fromQleverResults (sparql: QleverResults) : Entry[] =
        sparql.res
        |> Array.map (fun b ->
            { Url = b.[0]
              Ref = b.[1]
              Wikipedia = b.[2] })
