# Data of the ERA Register of Railway Infrastructure

F# program to access the ERA (EU Agency for Railways) Register of Infrastructure database and the ERA knowledge graph.

* [RInf](https://www.era.europa.eu/registers_en#rinf) Register of Railway Infrastructure
* [API](https://rinf.era.europa.eu/API/Help) to the the RINF database
* ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/)
* [ERA knowledge graph](https://era-web.linkeddata.es/sparql.html).
* Linked data in the knowledge graph: [Berlin Hauptbahnhof](http://data.europa.eu/949/functionalInfrastructure/operationalPoints/DE000BL).

## Program

* [RInfApi](src/RInfApi): implements the API
* [RInfLoader](src/RInfLoader): loads the data.
* [ERAKGApi](src/EraKGApi): API for the knowledge graph.
* [ERAKGLoader](src/EraKGLoader): loads the data via the knowledge graph.
* [RInfQuery](src/RInfQuery): query cached data.
* [RInfGraph](src/RInfGraph): finds shortest path between operational points
* [OSMComparison](src/OSMComparison): compares RInf data with OSM data.
