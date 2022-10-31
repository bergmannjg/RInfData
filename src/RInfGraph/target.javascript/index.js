const { readFile } = require('fs/promises');
const { rinfgraph } = require('./rinf-graph/rinfgraph.bundle.js');

readFile(process.argv[2])
    .then(json => {
        const g = JSON.parse(json);
        const path = rinfgraph.Graph_getShortestPath(g, process.argv[3].split(";"));
        console.log('path:')
        rinfgraph.Graph_printPath(path);
        const cpath = rinfgraph.Graph_getCompactPath(path);
        console.log('compact path:')
        rinfgraph.Graph_printPath(cpath);
        const ccpath = rinfgraph.Graph_getCompactPath(rinfgraph.Graph_compactifyPath(path, g));
        if (cpath.length > ccpath.length) {
            console.log('compactified path:')
            rinfgraph.Graph_printPath(ccpath);
        }
    }).catch((error) => {
        console.error(error);
    });
