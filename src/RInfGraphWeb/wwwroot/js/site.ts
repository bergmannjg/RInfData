import { rinfOsmMatchings, rinfOpTypes, rinfFindPath, rinfFindPathOfLine, rinfFindTunnelsOfLine, rinfGetBRouterUrls, rinfToCompactPath, rinfGetOpInfos, rinfMetadata, Metadata, OpInfo, TunnelInfo, OpType } from "./lib/bundle.js";

interface PathItem {
    fromId: string;
    toId: string;
    line: string;
    maxSpeed: number;
    startKm: number;
    endKm: number;
    length: number;
    cost: number;
}

interface PathResult {
    path: PathItem[];
    tunnels: TunnelInfo[];
    urls: string[];
}

const findOsmUrl = (op: OpInfo): string | undefined => {
    return rinfOsmMatchings().find(m => m.UOPID == op.UOPID)?.OsmUrl;
}

const findOpType = (op: OpInfo): OpType | undefined => {
    return rinfOpTypes().find(m => m.Value == op.RinfType);
}

const findPath = (ids: string[], isCompactifyPath: boolean): PathResult => {
    const spath = rinfFindPath(ids, isCompactifyPath);
    let arr: PathItem[] = [];
    let cost: number = 0;
    rinfToCompactPath(spath).forEach(x => {
        console.log(x);
        if (!!x.Node && !!x.Edges) {
            cost = cost + x.Edges[0].Cost;
            arr.push({
                fromId: x.Node, toId: x.Edges[0].Node, line: x.Edges[0].Line,
                maxSpeed: x.Edges[0].MaxSpeed, startKm: x.Edges[0].StartKm, endKm: x.Edges[0].EndKm, length: x.Edges[0].Length, cost: cost
            })
        }
    });
    const urls = rinfGetBRouterUrls(spath, true);
    console.log('urls', urls);
    return { path: arr, tunnels: [], urls: urls };
}

function compare(a: TunnelInfo, b: TunnelInfo) {
    if (a.StartKm && b.StartKm) { return a.StartKm - b.StartKm } else return 0;
}

const findPathOfLine = (line: string, country: string): PathResult => {
    const spath = rinfFindPathOfLine(line, country);
    let arr: PathItem[] = [];
    let cost: number = 0;
    spath.forEach(x => {
        console.log(x);
        if (!!x.Node && !!x.Edges) {
            cost = cost + x.Edges[0].Cost;
            arr.push({
                fromId: x.Node, toId: x.Edges[0].Node, line: x.Edges[0].Line,
                maxSpeed: x.Edges[0].MaxSpeed, startKm: x.Edges[0].StartKm, endKm: x.Edges[0].EndKm, length: x.Edges[0].Length, cost: cost
            })
        }
    });
    const urls = rinfGetBRouterUrls(spath, false);
    console.log('urls', urls);
    const tunnels = rinfFindTunnelsOfLine(line, country);
    const sorted = tunnels.sort(compare);
    return { path: arr, tunnels: sorted, urls: urls };
}

const addRow = (tb: HTMLElement, items: (string | HTMLElement)[]) => {
    var tr = document.createElement("tr");
    tb.appendChild(tr);
    items.forEach(x => {
        var td = document.createElement("td");
        if (typeof x === 'string') td.textContent = x;
        else if (typeof x === 'object') td.appendChild(x as HTMLElement);
        tr.appendChild(td);
    });
}

export function displayElement(nameShown: string) {
    const matches = document.querySelectorAll("div.row > div");
    matches.forEach(d => {
        if (d.id.endsWith('box')) (d as HTMLElement).style.display = 'none';
    });
    const element = document.getElementById(nameShown);
    if (element) element.style.display = 'initial';
}

function removeChilds(element: HTMLElement) {
    while (element.firstChild) {
        element.removeChild(element.firstChild);
    }
}

function createTextEnd(text: string): HTMLElement {
    var div = document.createElement("div");
    var cls = document.createAttribute("class");
    cls.value = "text-end";
    div.setAttributeNode(cls);
    div.textContent = text;
    return div;
}

function createOptions(options: string[], id: string): HTMLElement {
    const select = document.createElement("select");
    const clsAttr = document.createAttribute("class");
    clsAttr.value = "form-select";
    select.setAttributeNode(clsAttr);
    const idAttr = document.createAttribute("id");
    idAttr.value = id;
    select.setAttributeNode(idAttr);
    options.forEach(name => {
        const option = document.createElement("option");
        const valueAttr = document.createAttribute("value");
        valueAttr.value = name;
        option.setAttributeNode(valueAttr);
        option.textContent = name;
        select.add(option);
    });
    return select;
}

