#!/usr/bin/env bash
# restore data from rinf api
 
if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

if [ ! -d "./rinf-data" ]; then
    echo "directory rinf-data not found"
    exit 1
fi

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --SectionsOfLines > ./rinf-data/SectionsOfLines.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --OperationalPoints > ./rinf-data/OperationalPoints.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --SOLTrackParameters ./rinf-data/SectionsOfLines.json > ./rinf-data/SOLTrackParameters.json

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --OpInfo.Build ./rinf-data/ > ./rinf-data/OpInfos.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --Graph.Build ./rinf-data/ > ./rinf-data/Graph.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --LineInfo.Build ./rinf-data/ > ./rinf-data/LineInfos.json

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --Graph.Build ./rinf-data/ --noExtraEdges > ./rinf-data/Graph-orig.json
