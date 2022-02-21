const { readFile } = require('fs/promises');
const { rinfgraph } = require('./rinf-graph/rinfgraph.bundle.js');

readFile(process.argv[2])
    .then(json => {
        const g = JSON.parse(json);
        const path = rinfgraph.Graph_getShortestPath(g, process.argv[3].split(";"));
        rinfgraph.Graph_printPath(path);
        rinfgraph.Graph_printPath(rinfgraph.Graph_getCompactPath(path));
    }).catch((error) => {
        console.error(error);
    });
