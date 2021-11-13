#!/usr/bin/env bash

if [[ -z "${OSMS3S_EXEDIR}" ]]; then
  echo "OSMS3S_EXEDIR is unset"
  exit 1
fi

if [[ -z "${OSMS3S_DBDIR}" ]]; then
  echo "OSMS3S_DBDIR is unset"
  exit 1
fi

${OSMS3S_EXEDIR}/bin/osm3s_query --db-dir=${OSMS3S_DBDIR}
