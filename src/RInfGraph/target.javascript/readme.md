# Graph Library

Find shortest path for operational points, assumes that graph files are generated by RInfLoader.

## Compile to JavaScript via [Fable](https://github.com/fable-compiler/Fable)

* run: build-package.sh
* run: ./rinf-graph/bin/EraKGLoader --Build ./rinf-graph/data/ DEU
* usage: node index.js rinf-graph/data/Graph.json "DE000HH;DE000AH"

## Build rinf-data module

* run: build-package.sh
* module is used by [FahrplanApp](https://github.com/bergmannjg/FahrplanApp)
