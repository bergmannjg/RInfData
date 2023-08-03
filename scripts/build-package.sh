#!/usr/bin/env bash
# build RInfGraph js package

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

# DATADIR=../../../rinf-data
DATADIR=../../../erakg-data

pushd ./src/RInfGraph/target.javascript

if [ ! -d "${DATADIR}" ]; then
    echo "${DATADIR} not found"
    exit 1
fi

if [ ! -d "./rinf-graph" ]; then
    echo "rinf-data not found"
    exit 1
fi

if [ ! -f "./rinf-graph/rinfgraph.bundle.d.ts" ]; then
    echo "rinfgraph.bundle.d.ts not found"
    exit 1
fi

if [ ! -f "./package.json" ]; then
    echo "package.json not found"
    exit 1
fi

npm install

dotnet fable RInfGraph.fable.fsproj -o build --noCache --run webpack --mode production --no-devtool --config ./webpack.config.js

cp ${DATADIR}/Graph.json ${DATADIR}/LineInfos.json ${DATADIR}/TunnelInfos.json ${DATADIR}/OpInfos.json rinf-graph/data/

npm pack rinf-graph/

popd
