#!/usr/bin/env bash
# load station data from local osm database

wget -O ../../osm-data/node-stations.json --post-file=queries/node-station.txt "https://overpass-api.de/api/interpreter"
wget -O ../../osm-data/way-stations.json --post-file=queries/way-station.txt "https://overpass-api.de/api/interpreter"
wget -O ../../osm-data/relation-stations.json --post-file=queries/relation-station-ch.txt "https://overpass-api.de/api/interpreter"
wget -O ../../osm-data/node-stations-ch.json --post-file=queries/node-station-ch.txt "https://overpass-api.de/api/interpreter"
