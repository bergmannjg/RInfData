#!/usr/bin/env bash
# load station data from local osm database

OSMDIR="../../osm-data"

if [ ! -d ${OSMDIR} ]; then
    echo "directory ${OSMDIR} not found"
    exit 1
fi

if [ ! -d "queries" ]; then
    echo "directory queries not found"
    exit 1
fi

LOCAL_SERVER="http://localhost:12345/api/interpreter"
REMOTE_SERVER="https://overpass-api.de/api/interpreter"

wget -O ${OSMDIR}/relations.json --post-file=queries/route-tracks.txt $LOCAL_SERVER
wget -O ${OSMDIR}/node-stations.json --post-file=queries/node-station.txt $LOCAL_SERVER
wget -O ${OSMDIR}/way-stations.json --post-file=queries/way-station.txt $LOCAL_SERVER
wget -O ${OSMDIR}/relation-stations.json --post-file=queries/relation-station.txt $LOCAL_SERVER
wget -O ${OSMDIR}/node-stations-ch.json --post-file=queries/node-station-ch.txt $REMOTE_SERVER
