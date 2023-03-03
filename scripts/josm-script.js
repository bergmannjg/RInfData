/*
 * script for the Open Street Map editor JOSM to add tag railway:ref (see https://gubaer.github.io/josm-scripting-plugin/docs/v3/v3.html)
 */
import layers from 'josm/layers'
import { buildAddCommand, buildChangeCommand } from 'josm/command'
import { Api } from 'josm/api'
import { NodeBuilder } from 'josm/builder'
import { DataSet, DataSetUtil } from 'josm/ds'

var OsmDataLayer = Java.type('org.openstreetmap.josm.gui.layer.OsmDataLayer');

function getElement(ds, type, id) {
    const dsutil = new DataSetUtil(ds)
    if (type === 'node') return dsutil.node(id);
    else if (type === 'way') return dsutil.way(id);
    else if (type === 'relation') return dsutil.relation(id);
}

export function add_railwayref(id, type, railwayref) {
    var name = "Datenebene railway:ref";

    if (!layers.has(name)) {
        var dataLayer = new OsmDataLayer(new DataSet(), name, null);
        layers.add(dataLayer);
    }
    if (layers.has(name)) {
        var layer = layers.get(name);
        var element = getElement(layer.data, type, id);

        if (!element) {
            var ds = Api.downloadObject(id, type);

            layer.mergeFrom(ds);
            element = getElement(layer.data, type, id);
        }

        if (element && !element.hasTag("railway:ref")) {
            buildChangeCommand(element, { tags: { "railway:ref": railwayref } }).applyTo(layer)
        }
    }
}

export function add_node(lat, lon, name, railway, railwayref) {
    var dsname = "Datenebene railway:ref";

    if (!layers.has(dsname)) {
        var dataLayer = new OsmDataLayer(new DataSet(), dsname, null);
        layers.add(dataLayer);
    }
    if (layers.has(dsname)) {
        var layer = layers.get(dsname);
        var nb = new NodeBuilder();

        var node = nb.create({ lat: lat, lon: lon });
        node.tags = { name: name, operator: 'DB Netz AG', railway: railway, 'railway:ref': railwayref };

        buildAddCommand(node).applyTo(layer);
    }
}
