#!/usr/bin/env bash
# restore data from rinf api
 
if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

RINF_DATA_DIR="./rinf-data"

if [ ! -d ${RINF_DATA_DIR} ]; then
    echo "directory ${RINF_DATA_DIR} not found"
    exit 1
fi

if [ $# -ne 1 ]
  then
    echo "countries arg expected"
    exit 1
fi

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --SectionsOfLines $1 > ${RINF_DATA_DIR}/SectionsOfLines.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --OperationalPoints $1 > ${RINF_DATA_DIR}/OperationalPoints.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --SOLTrackParameters ${RINF_DATA_DIR}/SectionsOfLines.json > ${RINF_DATA_DIR}/SOLTrackParameters.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --SOLTunnels ${RINF_DATA_DIR}/SectionsOfLines.json > ${RINF_DATA_DIR}/SOLTunnels.json

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --OpInfo.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/OpInfos.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --Graph.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/Graph.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --TunnelInfo.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/TunnelInfos.json
dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --LineInfo.Build ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/LineInfos.json

dotnet run --project src/RInfLoader/RInfLoader.fsproj --  --Graph.Build ${RINF_DATA_DIR}/ --noExtraEdges > ${RINF_DATA_DIR}/Graph-orig.json
