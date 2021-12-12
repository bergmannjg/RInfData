# Comparison of RInf and OSM Railway data

Comparison of [RInf](https://www.era.europa.eu/registers_en#rinf) data and [OSM Railway](https://wiki.openstreetmap.org/wiki/Railways) data.

## Prerequisites

* [local installation](scripts/docker-overpass.sh) of overpass api
* download RInf data with [RInfLoader](https://github.com/bergmannjg/RInfData/tree/main/src/RInfLoader).

## Data models

Short summary of basic concepts.

### RInf concepts

See also [specifications of the register of Infrastructure](https://www.era.europa.eu/sites/default/files/registers/docs/rinf_application_guide_for_register_en.pdf) and ERA [vocabulary](https://data-interop.era.europa.eu/era-vocabulary/).

* **Operational Point** is a location for train service operations with type station, passenger terminal, junction, switch etc. OPs have a unique OPID, a location and a list of NationalIdentNum as parameters.
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

RInf operational points with type station are compared.

[Open data](https://data.deutschebahn.com/dataset/data-haltestellen.html) from Deutsche Bahn AG is used
to connect OSM tags with [UIC station codes](https://www.wikidata.org/wiki/Property:P722) or [IFOPT](https://en.wikipedia.org/wiki/Identification_of_Fixed_Objects_in_Public_Transport) to opids.

|OSM/RInf ops matched|Count|
|---|---:|
|total|6053|
|by OpId|5844|
|by UicRef|209|

|Reason for mismatch|Remark|Count|
|---|---|---:|
|DistanceToWaysOfLine|Matched but RInf ops distance to line more then 1 km|32|
|DistanceToStop|Matched but RInf ops distance to osm stop more then 1 km|4|
|HistoricStation|Unmatched, historic stations not mapped|4|
|OsmNotYetMapped|Unmatched, not yet mapped|7|
||total|47|

|Line|OPID|Name|Reason for mismatch|OSM Relation of line|
|---|---|---|---|---:|
|1153|[DE ALBG](https://brouter.de/brouter-web/#map=14/53.249756/10.419897/osm-mapnik-german_style&pois=10.419897,53.249756,RInf;10.419897,53.249756,OSM&profile=rail)|Lüneburg|DistanceToWaysOfLine [1.429](https://brouter.de/brouter-web/#map=14/53.249756/10.419897/osm-mapnik-german_style&pois=10.419897,53.249756,RInf;10.418426,53.262573,OSM&profile=rail)|[7239373](https://www.openstreetmap.org/relation/7239373)|
|1711|[DE   HH](https://brouter.de/brouter-web/#map=14/52.377482/9.742211/osm-mapnik-german_style&pois=9.742211,52.377482,RInf;9.742211,52.377482,OSM&profile=rail)|Hannover Hbf|DistanceToWaysOfLine [3.581](https://brouter.de/brouter-web/#map=14/52.377482/9.742211/osm-mapnik-german_style&pois=9.742211,52.377482,RInf;9.700722,52.397384,OSM&profile=rail)|[6116264](https://www.openstreetmap.org/relation/6116264)|
|1962|[DE HNPL](https://brouter.de/brouter-web/#map=14/52.548772/10.607491/osm-mapnik-german_style&pois=10.607491,52.548772,RInf;10.607491,52.548772,OSM&profile=rail)|Neudorf-Platendorf|HistoricStation|[9192176](https://www.openstreetmap.org/relation/9192176)|
|2165|[DE EBDA](https://brouter.de/brouter-web/#map=14/51.426123/7.141557/osm-mapnik-german_style&pois=7.141557,51.426123,RInf;7.141557,51.426123,OSM&profile=rail)|Bochum-Dahlhausen|DistanceToWaysOfLine [1.133](https://brouter.de/brouter-web/#map=14/51.426123/7.141557/osm-mapnik-german_style&pois=7.141557,51.426123,RInf;7.128048,51.431854,OSM&profile=rail)|[7761814](https://www.openstreetmap.org/relation/7761814)|
|2246|[DE  EOS](https://brouter.de/brouter-web/#map=14/51.499740/6.885076/osm-mapnik-german_style&pois=6.885076,51.499740,RInf;6.885076,51.499740,OSM&profile=rail)|Oberhausen-Osterfeld|DistanceToWaysOfLine [2.933](https://brouter.de/brouter-web/#map=14/51.499740/6.885076/osm-mapnik-german_style&pois=6.885076,51.499740,RInf;6.926927,51.503886,OSM&profile=rail)|[7769935](https://www.openstreetmap.org/relation/7769935)|
|2272|[DE  EOB](https://brouter.de/brouter-web/#map=14/51.474618/6.852153/osm-mapnik-german_style&pois=6.852153,51.474618,RInf;6.852153,51.474618,OSM&profile=rail)|Oberhausen Hbf|DistanceToWaysOfLine [1.423](https://brouter.de/brouter-web/#map=14/51.474618/6.852153/osm-mapnik-german_style&pois=6.852153,51.474618,RInf;6.852271,51.487418,OSM&profile=rail)|[2523490](https://www.openstreetmap.org/relation/2523490)|
|2320|[DE EDHD](https://brouter.de/brouter-web/#map=14/51.408767/6.754522/osm-mapnik-german_style&pois=6.754522,51.408767,RInf;6.754522,51.408767,OSM&profile=rail)|Duisburg-Hochfeld Süd|DistanceToWaysOfLine [1.522](https://brouter.de/brouter-web/#map=14/51.408767/6.754522/osm-mapnik-german_style&pois=6.754522,51.408767,RInf;6.774604,51.414280,OSM&profile=rail)|[2751678](https://www.openstreetmap.org/relation/2751678)|
|2326|[DE EDHD](https://brouter.de/brouter-web/#map=14/51.408767/6.754522/osm-mapnik-german_style&pois=6.754522,51.408767,RInf;6.754522,51.408767,OSM&profile=rail)|Duisburg-Hochfeld Süd|DistanceToWaysOfLine [1.522](https://brouter.de/brouter-web/#map=14/51.408767/6.754522/osm-mapnik-german_style&pois=6.754522,51.408767,RInf;6.774604,51.414280,OSM&profile=rail)|[2537836](https://www.openstreetmap.org/relation/2537836)|
|2416|[DE  KDD](https://brouter.de/brouter-web/#map=14/51.246860/6.794351/osm-mapnik-german_style&pois=6.794351,51.246860,RInf;6.794351,51.246860,OSM&profile=rail)|Düsseldorf-Derendorf|DistanceToWaysOfLine [1.153](https://brouter.de/brouter-web/#map=14/51.246860/6.794351/osm-mapnik-german_style&pois=6.794351,51.246860,RInf;6.791524,51.257079,OSM&profile=rail)|[6814603](https://www.openstreetmap.org/relation/6814603)|
|2624|[DE  KKM](https://brouter.de/brouter-web/#map=14/50.957494/7.013366/osm-mapnik-german_style&pois=7.013366,50.957494,RInf;7.013366,50.957494,OSM&profile=rail)|Köln-Mülheim|DistanceToWaysOfLine [1.150](https://brouter.de/brouter-web/#map=14/50.957494/7.013366/osm-mapnik-german_style&pois=7.013366,50.957494,RInf;7.020596,50.966780,OSM&profile=rail)|[6657020](https://www.openstreetmap.org/relation/6657020)|
|2690|[DE  FSP](https://brouter.de/brouter-web/#map=14/50.068021/8.633453/osm-mapnik-german_style&pois=8.633453,50.068021,RInf;8.633453,50.068021,OSM&profile=rail)|Frankfurt am Main Stadion|DistanceToWaysOfLine [1.029](https://brouter.de/brouter-web/#map=14/50.068021/8.633453/osm-mapnik-german_style&pois=8.633453,50.068021,RInf;8.622725,50.061834,OSM&profile=rail)|[569834](https://www.openstreetmap.org/relation/569834)|
|2901|[DE EPRN](https://brouter.de/brouter-web/#map=14/51.588098/7.539701/osm-mapnik-german_style&pois=7.539701,51.588098,RInf;7.539701,51.588098,OSM&profile=rail)|Lünen Preußen|DistanceToWaysOfLine [1.127](https://brouter.de/brouter-web/#map=14/51.588098/7.539701/osm-mapnik-german_style&pois=7.539701,51.588098,RInf;7.536672,51.598053,OSM&profile=rail)|[2530289](https://www.openstreetmap.org/relation/2530289)|
|3233|[DE  SSH](https://brouter.de/brouter-web/#map=14/49.241772/6.991136/osm-mapnik-german_style&pois=6.991136,49.241772,RInf;6.991136,49.241772,OSM&profile=rail)|Saarbrücken Hbf|DistanceToWaysOfLine [1.054](https://brouter.de/brouter-web/#map=14/49.241772/6.991136/osm-mapnik-german_style&pois=6.991136,49.241772,RInf;6.976671,49.242637,OSM&profile=rail)|[7711601](https://www.openstreetmap.org/relation/7711601)|
|3443|[DE RKWU](https://brouter.de/brouter-web/#map=14/49.002300/8.362700/osm-mapnik-german_style&pois=8.362700,49.002300,RInf;8.362700,49.002300,OSM&profile=rail)|Karlsruhe West Hp|OsmNotYetMapped|[4240679](https://www.openstreetmap.org/relation/4240679)|
|3604|[DE  FFS](https://brouter.de/brouter-web/#map=14/50.099111/8.686375/osm-mapnik-german_style&pois=8.686375,50.099111,RInf;8.686375,50.099111,OSM&profile=rail)|Frankfurt (Main) Süd|DistanceToWaysOfLine [1.354](https://brouter.de/brouter-web/#map=14/50.099111/8.686375/osm-mapnik-german_style&pois=8.686375,50.099111,RInf;8.669379,50.093696,OSM&profile=rail)|[7852697](https://www.openstreetmap.org/relation/7852697)|
|3704|[DE   FG](https://brouter.de/brouter-web/#map=14/50.579086/8.663095/osm-mapnik-german_style&pois=8.663095,50.579086,RInf;8.663095,50.579086,OSM&profile=rail)|Gießen|DistanceToWaysOfLine [1.275](https://brouter.de/brouter-web/#map=14/50.579086/8.663095/osm-mapnik-german_style&pois=8.663095,50.579086,RInf;8.652739,50.569693,OSM&profile=rail)|[7857211](https://www.openstreetmap.org/relation/7857211)|
|3828|[DE  FFU](https://brouter.de/brouter-web/#map=14/50.554819/9.684187/osm-mapnik-german_style&pois=9.684187,50.554819,RInf;9.684187,50.554819,OSM&profile=rail)|Fulda|DistanceToWaysOfLine [1.005](https://brouter.de/brouter-web/#map=14/50.554819/9.684187/osm-mapnik-german_style&pois=9.684187,50.554819,RInf;9.690098,50.546594,OSM&profile=rail)|[7857206](https://www.openstreetmap.org/relation/7857206)|
|4115|[DE  REP](https://brouter.de/brouter-web/#map=14/49.135972/8.914661/osm-mapnik-german_style&pois=8.914661,49.135972,RInf;8.914661,49.135972,OSM&profile=rail)|Eppingen|DistanceToWaysOfLine [2.424](https://brouter.de/brouter-web/#map=14/49.135972/8.914661/osm-mapnik-german_style&pois=8.914661,49.135972,RInf;8.938763,49.151024,OSM&profile=rail)|[5260323](https://www.openstreetmap.org/relation/5260323)|
|4262|[DE  RAP](https://brouter.de/brouter-web/#map=14/48.541839/7.973461/osm-mapnik-german_style&pois=7.973461,48.541839,RInf;7.973461,48.541839,OSM&profile=rail)|Appenweier|DistanceToWaysOfLine [1.064](https://brouter.de/brouter-web/#map=14/48.541839/7.973461/osm-mapnik-german_style&pois=7.973461,48.541839,RInf;7.976428,48.551206,OSM&profile=rail)|[12897405](https://www.openstreetmap.org/relation/12897405)|
|4280|[DE  RHL](https://brouter.de/brouter-web/#map=14/47.612856/7.611818/osm-mapnik-german_style&pois=7.611818,47.612856,RInf;7.611818,47.612856,OSM&profile=rail)|Haltingen|DistanceToWaysOfLine [1.186](https://brouter.de/brouter-web/#map=14/47.612856/7.611818/osm-mapnik-german_style&pois=7.611818,47.612856,RInf;7.602580,47.621515,OSM&profile=rail)|[7687702](https://www.openstreetmap.org/relation/7687702)|
|4720|[DE  TSZ](https://brouter.de/brouter-web/#map=14/48.829830/9.167089/osm-mapnik-german_style&pois=9.167089,48.829830,RInf;9.167089,48.829830,OSM&profile=rail)|Stuttgart-Zuffenhausen|DistanceToWaysOfLine [2.002](https://brouter.de/brouter-web/#map=14/48.829830/9.167089/osm-mapnik-german_style&pois=9.167089,48.829830,RInf;9.179974,48.845716,OSM&profile=rail)|[4240693](https://www.openstreetmap.org/relation/4240693)|
|5100|[DE NHAL](https://brouter.de/brouter-web/#map=14/49.929159/10.885519/osm-mapnik-german_style&pois=10.885519,49.929159,RInf;10.885519,49.929159,OSM&profile=rail)|Hallstadt (b Bamberg)|OsmNotYetMapped|[7910147](https://www.openstreetmap.org/relation/7910147)|
|5707|[DE  MRO](https://brouter.de/brouter-web/#map=14/47.850141/12.119376/osm-mapnik-german_style&pois=12.119376,47.850141,RInf;12.119376,47.850141,OSM&profile=rail)|Rosenheim|DistanceToWaysOfLine [1.266](https://brouter.de/brouter-web/#map=14/47.850141/12.119376/osm-mapnik-german_style&pois=12.119376,47.850141,RInf;12.134810,47.845419,OSM&profile=rail)|[7879781](https://www.openstreetmap.org/relation/7879781)|
|5832|[DE  MNR](https://brouter.de/brouter-web/#map=14/48.361387/12.502987/osm-mapnik-german_style&pois=12.502987,48.361387,RInf;12.502987,48.361387,OSM&profile=rail)|Neumarkt-St.Veit|DistanceToWaysOfLine [2.227](https://brouter.de/brouter-web/#map=14/48.361387/12.502987/osm-mapnik-german_style&pois=12.502987,48.361387,RInf;12.525810,48.374478,OSM&profile=rail)|[7728984](https://www.openstreetmap.org/relation/7728984)|
|5916|[DE  NER](https://brouter.de/brouter-web/#map=14/49.595973/11.001712/osm-mapnik-german_style&pois=11.001712,49.595973,RInf;11.001712,49.595973,OSM&profile=rail)|Erlangen|DistanceToWaysOfLine [2.840](https://brouter.de/brouter-web/#map=14/49.595973/11.001712/osm-mapnik-german_style&pois=11.001712,49.595973,RInf;10.997163,49.570605,OSM&profile=rail)|[9169813](https://www.openstreetmap.org/relation/9169813)|
|6078|[DE BHPN](https://brouter.de/brouter-web/#map=14/52.518748/13.681216/osm-mapnik-german_style&pois=13.681216,52.518748,RInf;13.681216,52.518748,OSM&profile=rail)|Hoppegarten (Mark)|OsmNotYetMapped|[7088075](https://www.openstreetmap.org/relation/7088075)|
|6087|[DE BPOT](https://brouter.de/brouter-web/#map=14/52.517056/12.971464/osm-mapnik-german_style&pois=12.971464,52.517056,RInf;12.971464,52.517056,OSM&profile=rail)|Priort|DistanceToWaysOfLine [2.753](https://brouter.de/brouter-web/#map=14/52.517056/12.971464/osm-mapnik-german_style&pois=12.971464,52.517056,RInf;12.974879,52.541726,OSM&profile=rail)|[6896789](https://www.openstreetmap.org/relation/6896789)|
|6104|[DE BPOT](https://brouter.de/brouter-web/#map=14/52.517056/12.971464/osm-mapnik-german_style&pois=12.971464,52.517056,RInf;12.971464,52.517056,OSM&profile=rail)|Priort|DistanceToWaysOfLine [2.529](https://brouter.de/brouter-web/#map=14/52.517056/12.971464/osm-mapnik-german_style&pois=12.971464,52.517056,RInf;12.974709,52.539718,OSM&profile=rail)|[7252590](https://www.openstreetmap.org/relation/7252590)|
|6107|[DE   BL](https://brouter.de/brouter-web/#map=14/52.525998/13.369366/osm-mapnik-german_style&pois=13.369366,52.525998,RInf;13.369366,52.525998,OSM&profile=rail)|Berlin Hauptbahnhof - Lehrter Bahnhof|DistanceToWaysOfLine [1.109](https://brouter.de/brouter-web/#map=14/52.525998/13.369366/osm-mapnik-german_style&pois=13.369366,52.525998,RInf;13.360478,52.534376,OSM&profile=rail)|[7099121](https://www.openstreetmap.org/relation/7099121)|
|6107|[DE BSPD](https://brouter.de/brouter-web/#map=14/52.534378/13.198396/osm-mapnik-german_style&pois=13.198396,52.534378,RInf;13.198396,52.534378,OSM&profile=rail)|Berlin-Spandau|DistanceToWaysOfLine [2.319](https://brouter.de/brouter-web/#map=14/52.534378/13.198396/osm-mapnik-german_style&pois=13.198396,52.534378,RInf;13.231526,52.529025,OSM&profile=rail)|[7099121](https://www.openstreetmap.org/relation/7099121)|
|6135|[DE  BHL](https://brouter.de/brouter-web/#map=14/51.499293/13.574746/osm-mapnik-german_style&pois=13.574746,51.499293,RInf;13.574746,51.499293,OSM&profile=rail)|Hohenleipisch|DistanceToStop [0.925](https://brouter.de/brouter-web/#map=14/51.499293/13.574746/osm-mapnik-german_style&pois=13.574746,51.499293,RInf;13.565695,51.493172,OSM&profile=rail)|[5372709](https://www.openstreetmap.org/relation/5372709)|
|6140|[DE  BLO](https://brouter.de/brouter-web/#map=14/52.509706/13.496505/osm-mapnik-german_style&pois=13.496505,52.509706,RInf;13.496505,52.509706,OSM&profile=rail)|Berlin-Lichtenberg|DistanceToWaysOfLine [1.103](https://brouter.de/brouter-web/#map=14/52.509706/13.496505/osm-mapnik-german_style&pois=13.496505,52.509706,RInf;13.482216,52.504928,OSM&profile=rail)|[7290631](https://www.openstreetmap.org/relation/7290631)|
|6191|[DE BDKO](https://brouter.de/brouter-web/#map=14/51.620823/13.564243/osm-mapnik-german_style&pois=13.564243,51.620823,RInf;13.564243,51.620823,OSM&profile=rail)|Doberlug-Kirchhain ob Bf|DistanceToWaysOfLine [2.018](https://brouter.de/brouter-web/#map=14/51.620823/13.564243/osm-mapnik-german_style&pois=13.564243,51.620823,RInf;13.585877,51.633036,OSM&profile=rail)|[7665695](https://www.openstreetmap.org/relation/7665695)|
|6194|[DE DWID](https://brouter.de/brouter-web/#map=14/51.392844/14.039311/osm-mapnik-german_style&pois=14.039311,51.392844,RInf;14.039311,51.392844,OSM&profile=rail)|Wiednitz|HistoricStation|[5379159](https://www.openstreetmap.org/relation/5379159)|
|6207|[DE  LWP](https://brouter.de/brouter-web/#map=14/51.870960/12.608751/osm-mapnik-german_style&pois=12.608751,51.870960,RInf;12.608751,51.870960,OSM&profile=rail)|Lutherstadt Wittenberg-Piesteritz|DistanceToStop [0.837](https://brouter.de/brouter-web/#map=14/51.870960/12.608751/osm-mapnik-german_style&pois=12.608751,51.870960,RInf;12.596647,51.871883,OSM&profile=rail)|[5379099](https://www.openstreetmap.org/relation/5379099)|
|6220|[DE BMZD](https://brouter.de/brouter-web/#map=14/51.768829/14.366591/osm-mapnik-german_style&pois=14.366591,51.768829,RInf;14.366591,51.768829,OSM&profile=rail)|Cottbus-Merzdorf                Süd-Nord|OsmNotYetMapped|[7357037](https://www.openstreetmap.org/relation/7357037)|
|6325|[DE WSBN](https://brouter.de/brouter-web/#map=14/53.876122/12.351091/osm-mapnik-german_style&pois=12.351091,53.876122,RInf;12.351091,53.876122,OSM&profile=rail)|Subzin-Liessow|DistanceToStop [1.772](https://brouter.de/brouter-web/#map=14/53.876122/12.351091/osm-mapnik-german_style&pois=12.351091,53.876122,RInf;12.347621,53.891930,OSM&profile=rail)|[7663369](https://www.openstreetmap.org/relation/7663369)|
|6345|[DELEGOB](https://brouter.de/brouter-web/#map=14/51.463375/12.667834/osm-mapnik-german_style&pois=12.667834,51.463375,RInf;12.667834,51.463375,OSM&profile=rail)|Eilenburg Ost Bk Hp|OsmNotYetMapped|[5378214](https://www.openstreetmap.org/relation/5378214)|
|6365|[DE   LE](https://brouter.de/brouter-web/#map=14/51.344029/12.469551/osm-mapnik-german_style&pois=12.469551,51.344029,RInf;12.469551,51.344029,OSM&profile=rail)|Leipzig-Engelsdorf|DistanceToWaysOfLine [1.008](https://brouter.de/brouter-web/#map=14/51.344029/12.469551/osm-mapnik-german_style&pois=12.469551,51.344029,RInf;12.484058,51.343822,OSM&profile=rail)|[7687714](https://www.openstreetmap.org/relation/7687714)|
|6375|[DE   LE](https://brouter.de/brouter-web/#map=14/51.344029/12.469551/osm-mapnik-german_style&pois=12.469551,51.344029,RInf;12.469551,51.344029,OSM&profile=rail)|Leipzig-Engelsdorf|DistanceToWaysOfLine [2.009](https://brouter.de/brouter-web/#map=14/51.344029/12.469551/osm-mapnik-german_style&pois=12.469551,51.344029,RInf;12.440635,51.343718,OSM&profile=rail)|[7672185](https://www.openstreetmap.org/relation/7672185)|
|6378|[DE  LGW](https://brouter.de/brouter-web/#map=14/51.250119/12.379187/osm-mapnik-german_style&pois=12.379187,51.250119,RInf;12.379187,51.250119,OSM&profile=rail)|Markkleeberg-Gaschwitz|DistanceToWaysOfLine [1.147](https://brouter.de/brouter-web/#map=14/51.250119/12.379187/osm-mapnik-german_style&pois=12.379187,51.250119,RInf;12.381199,51.239883,OSM&profile=rail)|[7687708](https://www.openstreetmap.org/relation/7687708)|
|6385|[DE DWIO](https://brouter.de/brouter-web/#map=14/50.883002/12.841352/osm-mapnik-german_style&pois=12.841352,50.883002,RInf;12.841352,50.883002,OSM&profile=rail)|Wittgensdorf ob Bf|HistoricStation|[1970766](https://www.openstreetmap.org/relation/1970766)|
|6386|[DE  DCW](https://brouter.de/brouter-web/#map=14/51.122927/13.579585/osm-mapnik-german_style&pois=13.579585,51.122927,RInf;13.579585,51.122927,OSM&profile=rail)|Coswig (bei Dresden)|DistanceToWaysOfLine [1.507](https://brouter.de/brouter-web/#map=14/51.122927/13.579585/osm-mapnik-german_style&pois=13.579585,51.122927,RInf;13.570301,51.135161,OSM&profile=rail)|[5363032](https://www.openstreetmap.org/relation/5363032)|
|6406|[DE  LMR](https://brouter.de/brouter-web/#map=14/52.171271/11.654893/osm-mapnik-german_style&pois=11.654893,52.171271,RInf;11.654893,52.171271,OSM&profile=rail)|Magdeburg-Rothensee|OsmNotYetMapped|[7704835](https://www.openstreetmap.org/relation/7704835)|
|6411|[DE LROD](https://brouter.de/brouter-web/#map=14/51.901843/12.204526/osm-mapnik-german_style&pois=12.204526,51.901843,RInf;12.204526,51.901843,OSM&profile=rail)|Rodleben|DistanceToStop [1.001](https://brouter.de/brouter-web/#map=14/51.901843/12.204526/osm-mapnik-german_style&pois=12.204526,51.901843,RInf;12.217290,51.897486,OSM&profile=rail)|[5555034](https://www.openstreetmap.org/relation/5555034)|
|6441|[DE WLOW](https://brouter.de/brouter-web/#map=14/53.411029/11.464242/osm-mapnik-german_style&pois=11.464242,53.411029,RInf;11.464242,53.411029,OSM&profile=rail)|Lüblow (Meckl)|HistoricStation|[7663371](https://www.openstreetmap.org/relation/7663371)|
|6441|[DE WSGR](https://brouter.de/brouter-web/#map=14/53.598071/11.384648/osm-mapnik-german_style&pois=11.384648,53.598071,RInf;11.384648,53.598071,OSM&profile=rail)|Schwerin-Görries|OsmNotYetMapped|[7663371](https://www.openstreetmap.org/relation/7663371)|

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
