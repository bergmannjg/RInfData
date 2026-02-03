import { rinfgraph } from 'rinf-graph';
import _g from 'rinf-graph/data/Graph.json' with { type: 'json' };
import opInfos from 'rinf-graph/data/OpInfos.json' with { type: 'json' };
import lineInfos from 'rinf-graph/data/LineInfos.json' with { type: 'json' };
import tunnelInfos from 'rinf-graph/data/TunnelInfos.json' with { type: 'json' };
import metadata from 'rinf-graph/data/Metadata.json' with { type: 'json' };
const g = _g;
const graph = rinfgraph.Graph_toGraph(g);
const operationalPointType = rinfgraph.Graph_operationalPointType;
const optypeExcludes = [operationalPointType.privatesiding, operationalPointType.depotorworkshop];
const mapOps = opInfos.reduce((map, op) => map.set(op.UOPID, op), new Map());
export function rinfFindPath(ids, isCompactifyPath) {
    const spath = rinfgraph.Graph_getShortestPathFromGraph(g, graph, ids);
    if (isCompactifyPath) {
        return rinfgraph.Graph_compactifyPath(spath, g);
    }
    else {
        return spath;
    }
}
export function rinfFindPathOfLine(line, country) {
    const lineInfo = lineInfos.find(li => li.Line === line && li.Country == country);
    if (lineInfo)
        return rinfgraph.Graph_getPathOfLineFromGraph(g, graph, lineInfo);
    else
        return [];
}
export function rinfFindTunnelsOfLine(line, country) {
    const result = tunnelInfos.filter(li => li.Line === line && li.Country == country);
    if (result)
        return result;
    else
        return [];
}
export function rinfToCompactPath(spath) {
    return rinfgraph.Graph_getCompactPath(spath);
}
export function rinfGetBRouterUrls(path, compactifyPath) {
    const locations = rinfgraph.Graph_getFilteredLocationsOfPath(g, mapOps, compactifyPath ? rinfToCompactPath(path) : path, optypeExcludes);
    return locations.map(l => rinfgraph.Graph_getBRouterUrl(l));
}
function replaceAll(str, find, replace) {
    return str.replace(new RegExp(find, 'g'), replace);
}
// include letters with stroke in text search, todo: incomplete
function findText(s, searchString) {
    const patterns = [['l', '[lł]'], ['o', '[oó]']];
    const pattern = patterns.reduce((acc, arr) => replaceAll(acc, arr[0], arr[1]), searchString);
    console.log('findText pattern', pattern);
    try {
        const regex = new RegExp(pattern, 'i');
        return regex.test(s);
    }
    catch (_) {
        return s.indexOf(searchString) != -1;
    }
}
export function rinfGetOpInfos(name, uopid) {
    console.log('rinfGetOpInfos', name, uopid);
    return opInfos.filter(op => {
        if (name && name.length > 0 && uopid && uopid.length > 0) {
            return op.Name.indexOf(name) != -1 && op.UOPID.indexOf(uopid) != -1;
        }
        else if (name && name.length > 0) {
            return findText(op.Name, name);
        }
        else if (uopid && uopid.length > 0) {
            return op.UOPID == uopid;
        }
        else
            return true;
    });
}
export function rinfMetadata() {
    return metadata;
}
// @ts-expect-error
if (typeof globalThis.window !== "object") {
    const args = process.argv.slice(2);
    const path = args.length === 1 ? rinfFindPathOfLine(args[0], 'DEU') : (args.length === 2) ? rinfFindPath([args[0], args[1]], true) : [];
    rinfToCompactPath(path).forEach(node => console.log(node.Node, node.Edges[0].Node, node.Edges[0].Line, node.Edges[0].MaxSpeed.toFixed(0)));
    const urls = rinfGetBRouterUrls(path, true);
    console.log('urls', urls);
}
