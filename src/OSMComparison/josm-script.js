/*
 * script for the Open Street Map editor JOSM to add tag railway:ref
 */
var layers = require("josm/layers");
var command = require("josm/command");
var api = require("josm/api").Api;
var OsmDataLayer = org.openstreetmap.josm.gui.layer.OsmDataLayer;
var DataSet = org.openstreetmap.josm.data.osm.DataSet;

function add_railwayref(id, railwayref) {
    var name = "Datenebene railway:ref";

    if (!layers.has(name)) {
        var dataLayer = new OsmDataLayer(new DataSet(), name, null);
        layers.add(dataLayer);
    }
    if (layers.has(name)) {
        var layer = layers.get(name);
        var node = layer.data.node(id);

        if (!node) {
            var ds = api.downloadObject(id, "node");

            layer.mergeFrom(ds);
            node = layer.data.node(id);
        }

        if (node && !node.hasKey("railway:ref")) {
            layer.apply(
                command.change(node, { tags: { "railway:ref": railwayref } })
            );
        }
    }
}


