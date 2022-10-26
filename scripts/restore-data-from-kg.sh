#!/usr/bin/env bash
# restore data from knowledge graph
 
if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

RINF_DATA_DIR="./erakg-data"

if [ ! -d ${RINF_DATA_DIR} ]; then
    echo "directory ${RINF_DATA_DIR} not found"
    exit 1
fi

if [ $# -ne 1 ]
  then
    echo "countriy arg expected"
    exit 1
fi

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --Tracks ${RINF_DATA_DIR}/ $1
dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --RailwayLine ${RINF_DATA_DIR}/ $1 > ${RINF_DATA_DIR}/Railwaylines.json
dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --SectionsOfLine ${RINF_DATA_DIR}/ $1 > ${RINF_DATA_DIR}/SectionsOfLines.json
dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --OperationalPoints ${RINF_DATA_DIR}/ $1 > ${RINF_DATA_DIR}/OperationalPoints.json

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --OpInfo.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/OpInfos.json
dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --Graph.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/Graph.json
dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --  --LineInfo.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/LineInfos.json
