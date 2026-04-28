#!/usr/bin/env bash
# build OpTypes.json file

set -e

if [ ! -d "./scripts" ]; then
    echo "please run from project directory"
    exit 1
fi

dotnet run --project src/EraKGLoader/EraKGLoader.fsproj --OpTypes > src/RInfGraphWeb/lib/OpTypes.json
