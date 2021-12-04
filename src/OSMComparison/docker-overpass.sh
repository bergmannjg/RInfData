#!/usr/bin/env bash
# https://github.com/wiktorn/Overpass-API

docker run \
  -e OVERPASS_META=yes \
  -e OVERPASS_MODE=init \
  -e OVERPASS_PLANET_URL=http://download.geofabrik.de/europe/germany-latest.osm.bz2 \
  -e OVERPASS_DIFF_URL=http://download.openstreetmap.fr/replication/europe/germany/minute/ \
  -e OVERPASS_RULES_LOAD=10 \
  -v /big/docker/overpass_db/:/db \
  -p 12345:80 \
  -i -t \
  --name overpass_germany wiktorn/overpass-api