# Data of the ERA Register of Railway Infrastructure

F# program to access the ERA (EU Agency for Railways) Register of Infrastructure database and the ERA knowledge graph.

* [RInf](https://www.era.europa.eu/domains/registers/rinf_en) Register of Railway Infrastructure
* ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/)
* Find entities in the [ERA knowledge graph](https://data-interop.era.europa.eu/).
* Linked data in the knowledge graph: [Berlin Hauptbahnhof](https://graph.data.era.europa.eu/resource?uri=http:%2F%2Fdata.europa.eu%2F949%2FoperationalPoint%2FDE000BL&role=object).

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
* [TargetedMosp](src/TargetedMosp): the multiobjective Dijkstra algorithm
* [RInfGraphWeb](src/RInfGraphWeb): RInfGraph web app
* [OSMComparison](src/OSMComparison): compares RInf data with OSM data.
