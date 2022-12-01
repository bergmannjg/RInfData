# EraKGLoader

Load data from [ERA knowledge graph](https://era-web.linkeddata.es/sparql.html).

## Build

* run: `dotnet build EraKGLoader.fsproj`

## Usage

```txt
USAGE: EraKGLoader
               [--help] [--SectionsOfLines] [--OperationalPoints]
               [--Tracks] [--OpInfo.Build <dataDir>]
               [--LineInfo.Build <dataDir>] [--Graph.Build <dataDir>]

OPTIONS:

    --SectionsOfLines     <country>
                          load SectionsOfLines.
    --OperationalPoints   <country>
                          load OperationalPoints.
    --Tracks              <country>
                          load tracks for all SectionsOfLines
    --OpInfo.Build <dataDir>
                          build OpInfos from file OperationalPoints.json in <dataDir>.
    --LineInfo.Build <dataDir>
                          build LineInfos from files SectionsOfLines.json and OperationalPoints.json in <dataDir>.
    --Graph.Build <dataDir>
                          build graph of OperationalPoints and SectionsOfLines from files SectionsOfLines.json,
                          OperationalPoints.json and SOLTrackParameters.json in <dataDir>.
    --help                display this list of options.
```
