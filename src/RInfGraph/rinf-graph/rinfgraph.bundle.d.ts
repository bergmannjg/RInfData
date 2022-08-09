// Type definitions for rinfgraph.bundle.js

export const rinfgraph: RInfGraph

export interface Location {
    Latitude: number;
    Longitude: number;
}

export interface OpInfo {
    UOPID: string;
    Name: string;
    Latitude: number;
    Longitude: number;
}

export interface LineInfo {
    Line: string;
    IMCode: string;
    Name: string;
    Length: number;
    StartKm: number;
    EndKm: number;
    UOPIDs: Array<string>;
    Tunnels: Array<string>;
}

export interface TunnelInfo {
    Tunnel: string;
    StartLong: number;
    StartLat: number;
    StartKm: number;
    EndLong: number;
    EndLat: number;
    EndKm: number;
    SingelTrack: boolean;
    Line: string;
}

export interface GraphEdge {
    Node: string;
    Cost: number;
    Line: string;
    IMCode: string;
    MaxSpeed: number;
    StartKm: number;
    EndKm: number;
    Length: number;
}

export interface PathElement {
    From: string;
    FromOPID: string;
    To: string;
    ToOPID: string
    Line: string;
    LineText: string;
    IMCode: string;
    StartKm: number;
    EndKm: number;
    MaxSpeed: number;
}

export interface GraphNode {
    Node: string;
    Edges: Array<GraphEdge>;
}

export interface RInfGraph {
    Graph_toGraph: (g: Array<GraphNode>) => Map<string, any>;
    Graph_getShortestPathFromGraph: (g: Array<GraphNode>, graph: Map<string, any>, ids: Array<string>) => Array<GraphNode>
    Graph_getShortestPath: (g: Array<GraphNode>, ids: Array<string>) => Array<GraphNode>
    Graph_getPathOfLineFromGraph: (g: Array<GraphNode>, graph: Map<string, any>, line: LineInfo) => Array<GraphNode>
    Graph_getCompactPath: (path: Array<GraphNode>) => Array<GraphNode>
    Graph_getCompactPathWithMaxSpeed: (path: Array<GraphNode>, g: Array<GraphNode>) => Array<GraphNode>
    Graph_compactifyPath: (path: Array<GraphNode>, g: Array<GraphNode>) => Array<GraphNode>
    Graph_getPathOfLine: (g: Array<GraphNode>, line: LineInfo) => Array<GraphNode>
    Graph_printPath: (path: Array<GraphNode>) => void
    Graph_getLocationsOfPath: (g: Array<GraphNode>, opInfos: Map<string, OpInfo>, path: Array<GraphNode>) => Array<Array<Location>>
    Graph_toPathElement: (opInfos: Map<string, OpInfo>, lineInfos: Map<string, LineInfo>, node: GraphNode) => PathElement
    Graph_isWalkingPath: (node: GraphNode) => boolean
}