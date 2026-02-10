#!/usr/bin/env bash
# build RInfGraph web app

set -e

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

if [[ $1 != "--countries" ]] && [[ $1 != "--cache" ]]
  then
    echo "usage $0 [--countries <countries>] [--cache <cachedir>] "
    exit 1
fi

if [[ $1 = "--countries" ]] && [[ $# -ne 2 ]]; then
    echo "country arg expected"
    exit 1
fi

if [[ $1 = "--cache" ]] && [[ ! -d $2 ]]; then
    echo "directory '$2' not found"
    exit 1
fi

cd ./src/RInfGraphWeb

pushd ./lib
rm -rf node_modules/rinf-graph/ package-lock.json dist
npm install

if [ $1 = "--countries" ] 
  then
    ./node_modules/rinf-graph/bin/EraKGLoader $2
fi

if [ $1 = "--cache" ] 
  then
    echo "copy files from $2"
    DATA_DIR=../../../$2
    cp ${DATA_DIR}/Graph.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/LineInfos.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/OpInfos.json node_modules/rinf-graph/data/
    cp ${DATA_DIR}/TunnelInfos.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/Metadata.json node_modules/rinf-graph/data/ 
fi

dotnet run --project ../../OSMComparison/OsmComparison.fsproj --Osm node_modules/rinf-graph/data/
dotnet run --project ../../OSMComparison/OsmComparison.fsproj --Osm.Compare node_modules/rinf-graph/data/ ./
dotnet run --project ../../EraKGLoader/EraKGLoader.fsproj --OpTypes > OpTypes.json

npx tsc
npx webpack --config webpack.config.cjs
cp index.d.ts dist/bundle.d.ts
popd

if [ ! -d "./wwwroot/js/lib" ]; then
    mkdir ./wwwroot/js/lib
fi

cp lib/dist/bundle.* wwwroot/js/lib/

pushd ./wwwroot/js
npm install
npx tsc
popd
