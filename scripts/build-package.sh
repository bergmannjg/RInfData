#!/usr/bin/env bash
# build RInfGraph js package

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

dotnet build src/EraKGLoader/EraKGLoader.fsproj

dotnet publish src/EraKGLoader/EraKGLoader.fsproj

pushd ./src/RInfGraph/target.javascript

if [ ! -d "./rinf-graph" ]; then
    echo "rinf-graph not found"
    exit 1
fi

if [ ! -d "./rinf-graph/data" ]; then
    mkdir rinf-graph/data
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

rm rinf-graph/data/*
echo '[]' >  rinf-graph/data/Graph.json
echo '[]' >  rinf-graph/data/OpInfos.json
echo '[]' >  rinf-graph/data/LineInfos.json
echo '[]' >  rinf-graph/data/TunnelInfos.json

rm -rf rinf-graph/bin/
VERSION=$(grep TargetFramework ../../EraKGLoader/EraKGLoader.fsproj | sed 's/[<>]/ /g' | awk '{print $2}')
cp -r ../../EraKGLoader/bin/Release/${VERSION}/publish/ rinf-graph/bin/

npm pack rinf-graph/

popd
