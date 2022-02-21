#!/usr/bin/env bash
# build RInfGraph js package

DATADIR=./rinf-data
PROJDIR=./src/RInfGraph

if [ ! -d "${PROJDIR}/rinf-data" ]; then
    echo "rinf-data not found"
    exit 1
fi

if [ ! -d "${DATADIR}" ]; then
    echo "${DATADIR} not found"
    exit 1
fi

if [ ! -f "${PROJDIR}/rinf-data/rinfgraph.bundle.d.ts" ]; then
    echo "rinfgraph.bundle.d.ts not found"
    exit 1
fi

if [ ! -f "${PROJDIR}/package.json" ]; then
    echo "package.json not found"
    exit 1
fi

pushd ${PROJDIR}

dotnet fable RInfGraph.fable.fsproj -o build --run webpack --mode production --no-devtool --config ./webpack.config.js

cp ${DATADIR}/Graph.json ${DATADIR}/LineInfos.json ${DATADIR}/OpInfos.json rinf-data/data/

npm pack rinf-data/

popd