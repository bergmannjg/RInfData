# Comparison of ERA knowledge graph and OSM Railway data

Comparison of [ERA](https://www.era.europa.eu/) knowledge graph data and [OSM Railway](https://wiki.openstreetmap.org/wiki/Railways) data.

* ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/)
* [SPARQL Endpoint](https://era-web.linkeddata.es/sparql.html) for ERA data,
* [SPARQL Endpoint](https://qlever.cs.uni-freiburg.de/osm-germany) for OSM data.

## Data models

Short summary of basic concepts.

### RInf concepts

See also [specifications of the register of Infrastructure](https://www.era.europa.eu/sites/default/files/registers/docs/rinf_application_guide_for_register_en.pdf) and ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/).

* **Operational Point** is a location for train service operations with type station, passenger stop, junction, switch etc. OPs have a unique OPID, a location and a list of NationalIdentNum as parameters.
* **Section of Line** is the connection between two adjacent OPs consisting of tracks of the same line with parameter SOLLineIdentification as the National line identification.
* **MaximumPermittedSpeed** is as infrastructure performance parameter of a track describing the maximum permitted speed.
* **National Railway Line** is a Railway line within a member state.

### OSM Railway concepts

The basic [elemets](https://wiki.openstreetmap.org/wiki/Elements) of OSM are [nodes](https://wiki.openstreetmap.org/wiki/Node), [ways](https://wiki.openstreetmap.org/wiki/Way) and [relations](https://wiki.openstreetmap.org/wiki/Relation). Elements can have [tags](https://wiki.openstreetmap.org/wiki/Tags). See also [OpenRailwayMap Tagging Schema](https://wiki.openstreetmap.org/wiki/OpenRailwayMap/Tagging) and [tagging of a train station](https://wiki.openstreetmap.org/wiki/File:A-simple-station.svg).

* **stations** and **halts** as places with railway services are mapped as
  * nodes and ways with tag railway=station|halt and key railway:ref as Station Code
  * or relations with tag public_transport=stop_area and key railway:ref as Station Code.
* **stop positions** as stop positions of the vehicle are mapped as
  * nodes with tag railway=stop and key railway:ref as Station Code
* **tracks** are mapped as ways with tag railway=rail and key ref as Railway line number. The connection between two adjacent stations consists of multiple tracks.
* **maxspeed=*** tag is used on ways to define the maximum legal speed limit.
* **railway lines** are relations of tracks with the tags type=route and route=tracks and key ref as Railway line number.

### OSM data as RDF data

The [OSM to RDF converter](https://ad-publications.cs.uni-freiburg.de/SIGSPATIAL_osm2rdf_BBKL_2021.pdf) make OSM data acceesible via SPARQL queries.

The OSM railway data is loaded with the following SPARQL query:

```SparQl
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX osmkey: <https://www.openstreetmap.org/wiki/Key:>
SELECT distinct ?stop ?public_transport ?railway ?name ?type ?railway_ref ?uic_ref ?railway_ref_DBAG WHERE {
  ?stop osmkey:name ?name .
  ?stop rdf:type ?type .
  { ?stop osmkey:railway ?railway . }
  UNION
  { ?stop osmkey:public_transport ?public_transport . }
  OPTIONAL {
      {?stop osmkey:railway:ref ?railway_ref . }
      UNION
      {?stop osmkey:uic_ref ?uic_ref . }
  }
   OPTIONAL { ?stop osmkey:railway:ref:DBAG ?railway_ref_DBAG . }
}
```

### Comparison of operational points

ERA KG operational points with type station or passenger stop are compared with the above OSM railway data regarding OPID and railway:ref.

|RInf ops|Count|
|---|---:|
|total|6542|
|OSM data found|6195|
|OSM data not found|347|
