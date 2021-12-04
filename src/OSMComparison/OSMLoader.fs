module OSMLoader

open System.IO
open System.Text.Json
open System.Text.Json.Serialization

open OSM

let private readFile<'a> path name =
    let options = JsonSerializerOptions()

    options.Converters.Add(
        JsonFSharpConverter(
            JsonUnionEncoding.InternalTag
            ||| JsonUnionEncoding.UnwrapRecordCases
            ||| JsonUnionEncoding.UnwrapOption,
            unionTagName = "type",
            unionTagCaseInsensitive = true
        )
    )

    JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name), options)

let private filename (line: int) = sprintf "route-%d.json" line

let getLineQuery (line: int) (area: string option) =
    sprintf
        """
        [timeout:900][out:json];%s
        (
          (
            rel
              [route=tracks]
              [ref=%d]
              [type=route];
            (
              ._;>;
            );
            (
              ._;
              way(r);
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area];
              node(r)["railway:ref"];
            );
            (
              ._;
              way(r);
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area]["railway:ref"];
            );
            (
              ._;
              way(r);
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area]["uic_ref"];
            );
            (
              ._;
              way(r);
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area];
              way(r)["railway:ref"];
              node(w);
            );
            (
              ._;
              way(r);
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area];
              way(r)[!"railway:ref"]["uic_ref"];
              node(w);
            );
            (
              ._;
              way(r);
              node(w)["railway"=stop];
              relation(bn)[public_transport=stop_area];
              node(r)["railway:ref"];
            );
            (
              ._;
              way(r);
              node(w)["railway"=switch];
            );
            (
              ._;
              way(r);
              node(w)["railway"=switch];
              way(bn)["railway"="rail"][!"railway:ref"]["service"="siding"];
              node(w)["public_transport"=stop_position];
              relation(bn)[public_transport=stop_area];
              node(r)["railway:ref"];
            );
            (
              ._;
              node(r)["uic_ref"];
            );
          );
          (
            way["ref"=%d]["railway"="rail"];
            node(w);
          );
        );
        out;
    """
        (match area with
         | Some area ->
             "area[boundary=administrative][name='"
             + area
             + "'];"
         | None -> "")
        line
        line

let loadOsmData osmDataDir client (useRemote: bool) (area: string option) (line: int) =
    if
        (not useRemote)
        && File.Exists(osmDataDir + (filename line))
    then
        async { return readFile<OSMJson> osmDataDir (filename line) }
    else
        async {
            let query = getLineQuery line (if useRemote then area else None)

            let url =
                if useRemote then
                    "https://overpass-api.de/api/"
                else
                    "http://localhost:12345/api/"

            let! response = OverpassRequest.exec client url query
            File.WriteAllText(osmDataDir + filename (line), response)
            return readFile<OSMJson> osmDataDir (filename line)
        }

let loadAllStops osmDataDir uicRefMappings =

    let load (prefix: string) (map: DB.UicRefMapping [] -> Element [] -> OsmOperationalPoint []) (g: OSMJson) =
        let sw = new System.Diagnostics.Stopwatch()
        sw.Start()
        let pts = map uicRefMappings g.elements
        sw.Stop()
        printfn "%s: %d, elements %d, %d msecs" prefix pts.Length g.elements.Length sw.ElapsedMilliseconds
        pts

    let wayStops =
        readFile<OSMJson> osmDataDir "way-stations.json"
        |> load "wayStops" OSM.Transform.Op.wayStopsToOsmOperationalPoints

    let relationStops =
        readFile<OSMJson> osmDataDir "relation-stations.json"
        |> load "relationStops" OSM.Transform.Op.relationStopsToOsmOperationalPoints

    let nodeStops =
        readFile<OSMJson> osmDataDir "node-stations.json"
        |> load "nodeStops" OSM.Transform.Op.nodeStopsToOsmOperationalPoints

    let nodeStopsCH =
        readFile<OSMJson> osmDataDir "node-stations-ch.json"
        |> load "nodeStopsCH" OSM.Transform.Op.nodeStopsToOsmOperationalPoints

    Array.concat [ nodeStops
                   nodeStopsCH
                   relationStops
                   wayStops ]
