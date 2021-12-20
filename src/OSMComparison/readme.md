# Comparison of RInf and OSM Railway data

Comparison of [RInf](https://www.era.europa.eu/registers_en#rinf) data and [OSM Railway](https://wiki.openstreetmap.org/wiki/Railways) data.

## Prerequisites

* [local installation](scripts/docker-overpass.sh) of overpass api
* download RInf data with [RInfLoader](https://github.com/bergmannjg/RInfData/tree/main/src/RInfLoader).

## Data models

Short summary of basic concepts.

### RInf concepts

See also [specifications of the register of Infrastructure](https://www.era.europa.eu/sites/default/files/registers/docs/rinf_application_guide_for_register_en.pdf) and ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/).

* **Operational Point** is a location for train service operations with type station, passenger stop, junction, switch etc. OPs have a unique OPID, a location and a list of NationalIdentNum as parameters.
* **Section of Line** is the connection between two adjacent OPs consisting of tracks of the same line with parameter SOLLineIdentification as the National line identification.
* **IPP_MaxSpeed** is as infrastructure performance parameter of a track describing the maximum permitted speed.
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

possible connection of a station (node) with a railway line (relation):

* node [railway=station] s is part of relation r [type=public_transport]
* relation r has member node sn [railway=stop]
* node sn is element of nodes of way t [ref=linenumber]
* way t is part of relation [route=tracks][ref=linenumber].

### Conceptual correspondence

* OSM tags correspond to RInf parameters.
* OSM stations, stops and halts as nodes correspond to RInf operational points.
* The OPs OPID corrrsponds to the station railway:ref.

### Comparison of data

* location distances of operational points/stations
* max speed of railway lines
* etc.

## Preliminary results

RInf has data for 1028 railway lines.

OSM data with relation [route=tracks] was found for 976 railway lines.

### Comparison of operational points

RInf operational points with type station or passenger stop are compared.

|OSM/RInf ops matched|Count|
|---|---:|
|total|6609|
|by OpId|6606|
|by OpIdParent|3|

|Reason for mismatch|Remark|Count|
|---|---|---:|
|DistanceToWaysOfLine|Matched but RInf ops distance to line more then 1 km|40|
|DistanceToStop|Matched but RInf ops distance to osm stop more then 1 km|26|
|OsmNotYetMapped|Unmatched, not yet mapped|67|
|DisusedStation|Unmatched, not mapped disused stations|29|
|Unexpected|Unmatched, check if op is disused station or should be mapped|147|
||total|309|

### Comparison of sections of line

|Type|Count|
|---|---:|
|RInf railway lines|1028|
|OSM railway lines with tag [route=tracks]|976|
|RInf railway lines with more than 1 operational point|599|
|OSM railway lines with more than 1 operational point|594|
|OSM railway lines with section of line|541|
|OSM railway lines with maxspeed data|468|
|OSM railway lines with maxspeed difference more than 50 km|47|
