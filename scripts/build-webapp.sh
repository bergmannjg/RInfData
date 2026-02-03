#!/usr/bin/env bash
# build RInfGraph web app

set -e

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

RINF_DATA_DIR="./erakg-data"

if [ ! -d ${RINF_DATA_DIR} ]; then
    echo "directory ${RINF_DATA_DIR} not found, please restore data with './scripts/restore-data-from-kg.sh'"
    exit 1
fi

cd ./src/RInfGraphWeb

pushd ./lib
rm -rf node_modules/rinf-graph/ package-lock.json
npm install
echo "copy files from ${RINF_DATA_DIR}"
DATA_DIR=../../../${RINF_DATA_DIR}
cp ${DATA_DIR}/Graph.json node_modules/rinf-graph/data/ 
cp ${DATA_DIR}/LineInfos.json node_modules/rinf-graph/data/ 
cp ${DATA_DIR}/OpInfos.json node_modules/rinf-graph/data/
cp ${DATA_DIR}/TunnelInfos.json node_modules/rinf-graph/data/ 
cp ${DATA_DIR}/Metadata.json node_modules/rinf-graph/data/ 
tsc
npx webpack --config webpack.config.cjs
cp index.d.ts dist/bundle.d.ts
popd

if [ ! -d "./wwwroot/js" ]; then
    mkdir ./wwwroot/js
fi

if [ ! -d "./wwwroot/js/lib" ]; then
    mkdir ./wwwroot/js/lib
fi

cp lib/dist/bundle.* wwwroot/js/lib/

pushd ./wwwroot/js
tsc --target ES2015 site.ts
popd
