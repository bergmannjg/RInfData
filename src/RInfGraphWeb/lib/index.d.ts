export interface OpInfo {
    UOPID: string;
    Name: string;
    RinfType: number;
    Latitude: number;
    Longitude: number;
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
    Country: string;
}

export interface GraphNode {
    Node: string;
    Edges: Array<GraphEdge>;
}

export interface Metadata {
    Endpoint: string;
    Ontology: string;
    Revision: string;
    Program: string;
    Countries: string[];
    Date: string
}

export interface Matching {
    UOPID: string;
    OsmUrl?: string;
}

export interface OpType {
    Label: string;
    Definition: string;
    Value: number;
}

export function rinfFindPath(ids: string[], isCompactifyPath: boolean): GraphNode[];
export function rinfFindPathOfLine(ine: string, country: string): GraphNode[];
export function rinfFindTunnelsOfLine(line: string, country: string): TunnelInfo[];
export function rinfToCompactPath(path: GraphNode[]): GraphNode[];
export function rinfGetOpInfos(name: string, uopid: string): OpInfo[];
export function rinfGetBRouterUrls(arr: GraphNode[], compactifyPath: boolean): string[];
export function rinfMetadata() : Metadata;
export function rinfOsmMatchings(): Matching[];
export function rinfOpTypes(): OpType[];


