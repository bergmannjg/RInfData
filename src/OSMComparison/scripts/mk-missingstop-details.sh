#!/usr/bin/env bash
# make missing stop details markdown file from output of "RInfLoader --Compare.Lines"

if [ $# -eq 0 ]; then
    echo "usage: mk-missingstop-details.sh file"
    exit 1
fi

if [ ! -f $1 ]; then
    echo "file $1 not found"
    exit 1
fi

echo "# Missing stop details"
echo ""

COUNT=$(grep -c "missing stop|" $1)
echo "$COUNT entries found"
echo ""

echo "## Reason DistanceToStop"
echo ""

echo "|Line|OPID|Name|Reason for mismatch|OSM Relation of line|"
echo "|---|---|---|---|---:|"

grep "|DistanceToStop" $1 | sed 's/^  missing stop//'
echo ""
COUNT=$(grep -c "|DistanceToStop" $1)
echo "$COUNT entries found"
echo ""

echo "## Reason DistanceToWaysOfLine"
echo ""

echo "|Line|OPID|Name|Reason for mismatch|OSM Relation of line|"
echo "|---|---|---|---|---:|"

grep "|DistanceToWaysOfLine" $1 | sed 's/^  missing stop//'
echo ""
COUNT=$(grep -c "|DistanceToWaysOfLine" $1)
echo "$COUNT entries found"
echo ""

echo "## Reason OsmNotYetMapped"
echo ""

echo "|Line|OPID|Name|Reason for mismatch|OSM Relation of line|"
echo "|---|---|---|---|---:|"

grep "|OsmNotYetMapped" $1 | sed 's/^  missing stop//'
echo ""
COUNT=$(grep -c "|OsmNotYetMapped" $1)
echo "$COUNT entries found"
echo ""

echo "## Reason DisusedStation"
echo ""

echo "|Line|OPID|Name|Reason for mismatch|OSM Relation of line|"
echo "|---|---|---|---|---:|"

grep "|DisusedStation" $1 | sed 's/^  missing stop//'
echo ""
COUNT=$(grep -c "|DisusedStation" $1)
echo "$COUNT entries found"
echo ""

echo "## Reason Unexpected"
echo ""

echo "|Line|OPID|Name|Reason for mismatch|OSM Relation of line|"
echo "|---|---|---|---|---:|"

grep "|Unexpected" $1 | sed 's/^  missing stop//'
echo ""
COUNT=$(grep -c "|Unexpected" $1)
echo "$COUNT entries found"
echo ""
