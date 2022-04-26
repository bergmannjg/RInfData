# RInfQuery

Query cached [RINF API](https://rinf.era.europa.eu/API/Help) data.

## Build

* run: `restore-data.sh`
* run: `dotnet build RInfQuery.fsproj`

## Usage

```txt
USAGE: RInfQuery
               [--help] [--OperationalPoints.Line <dataDir> <country> <line>]  [--SectionsOfLine.Line <dataDir> <imcode> <line>]
               [--Compare.Line <line>] [--Compare.Line.Remote <line>]  [--Compare.Lines <maxlines>]
               [--Graph.Route <dataDir> <ops>] [--Graph.Line <dataDir> <line>]

OPTIONS:

    --OperationalPoints.Line <dataDir> <country> <line>
                          get OperationalPoints of line from file OperationalPoints.json in <dataDir>.
    --SectionsOfLine.Line <dataDir> <imcode> <line>
                          get SectionsOfLine of line from file SectionsOfLine.json in <dataDir>.
    --Graph.Route <dataDir> <opIds>
                          get path of route from <opIds>, ex. "DE   HH;DE   BL"
                          (assumes Graph.json and OpInfos.json in <dataDir>).
    --Graph.Line <dataDir> <line>
                          get path of line 
                          (assumes Graph.json, LineInfos.json and OpInfos.json in <dataDir>).
    --Compare.Line <line> compare local RInf and local OSM data of line.
    --Compare.Line.Remote <line>  
                          compare local RInf and remote OSM data of line.
    --Compare.Lines <maxlines>    
                          compare local RInf and local OSM data of max lines.
    --help                display this list of options.
```

## Example

Compute the route from Hannover to Berlin:

```txt
RInfQuery --Graph.Route ../../rinf-data/ "DE000HH;DE000BL"
```

Output as table (with links to the [ERA Knowledge Graph](https://linked.ec-dataplatform.eu/sparql?default-graph-uri=https%3A%2F%2Flinked.ec-dataplatform.eu\%2Fera)):

|From|To|Line|Distance|
| --- | --- | ---| ---: |
|[DE000HH](https://linked.ec-dataplatform.eu/describe/?url=http://data.europa.eu/949/functionalInfrastructure/operationalPoints/DE000HH)|DE95411|1730|17.1|
|DE95411|DE00LOE|6107|71.2|
|DE00LOE|DE0BSPD|6185|155.3|
|DE0BSPD|[DE000BL](https://linked.ec-dataplatform.eu/describe/?url=http://data.europa.eu/949/functionalInfrastructure/operationalPoints/DE000BL)|6107|9.0|

 Output of [operational points of route](https://brouter.de/brouter-web/#map=9/52.439487/10.984844/osm-mapnik-german_style&lonlats=9.742211,52.377482;9.808958,52.374380;9.974214,52.376994;9.982670,52.373167;10.083924,52.391323;10.181740,52.417191;10.238710,52.432217;10.322279,52.449419;10.428068,52.456469;10.545209,52.456210;10.624839,52.427532;10.710352,52.423153;10.717842,52.423548;10.787725,52.429355;10.852638,52.430929;10.971329,52.436259;10.984844,52.439487;11.704606,52.574701;11.948342,52.597452;12.040855,52.591211;12.354864,52.599770;12.432024,52.601998;12.502795,52.599933;12.629225,52.592225;12.677476,52.590744;12.939727,52.551904;13.148819,52.537358;13.198396,52.534378;13.255475,52.526888;13.299406,52.530313;13.339423,52.535255;13.341424,52.535724;13.369366,52.525998&profile=rail) visualized with [BRouter-Web](https://brouter.de/).
