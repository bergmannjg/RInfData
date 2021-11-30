#!/usr/bin/env bash

if [[ -z "${OSMS3S_EXEDIR}" ]]; then
  echo "OSMS3S_EXEDIR is unset"
  exit 1
fi

if [[ -z "${OSMS3S_DBDIR}" ]]; then
  echo "OSMS3S_DBDIR is unset"
  exit 1
fi

cd ~/OSM3S/

if [[ ! -f "germany-latest.osm.bz2" ]]; then
  wget https://download.geofabrik.de/europe/germany-latest.osm.bz2
fi

nohup ${OSMS3S_EXEDIR}/bin/init_osm3s.sh germany-latest.osm.bz2 $OSMS3S_DBDIR $OSMS3S_EXEDIR &
