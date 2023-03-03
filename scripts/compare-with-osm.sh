#!/usr/bin/env bash
# compare data from rinf knowledge graph with osm data
 
if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

RINF_DATA_DIR="./erakg-data"

if [ ! -d ${RINF_DATA_DIR} ]; then
    echo "directory ${RINF_DATA_DIR} not found"
    exit 1
fi

dotnet run --project src/OSMComparison/OsmComparison.fsproj -- --Osm ${RINF_DATA_DIR}/ > ${RINF_DATA_DIR}/OsmEntries.json
dotnet run --project src/OSMComparison/OsmComparison.fsproj -- --Osm.Compare ${RINF_DATA_DIR}/
