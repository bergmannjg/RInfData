#!/usr/bin/env bash
# query to local overpass api

if [ $# -eq 0 ]; then
    echo "usage: ./overpass-query.sh query"
    exit 1
fi

wget -O - -q "http://localhost:12345/api/interpreter?data=[out:json];$1;out;"