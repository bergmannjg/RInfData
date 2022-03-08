# analyze graph

import sys
import json
import networkx as nx

G = nx.MultiGraph()

file = '../../rinf-data/Graph-orig.json' if '--orig' in sys.argv else '../../rinf-data/Graph.json'

with open(file, 'r') as f:
    data = json.load(f)

for n in data:
    for e in n['Edges']:
        G.add_edge(n['Node'], e['Node'], key=e['Line'],
                   weight=e['Cost'], line=e['Line'])

print('info: ', nx.info(G))

show_shortest_path = len(sys.argv) >= 4 and sys.argv[1] == '--shortest_path'

if show_shortest_path:
    path = nx.shortest_path(G, sys.argv[2], sys.argv[3], 'weight')

    print('path: ', path)

show_degree = len(sys.argv) >= 2 and sys.argv[1] == '--degree'

if show_degree:
    print('degree_histogram: ', nx.degree_histogram(G))

    for n, d in G.degree():
        if d > 10:
            print(n, d)


def filter_line(line: str):
    return lambda n1, n2, k: k == line


def get_line_graph(line: str):
    g = nx.subgraph_view(G,  filter_edge=filter_line(line))
    return nx.MultiGraph(incoming_graph_data=g.edges(), multigraph_input=True)


def get_line_info(line: str):
    lg = get_line_graph(line)
    degree_histogram = nx.degree_histogram(lg)
    return {'line': int(line),
            'edges': len(lg.edges()),
            'nodes': len(lg.nodes()),
            'is_connected': nx.is_connected(lg),
            'is_path_graph': len(degree_histogram) in [2, 3],
            'degree_histogram': degree_histogram,
            'high_degree_nodes': [(n, d) for n, d in lg.degree() if d > 2]}


def count_elements(seq) -> dict:
    hist = {}
    for i in seq:
        hist[i] = hist.get(i, 0) + 1
    return hist


show_lineinfos = len(sys.argv) >= 3 and sys.argv[1] == '--lineinfos'

if show_lineinfos:
    lines = sorted(list(set([k for u, v, k in G.edges(keys=True)])))

    lineinfos = [get_line_info(line) for line in lines]

    out_file = open(sys.argv[2], "w")
    json.dump(lineinfos, out_file)


show_degree_of_line = len(sys.argv) >= 3 and sys.argv[1] == '--degree_of_line'

if show_degree_of_line:
    lg = nx.subgraph_view(G, filter_edge=filter_line(sys.argv[2]))
    print('degree_histogram: ', nx.degree_histogram(lg))

    for n, d in lg.degree():
        if d > 2:
            print(n, d)
