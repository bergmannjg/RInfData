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

            let loader =
                if useRemote then
                    OverpassRequest.exec client
                else
                    OSM.Command.execQuery

            let! response = loader query
            File.WriteAllText(osmDataDir + filename (line), response)
            return readFile<OSMJson> osmDataDir (filename line)
        }

let loadAllStops osmDataDir uicRefMappings =
    let wayStops =
        readFile<OSMJson> osmDataDir "way-stations.json"
        |> fun g -> OSM.Transform.Op.wayStopsToOsmOperationalPoints uicRefMappings g.elements

    printfn "wayStops: %d" wayStops.Length

    let relationStops =
        readFile<OSMJson> osmDataDir "relation-stations.json"
        |> fun g -> OSM.Transform.Op.relationStopsToOsmOperationalPoints uicRefMappings g.elements

    printfn "relationStops: %d" relationStops.Length

    let nodeStops =
        readFile<OSMJson> osmDataDir "node-stations.json"
        |> fun g -> OSM.Transform.Op.nodeStopsToOsmOperationalPoints uicRefMappings g.elements

    printfn "nodeStops: %d" nodeStops.Length

    let nodeStopsCH =
        readFile<OSMJson> osmDataDir "node-stations-ch.json"
        |> fun g -> OSM.Transform.Op.nodeStopsToOsmOperationalPoints uicRefMappings g.elements

    printfn "nodeStopsCH: %d" nodeStopsCH.Length

    Array.concat [ nodeStops
                   nodeStopsCH
                   relationStops
                   wayStops ]
