const { readFile } = require('fs/promises');
const { RInfGraph } = require('./bundle/RInfGraph.bundle.js');

readFile(process.argv[2])
    .then(json => {
        const g = JSON.parse(json);
        const path = RInfGraph.Graph_getShortestPath(g, process.argv[3].split(";"));
        console.log(RInfGraph.Graph_printShortestPath(path));
    }).catch((error) => {
        console.error(error);
    });
