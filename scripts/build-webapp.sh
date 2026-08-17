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

if [[ $1 = "--countries" ]] && [[ $# -lt 2 ]]; then
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

if [ "$3" = "--version" ]; then
    PACKAGE="../../RInfGraph/target.javascript/rinf-graph-$4.tgz"
    if [ ! -f ${PACKAGE} ]; then
        echo "file '${PACKAGE}' not found"
        exit 1
    fi
    npm install ${PACKAGE} --save false
else 
    npm install
fi

if [ "$3" = "--skipOsm" ]; then
    SKIPOSM="--skipOsm"
fi

if [ $1 = "--countries" ] 
  then
    dotnet run --project ../../EraKGLoader/EraKGLoader.fsproj --Build node_modules/rinf-graph/data/ $2 ${SKIPOSM}
fi

if [ $1 = "--cache" ] 
  then
    echo "copy files from $2"
    DATA_DIR=../../../$2
    cp ${DATA_DIR}/Graph.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/LineInfos.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/OpInfos.json node_modules/rinf-graph/data/
    cp ${DATA_DIR}/SectionsOfLines.json node_modules/rinf-graph/data/
    cp ${DATA_DIR}/TunnelInfos.json node_modules/rinf-graph/data/ 
    cp ${DATA_DIR}/Metadata.json node_modules/rinf-graph/data/ 
fi

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