function addToolTip(elem: HTMLElement, tooltip: string) {
    var datatoggle = document.createAttribute("data-toggle");
    datatoggle.value = "tooltip";
    elem.setAttributeNode(datatoggle);
    var dataplacement = document.createAttribute("data-placement");
    dataplacement.value = "top";
    elem.setAttributeNode(dataplacement);
    var title = document.createAttribute("title");
    title.value = tooltip;
    elem.setAttributeNode(title);
}

function createSpan(text: string, tooltip?: string): HTMLElement {
    var span = document.createElement("span");
    span.textContent = text;

    if (tooltip) {
        addToolTip(span, tooltip);
    }

    return span;
}

function createUrl(url: string, text?: string, tooltip?: string): HTMLElement {
    var a = document.createElement("a");
    var href = document.createAttribute("href");
    href.value = url;
    a.setAttributeNode(href);
    var target = document.createAttribute("target");
    target.value = "_blank";
    a.setAttributeNode(target);

    if (tooltip) {
        addToolTip(a, tooltip);
    }

    a.textContent = text ? text : "link";
    return a;
}

const createElemsInSpan = (items: HTMLElement[]) => {
    var span = document.createElement("span");
    items.forEach(item => {
        const itemAttr = document.createAttribute("style");
        itemAttr.value = "padding-right: 10px;";
        item.setAttributeNode(itemAttr);
        span.appendChild(item);
    });
    return span
}

function stringCompare(a: string, b: string) {
    return a.localeCompare(b);
}

export function displayCountryOptions(idCountryOptions: string, idInputCountry: string) {
    const div = document.getElementById(idCountryOptions) as HTMLDivElement;
    const data = rinfMetadata();
    const options = createOptions(data.Countries.sort(stringCompare), idInputCountry);
    div.append(options);
}

export function displayMetadata(idEndpoint: string, idDate: string, idCountries: string) {
    const endpoint = document.getElementById(idEndpoint) as HTMLSpanElement;
    const data = rinfMetadata();
    if (endpoint) {
        endpoint.appendChild(createUrl(data.Endpoint, 'Endpoint'));
    }
    const dateElement = document.getElementById(idDate) as HTMLSpanElement;
    if (dateElement) {
        const dt = new Date(Date.parse(data.Date));
        dateElement.textContent = dt.toDateString();
    }
    const countriesElement = document.getElementById(idCountries) as HTMLSpanElement;
    if (countriesElement) {
        countriesElement.textContent = data.Countries.sort(stringCompare).join(",");
    }
}

function getBRouterUrlOfLocation(latitude: number, longitude: number, info: string) {
    return 'https://brouter.de/brouter-web/#map=13/' + latitude + '/' + longitude
        + '/osm-mapnik-german_style&pois=' + longitude + ',' + latitude + ',' + info;
}

function getCenter(latitudes: number[], longitudes: number[]): number[] {
    const minLatitude = Math.min(...latitudes);
    const maxLatitude = Math.max(...latitudes);
    const minLongitude = Math.min(...longitudes);
    const maxLongitude = Math.max(...longitudes);
    const latitude = Math.abs(minLatitude + (maxLatitude - minLatitude) / 2);
    const longitude = Math.abs(minLongitude + (maxLongitude - minLongitude) / 2);
    return [latitude, longitude];
}

function getBRouterUrlOfLocations(latitude1: number, longitude1: number, latitude2: number, longitude2: number, info: string) {
    const latlon = getCenter([latitude1, latitude2], [longitude1, longitude2])
    const pois = longitude1 + ',' + latitude1 + ',' + info + ';' + longitude2 + ',' + latitude2 + ',' + info;
    return 'https://brouter.de/brouter-web/#map=13/' + latlon[0] + '/' + latlon[1]
        + '/osm-mapnik-german_style&pois=' + pois;
}

function getOrmUrlOfLocation(latitude: number, longitude: number) {
    return 'https://www.openrailwaymap.org/?lang=en&lat=' + latitude + '&lon=' + longitude + '&zoom=14&style=standard';
}

function getOverpassUrlOfLocation(op: OpInfo) {
    return 'https://overpass-turbo.eu/?Q=node[railway~"stop|halt|station"](around:1000,' + op.Latitude + ',' + op.Longitude + ');out;';
}

function getOsmUrlOfLocation(op: OpInfo) {
    return 'https://www.openstreetmap.org/#map=17/' + op.Latitude + '/' + op.Longitude;
}

