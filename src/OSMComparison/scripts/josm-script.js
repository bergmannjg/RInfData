/*
 * script for the Open Street Map editor JOSM to add tag railway:ref
 */
var layers = require("josm/layers");
var command = require("josm/command");
var api = require("josm/api").Api;
var builder = require("josm/builder");
var OsmDataLayer = org.openstreetmap.josm.gui.layer.OsmDataLayer;
var DataSet = org.openstreetmap.josm.data.osm.DataSet;

function add_railwayref(id, type, railwayref) {
    var name = "Datenebene railway:ref";

    if (!layers.has(name)) {
        var dataLayer = new OsmDataLayer(new DataSet(), name, null);
        layers.add(dataLayer);
    }
    if (layers.has(name)) {
        var layer = layers.get(name);
        var element = layer.data.get(id, type);

        if (!element) {
            var ds = api.downloadObject(id, type);

            layer.mergeFrom(ds);
            element = layer.data.get(id, type);
        }

        if (element && !element.hasKey("railway:ref")) {
            layer.apply(
                command.change(element, { tags: { "railway:ref": railwayref } })
            );
        }
    }
}


function add_node(lat, lon, name, railway, railwayref) {
    var dsname = "Datenebene railway:ref";

    if (!layers.has(dsname)) {
        var dataLayer = new OsmDataLayer(new DataSet(), dsname, null);
        layers.add(dataLayer);
    }
    if (layers.has(dsname)) {
        var layer = layers.get(dsname);
        var nb = new builder.NodeBuilder();

        var node = nb.create({ lat: lat, lon: lon });
        node.tags = { name: name, operator: 'DB Netz AG', railway: railway, 'railway:ref': railwayref };

        layer.apply(
            command.add(node)
        );

    }
}