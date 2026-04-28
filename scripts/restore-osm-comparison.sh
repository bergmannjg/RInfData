#!/usr/bin/env bash
# build RInf Osm comparison file
# access to QLever endpoint https://qlever.dev/osm-planet in GitHub actions is not possible

set -e

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

RINF_DATA_DIR="./erakg-data"

if [ -d "$1" ]; then
    echo "use directory  $1"
    RINF_DATA_DIR=$1
fi

if [ ! -d ${RINF_DATA_DIR} ]; then
    echo "directory  ${RINF_DATA_DIR} not found"
    exit 1
fi

dotnet run --project src/OSMComparison/OsmComparison.fsproj --Osm ${RINF_DATA_DIR}/
dotnet run --project src/OSMComparison/OsmComparison.fsproj --Osm.Compare ${RINF_DATA_DIR}/ > src/RInfGraphWeb/lib/RInfOsmMatchings.json
