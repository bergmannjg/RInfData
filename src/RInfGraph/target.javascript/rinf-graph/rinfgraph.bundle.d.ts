// Type definitions for rinfgraph.bundle.js

export const rinfgraph: RInfGraph

export interface Location {
    Latitude: number;
    Longitude: number;
}

export interface OperationalPointType {
    readonly station: number;
    readonly smallstation: number;
    readonly passengerterminal: number;
    readonly freightterminal: number;
    readonly depotorworkshop: number;
    readonly traintechnicalservices: number;
    readonly passengerstop: number;
    readonly junction: number;
    readonly borderpoint: number;
    readonly shuntingyard: number;
    readonly technicalchange: number;
    readonly switch: number;
    readonly privatesiding: number;
    readonly domesticborderpoint: number;
}

export interface OpInfo {
    UOPID: string;
    Name: string;
    RinfType: number;
    Latitude: number;
    Longitude: number;
}

export interface LineInfo {
    Line: string;
    Country: string;
    Name: string;
    Length: number;
    StartKm: number;
    EndKm: number;
    UOPIDs: Array<string>;
    Tunnels: Array<string>;
}

export interface TunnelInfo {
    Tunnel: string;
    Length: number;
    StartLong: number;
    StartLat: number;
    StartKm?: number;
    StartOP: string;
    EndLong: number;
    EndLat: number;
    EndKm?: number;
    EndOP: string;
    SingelTrack: boolean;
    Line: string;
}

export interface GraphEdge {
    Node: string;
    Cost: number;
    Line: string;
    Country: string;
    MaxSpeed: number;
    Electrified: boolean;
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
    Country: string;
    StartKm: number;
    EndKm: number;
    MaxSpeed: number;
}

export interface GraphNode {
    Node: string;
    Edges: Array<GraphEdge>;
}

export interface RInfGraph {
    Graph_operationalPointType: OperationalPointType;
    Graph_toGraph: (g: Array<GraphNode>) => Map<string, any>;
    Graph_getShortestPathFromGraph: (g: Array<GraphNode>, graph: Map<string, any>, ids: Array<string>) => Array<GraphNode>
    Graph_getShortestPath: (g: Array<GraphNode>, ids: Array<string>) => Array<GraphNode>
    Graph_getPathOfLineFromGraph: (g: Array<GraphNode>, graph: Map<string, any>, line: LineInfo) => Array<GraphNode>
    Graph_getCompactPath: (path: Array<GraphNode>) => Array<GraphNode>
    Graph_getCompactPathWithMaxSpeed: (path: Array<GraphNode>, g: Array<GraphNode>) => Array<GraphNode>
    Graph_compactifyPath: (path: Array<GraphNode>, g: Array<GraphNode>) => Array<GraphNode>
    Graph_getPathOfLine: (g: Array<GraphNode>, line: LineInfo) => Array<GraphNode>
    Graph_printPath: (path: Array<GraphNode>) => void
    Graph_lengthOfPath: (path: Array<GraphNode>) => number
    Graph_costOfPath: (path: Array<GraphNode>) => number
    Graph_getLocationsOfPath: (g: Array<GraphNode>, opInfos: Map<string, OpInfo>, path: Array<GraphNode>) => Array<Array<Location>>
    Graph_getFilteredLocationsOfPath: (g: Array<GraphNode>, opInfos: Map<string, OpInfo>, path: Array<GraphNode>, excludedRinfTypes: Array<number>) => Array<Array<Location>>
    Graph_toPathElement: (opInfos: Map<string, OpInfo>, lineInfos: Map<string, LineInfo>, node: GraphNode) => PathElement
    Graph_isWalkingPath: (node: GraphNode) => boolean
}