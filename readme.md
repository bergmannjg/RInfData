# Data of the Register of Railway Infrastructure

F# program to access the Register of Railway Infrastructure database.

* [RInf](https://www.era.europa.eu/registers_en#rinf) Register of Railway Infrastructure
* [API](https://rinf.era.europa.eu/API/Help) to the the RINF database

## Program

* [RInfApi](src/RInfApi): implements the API
* [RInfGraph](src/RInfGraph): finds shortest path between operational points
* [RInfLoader](src/RInfLoader): loads the data.
* [OSMComparison](src/OSMComparison): compares RInf data with OSM data.
