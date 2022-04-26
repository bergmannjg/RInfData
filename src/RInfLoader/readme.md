# RInfLoader

Load data from [RINF API](https://rinf.era.europa.eu/API/Help).

## Build

* run: `dotnet build RInfLoader.fsproj`

## Usage

```txt
USAGE: RInfLoader
               [--help] [--DatasetImports] [--SectionsOfLines] [--OperationalPoints]
               [--SOLTrackParameters <sol file>] [--OpInfo.Build <dataDir>]
               [--LineInfo.Build <dataDir>] [--Graph.Build <dataDir>] 

OPTIONS:

    --DatasetImports      load DatasetImports (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SectionsOfLines     <countries>
                          load SectionsOfLines of <countries> (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --OperationalPoints   <countries>
                          load OperationalPoints of <countries> (assumes env vars RINF_USERNAME and RINF_PASSWORD).
    --SOLTrackParameters <SectionsOfLines file>
                          load SOLTrackParameters for all SectionsOfLines 
                          (assumes env vars RINF_USERNAME and RINF_PASSWORD). 
    --OpInfo.Build <dataDir>
                          build OpInfos from file OperationalPoints.json in <dataDir>.
    --LineInfo.Build <dataDir>
                          build LineInfos from files SectionsOfLines.json and OperationalPoints.json in <dataDir>.
    --Graph.Build <dataDir>
                          build graph of OperationalPoints and SectionsOfLines from files SectionsOfLines.json,
                          OperationalPoints.json and SOLTrackParameters.json in <dataDir>.

Environment variables:
    RINF_USERNAME         RInf account username.
    RINF_PASSWORD         RInf account password.
```
