# Data of the ERA Register of Railway Infrastructure

F# program to access the ERA (EU Agency for Railways) Register of Infrastructure database and the ERA knowledge graph.

* [RInf](https://www.era.europa.eu/domains/registers/rinf_en) Register of Railway Infrastructure
* [API](https://rinf.era.europa.eu/API/Help) to the the RINF database
* ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/)
* [ERA knowledge graph](https://era-web.linkeddata.es/sparql.html).
* Linked data in the knowledge graph: [Berlin Hauptbahnhof](http://data.europa.eu/949/functionalInfrastructure/operationalPoints/DE000BL).

## Package

The rinf-graph JavaScript package contains the RInfGraph JavaScript code and the EraKGLoader executable
to load data from the knowledge graph ([build-package](./scripts/build-package.sh)).

Installation in JavaScript project:

* install [dotnet](https://dotnet.microsoft.com/en-us/)
* install rinf-graph package
* execute *./node_modules/rinf-graph/bin/EraKGLoader &lt;countries&gt;*

## Program

* [ERAKGApi](src/EraKGApi): API for the knowledge graph.
* [ERAKGLoader](src/EraKGLoader): loads the data via the knowledge graph.
* [RInfQuery](src/RInfQuery): query cached data.
* [RInfGraph](src/RInfGraph): finds shortest path between operational points
* [OSMComparison](src/OSMComparison): compares RInf data with OSM data.