function getBRouterUrlOfOpInfos(arr: OpInfo[]) {
    if (arr.length == 1) {
        let x = arr[0];
        return getBRouterUrlOfLocation(x.Latitude, x.Longitude, x.UOPID);
    } else {
        const latlon = getCenter(arr.map(op => op.Latitude), arr.map(op => op.Longitude))
        const pois = arr.reduce((acc, x) => acc + x.Longitude + ',' + x.Latitude + ',' + x.UOPID + ';', '');
        return 'https://brouter.de/brouter-web/#map=10/' + latlon[0] + '/' + latlon[1]
            + '/osm-mapnik-german_style&pois=' + pois;

    }
}

function rinfGetKgUrlOfUOPID(uopid: string) {
    return 'http://data.europa.eu/949/functionalInfrastructure/operationalPoints/' + (uopid.replace(' ', '%2520'));
}

function rinfGetLocationUrlOfUOPID(uopid: string) {
    const results = rinfGetOpInfos('', uopid);
    return getBRouterUrlOfOpInfos(results);
}

function getTooltipOfId(uopid: string): string | undefined {
    const results = rinfGetOpInfos('', uopid);
    return results[0]?.Name;
}

export function lookupLocations(inputLocation: string, inputUPID: string) {
    var tableLocations = document.getElementById("result-locations");
    var spanUrls = document.getElementById("result-url-of-locations") as HTMLSpanElement;
    if (tableLocations && spanUrls) {
        removeChilds(tableLocations);
        removeChilds(spanUrls);
        var element = document.getElementById(inputLocation) as HTMLInputElement;
        var elementUOPID = document.getElementById(inputUPID) as HTMLInputElement;
        if (element.value.length > 0 || elementUOPID.value.length > 0) {
            const results = rinfGetOpInfos(element.value, elementUOPID.value);
            console.log('results', results.length);
            results.sort((a, b) => {
                const nameA = a.Name;
                const nameB = b.Name;
                if (nameA < nameB) return -1;
                else if (nameA > nameB) return 1;
                else return 0;
            });
            results.forEach(x => {
                const url1 = createUrl(getBRouterUrlOfOpInfos([x]), 'BRouter');
                const url2 = createUrl(getOrmUrlOfLocation(x.Latitude, x.Longitude), 'ORM');
                const osmUrl = findOsmUrl(x);
                const url3 = osmUrl ? createUrl(osmUrl, osmUrl.replace('https://www.openstreetmap.org/', '')) : undefined;
                const opType = findOpType(x);
                if (tableLocations) {
                    addRow(tableLocations, [
                        x.Name,
                        opType ? createSpan(opType.Label, opType.Definition) : createSpan(''),
                        createUrl(rinfGetKgUrlOfUOPID(x.UOPID), x.UOPID),
                        url3 ? url3 : '',
                        createElemsInSpan([url1, url2])]);
                }
            });

            if (results.length < 1000) {
                const url = getBRouterUrlOfOpInfos(results);
                if (spanUrls && url) spanUrls.appendChild(createUrl(url, results.length.toFixed()))
            } else {
                if (spanUrls) spanUrls.textContent = results.length.toFixed();
            }
        }
    }
}

export function lookupOsmComparison() {
    var tableSummary = document.getElementById("result-summary-osm-comparison");
    var tableLocations = document.getElementById("result-locations-osm-comparison");
    if (tableSummary && tableLocations) {
        removeChilds(tableSummary);
        removeChilds(tableLocations);
        const total = rinfOsmMatchings().length;
        const found = rinfOsmMatchings().filter(m => m.OsmUrl).length;
        addRow(tableSummary, ["Total (DE)", createTextEnd(total.toFixed(0))]);
        addRow(tableSummary, ["OSM data found", createTextEnd(found.toFixed(0))]);
        addRow(tableSummary, ["OSM data not found", createTextEnd((total - found).toFixed(0))]);
        const results = rinfOsmMatchings()
            .filter(m => !m.OsmUrl)
            .map(m => {
                const opInfos = rinfGetOpInfos('', m.UOPID);
                if (opInfos.length == 1) return opInfos[0]; else return undefined;
            })
            .filter(op => op) as OpInfo[];
        console.log('results', results.length);
        results.sort((a, b) => {
            const nameA = a.Name;
            const nameB = b.Name;
            if (nameA < nameB) return -1;
            else if (nameA > nameB) return 1;
            else return 0;
        });
        results.forEach(x => {
            const url1 = createUrl(getBRouterUrlOfOpInfos([x]), 'BRouter');
            const url2 = createUrl(getOrmUrlOfLocation(x.Latitude, x.Longitude), 'ORM');
            const url3 = createUrl(getOsmUrlOfLocation(x), 'OSM');
            const url4 = createUrl(getOverpassUrlOfLocation(x), 'Overpass');
            const opType = findOpType(x);
            if (tableLocations) {
                addRow(tableLocations, [
                    x.Name,
                    opType ? createSpan(opType.Label, opType.Definition) : createSpan(''),
                    createUrl(rinfGetKgUrlOfUOPID(x.UOPID), x.UOPID),
                    createElemsInSpan([url1, url2, url3, url4])]);
            }
        });
    }
}

