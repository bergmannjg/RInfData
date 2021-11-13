# Comparison of RInf and OSM Railway data

Comparison of [RInf](https://www.era.europa.eu/registers_en#rinf) data and [OSM Railway](https://wiki.openstreetmap.org/wiki/Railways) data.

## Prerequisites

* [local installation](https://wiki.openstreetmap.org/wiki/Overpass_API/Installation) of overpass api
* download RInf data with [RInfLoader](https://github.com/bergmannjg/RInfData/tree/main/src/RInfLoader).

## Data models

Short summary of basic concepts.

### RInf concepts

See also [specifications of the register of Infrastructure](https://www.era.europa.eu/sites/default/files/registers/docs/rinf_application_guide_for_register_en.pdf).

* **Operational Point** with type station, passenger terminal, junction, switch etc. OPs have a unique OPID, a location and a list of NationalIdentNum as parameters.
* **Section of line** is the connection between two adjacent OPs consisting of tracks of the same line with parameter SOLLineIdentification as the National line identification.

### OSM Railway concepts

The basic [elemets](https://wiki.openstreetmap.org/wiki/Elements) of OSM are [nodes](https://wiki.openstreetmap.org/wiki/Node), [ways](https://wiki.openstreetmap.org/wiki/Way) and [relations](https://wiki.openstreetmap.org/wiki/Relation).

See also [OpenRailwayMap Tagging Schema](https://wiki.openstreetmap.org/wiki/OpenRailwayMap/Tagging).

* **stations**, **stops** and **halts** are mapped as
  * nodes and ways with tag railway=station|stop|halt and key railway:ref as Station Code 
  * or relations with tag public_transport=stop_area and key railway:ref as Station Code.
* **tracks** are mapped as ways with tag railway=rail and key ref as Railway line number. The connection between two adjacent stations consists of multiple tracks.
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

RInf has data for 1210 railway lines.

OSM data with relation [route=tracks] was found for 1142 railway lines.

### Comparison of operational points

OSM data for stations or stops with tag railway:ref was not found for 227 railway lines.

The location distances of 6591 corresponding RInf operational points and OSM stations from 985 railway lines were compared.

Only 6 operational points with distances more than 0.7 km (max platform length) were found.

|Line|Operational Point|OPID|Distance
|---|---|---|---|
|2615|Köln-Nippes|DE  KKN|[3.158](https://brouter.de/brouter-web/#map=14/50.984453/6.921787/osm-mapnik-german_style&pois=6.921787,50.984453,RInf;6.941777,50.958994,OSM&profile=rail)|
|6135|Hohenleipisch|DE  BHL|[0.925](https://brouter.de/brouter-web/#map=14/51.499293/13.574746/osm-mapnik-german_style&pois=13.574746,51.499293,RInf;13.565695,51.493172,OSM&profile=rail)|
|6207|Lutherstadt Wittenberg-Piesteritz|DE  LWP|[0.837](https://brouter.de/brouter-web/#map=14/51.870960/12.608751/osm-mapnik-german_style&pois=12.608751,51.870960,RInf;12.596647,51.871883,OSM&profile=rail)|
|6325|Subzin-Liessow|DE WSBN|[1.772](https://brouter.de/brouter-web/#map=14/53.876122/12.351091/osm-mapnik-german_style&pois=12.351091,53.876122,RInf;12.347621,53.891930,OSM&profile=rail)|
|6411|Rodleben|DE LROD|[1.001](https://brouter.de/brouter-web/#map=14/51.901843/12.204526/osm-mapnik-german_style&pois=12.204526,51.901843,RInf;12.217290,51.897486,OSM&profile=rail)|
|6899|Kläden (Kr Stendal)|DE  LKD|[1.051](https://brouter.de/brouter-web/#map=14/52.640786/11.648791/osm-mapnik-german_style&pois=11.648791,52.640786,RInf;11.660941,52.634874,OSM&profile=rail)|

A manual check shows that the OSM location data are more accurate.

### Comparison of sections of line

RInf has 967 railway lines with more than 1 operational point.

More than 1 osm operational point was found for 680 railway lines.

Section of line data was found for 557 railway lines.

Maxspeed data was found for 484 railway lines.

Maxspeed data differs in 71 railway lines more than 50 km.
