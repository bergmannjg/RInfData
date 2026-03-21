# Graph Library

Find the shortest path for operational points (see [Multiobjective Dijkstra Algorithm](../TargetedMosp))

The cost function is based on two criteria:
* the number of lines and
* the estimated traveltime (length of section of line multiplied by max speed).

The algorithm may generate multiple paths
one of whiich with the fewest line and one with the shortest traveltime.

The path actually taken by a train may be slightly different.