export function lookupPath(inputFrom: string, inputOver: string, inputTo: string, inputCheckExact: string) {
    var tablePath = document.getElementById("result-path");
    var spanUrls = document.getElementById("result-url-of-path") as HTMLSpanElement;
    if (tablePath && spanUrls) {
        removeChilds(tablePath);
        removeChilds(spanUrls);
        var elementFrom = document.getElementById(inputFrom) as HTMLInputElement;
        var elementOver = document.getElementById(inputOver) as HTMLInputElement;
        var elementTo = document.getElementById(inputTo) as HTMLInputElement;
        var elementCheckExact = document.getElementById(inputCheckExact) as HTMLInputElement;
        if (elementFrom.value.length > 0 && elementTo.value.length > 0) {
            const ids = elementOver.value.length > 0 ? [elementFrom.value, elementOver.value, elementTo.value] : [elementFrom.value, elementTo.value];
            const results = findPath(ids, elementCheckExact ? !elementCheckExact.checked : true)
            results.path.forEach(x => {
                if (tablePath) {
                    addRow(tablePath, [createUrl(rinfGetLocationUrlOfUOPID(x.fromId), x.fromId, getTooltipOfId(x.fromId)),
                    createUrl(rinfGetLocationUrlOfUOPID(x.toId), x.toId, getTooltipOfId(x.toId)), x.line, createTextEnd(x.maxSpeed.toFixed(0)),
                    createTextEnd(x.startKm.toFixed(3)), createTextEnd(x.endKm.toFixed(3)), createTextEnd(x.length.toFixed(3)),
                    createTextEnd(x.cost.toFixed(0))]);
                }
            });
            if (results.path.length > 0 && tablePath) {
                const sum = results.path.reduce((acc, p) => acc + p.length, 0);
                addRow(tablePath, ['', '', '', '', '', '', createTextEnd(sum.toFixed(3)), '']);
            }
            if (results.urls.length == 1 && spanUrls) {
                spanUrls.appendChild(createUrl(results.urls[0], 'Route'));
            }
        }
    }
}

export function lookupLine(inputLine: string, inputCountry: string) {
    var tablePath = document.getElementById("result-line");
    var tableTunnels = document.getElementById("result-tunnels");
    var spanUrls = document.getElementById("result-url-of-line") as HTMLSpanElement;
    if (tablePath && tableTunnels && spanUrls) {
        removeChilds(tablePath);
        removeChilds(tableTunnels);
        removeChilds(spanUrls);
        var elementLine = document.getElementById(inputLine) as HTMLInputElement;
        var elementCountry = document.getElementById(inputCountry) as HTMLInputElement;
        if (elementLine.value.length > 0 && elementCountry.value.length > 0) {
            const results = findPathOfLine(elementLine.value, elementCountry.value)
            results.path.forEach(x => {
                if (tablePath) {
                    addRow(tablePath, [createUrl(rinfGetLocationUrlOfUOPID(x.fromId), x.fromId, getTooltipOfId(x.fromId)),
                    createUrl(rinfGetLocationUrlOfUOPID(x.toId), x.toId, getTooltipOfId(x.toId)), createTextEnd(x.maxSpeed.toFixed(0)),
                    createTextEnd(x.startKm.toFixed(3)), createTextEnd(x.endKm.toFixed(3)), createTextEnd(x.length.toFixed(3)),
                    createTextEnd(x.cost.toFixed(0))])
                }
            });
            results.tunnels.forEach(x => {
                if (tableTunnels) {
                    addRow(tableTunnels, [x.Tunnel, createUrl(rinfGetLocationUrlOfUOPID(x.StartOP), x.StartOP),
                    createUrl(rinfGetLocationUrlOfUOPID(x.EndOP), x.EndOP),
                    createUrl(getBRouterUrlOfLocations(x.StartLat, x.StartLong, x.EndLat, x.EndLong, x.Tunnel), x.StartKm?.toFixed(3) + ' bis ' + x.EndKm?.toFixed(3)),
                    createTextEnd(x.Length.toFixed(3))])
                }
            });
            if (results.tunnels.length > 0 && tableTunnels) {
                var sum = results.tunnels.reduce(function (prev, cur) {
                    return prev + cur.Length;
                }, 0);
                addRow(tableTunnels, ['Length', '', '', '', createTextEnd(sum.toFixed(3))])
            }
            if (results.urls.length == 1 && spanUrls) {
                spanUrls.appendChild(createUrl(results.urls[0], 'Route'));
            }
        }
    }
}
