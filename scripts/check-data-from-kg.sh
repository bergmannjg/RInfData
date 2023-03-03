#!/usr/bin/env bash
# check new data from knowledge graph
 
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

if [ ! -f ${RINF_DATA_DIR}/sparql-operationalPoints.json ]; then
    echo "file sparql-operationalPoints.json not found"
    exit 1
fi

if [ ! -f ${RINF_DATA_DIR}/sparql-operationalPoints.json ]; then
    echo "file sparql-sectionsOfLine.json not found"
    exit 1
fi

rm -f ${RINF_DATA_DIR}/sparql-operationalPoints-new.json
rm -f ${RINF_DATA_DIR}/sparql-sectionsOfLine-new.json

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --OperationalPoints ${RINF_DATA_DIR}/ $1 sparql-operationalPoints-new.json > /dev/null

diff -e ${RINF_DATA_DIR}/sparql-operationalPoints.json ${RINF_DATA_DIR}/sparql-operationalPoints-new.json 2> /dev/null

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --SectionsOfLine ${RINF_DATA_DIR}/ $1 sparql-sectionsOfLine-new.json > /dev/null

diff -e ${RINF_DATA_DIR}/sparql-sectionsOfLine.json ${RINF_DATA_DIR}/sparql-sectionsOfLine-new.json 2> /dev/null
