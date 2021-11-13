#!/usr/bin/env bash
# load station data from local osm database

./osm-query.sh < queries/node-station.txt > osm-data/node-stations.json 
./osm-query.sh < queries/way-station.txt > osm-data/way-stations.json 
./osm-query.sh < queries/relation-station.txt > osm-data/relation-stations.json
