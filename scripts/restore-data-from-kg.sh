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

if [ $# -eq 0 ]
  then
    echo "country arg expected"
    exit 1
fi

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --Build ${RINF_DATA_DIR}/ $1 $2
