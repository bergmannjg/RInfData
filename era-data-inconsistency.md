# ERA data inconsistency

The following Sparql queries give

* operational points and kilometric posts of line 1733
* sections of line 1733 and kilometers of end operational point.

There is inconsistent data (loaded at May 2026).

* The last kilometric post of the operational points is **362**.
* The last kilometer of the sections is **147**.

## Operational points and kilometric posts of line 1733 

See live at [https://rinf.data.era.europa.eu/](https://rinf.data.era.europa.eu/endpoint#query=PREFIX%20rdfs%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2000%2F01%2Frdf-schema%23%3E%0APREFIX%20era%3A%20%3Chttp%3A%2F%2Fdata.europa.eu%2F949%2F%3E%0APREFIX%20wgs%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2003%2F01%2Fgeo%2Fwgs84_pos%23%3E%20%0APREFIX%20xsd%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2001%2FXMLSchema%23%3E%0APREFIX%20time%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2006%2Ftime%23%3E%0A%0ASELECT%20distinct%20%3FoperationalPoint%20%3FopName%20%3Fuopid%20%3FlineId%20%3Fkilometer%20%3Foffset%0AWHERE%20%7B%0A%20%20%20%20%3FoperationalPoint%20a%20era%3AOperationalPoint%20.%0A%20%20%20%20%3FoperationalPoint%20era%3AopName%20%3FopName%20.%0A%20%20%20%20%3FoperationalPoint%20era%3Auopid%20%3Fuopid%20.%0A%20%20%20%20%3FoperationalPoint%20era%3AnetReference%20%3FnetReference%20.%0A%20%20%20%20%3FnetReference%20era%3AhasLrsCoordinate%20%3FlrsCoordinate%20.%0A%20%20%20%20%3FlrsCoordinate%20era%3AkmPost%20%3FkmPost%20.%0A%20%20%20%20%3FlrsCoordinate%20era%3AoffsetFromKilometricPost%20%3Foffset.%0A%20%20%20%20%3FkmPost%20era%3Akilometer%20%3Fkilometer%20.%0A%20%20%20%20%3FkmPost%20era%3AhasLRS%20%3Flrs%20.%0A%20%20%20%20%3Flrs%20era%3AlineId%20%3FlineId%20.%20%0A%20%20%20%20%3Flrs%20era%3AlineId%20%221733%22%20.%0A%20%20%20%20%3FoperationalPoint%20era%3Avalidity%20%3Fvalidity%20.%0A%20%20%20%20%3Fvalidity%20time%3AhasBeginning%20%3FtimeBeginning%20.%0A%20%20%20%20%3FtimeBeginning%20time%3AinXSDDate%20%3FxsdTimeBeginning%20.%0A%20%20%20%20%3Fvalidity%20time%3AhasEnd%20%3FtimeEnd%20.%0A%20%20%20%20%3FtimeEnd%20time%3AinXSDDate%20%3FxsdTimeEnd%20.%0A%20%20%20%20BIND(xsd%3AdateTime(NOW())%20AS%20%3Fnow)%20.%0A%20%20%20%20Filter(%3FxsdTimeBeginning%20%3C%3D%20%3Fnow%20%26%26%20%3Fnow%20%3C%3D%20%3FxsdTimeEnd)%20.%0A%20%20%20%20%3FoperationalPoint%20era%3AinCountry%20%3Chttp%3A%2F%2Fpublications.europa.eu%2Fresource%2Fauthority%2Fcountry%2FDEU%3E%20.%20%0A%7D%20ORDER%20BY%20%3Fkilometer%20%3Foffset%20LIMIT%20100%0A&endpoint=https%3A%2F%2Frinf.data.era.europa.eu%2Fapi%2Fv1%2Fsparql%2Frinf&requestMethod=POST&tabTitle=Query&headers=%7B%7D&contentTypeConstruct=application%2Fn-triples%2C*%2F*%3Bq%3D0.9&contentTypeSelect=application%2Fsparql-results%2Bjson%2C*%2F*%3Bq%3D0.9&outputFormat=table).

```
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX era: <http://data.europa.eu/949/>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdf:	<http://www.w3.org/1999/02/22-rdf-syntax-ns#>

SELECT distinct ?operationalPoint ?opName ?uopid ?lineId ?kilometer ?offset 
WHERE {
    ?operationalPoint a era:OperationalPoint .
    ?operationalPoint era:opName ?opName .
    ?operationalPoint era:uopid ?uopid .
    ?operationalPoint era:netReference ?netReference .
    ?netReference rdf:type era:NetPointReference .
    ?netReference era:hasLrsCoordinate ?lrsCoordinate .
    ?lrsCoordinate era:kmPost ?kmPost .
    ?lrsCoordinate era:offsetFromKilometricPost ?offset.
    ?kmPost era:kilometer ?kilometer .
    ?kmPost era:hasLRS ?lrs .
    ?lrs era:lineId ?lineId . 
    ?lrs era:lineId "1733" .
    ?operationalPoint era:validity ?validity .
    ?validity time:hasBeginning ?timeBeginning .
    ?timeBeginning time:inXSDDate ?xsdTimeBeginning .
    ?validity time:hasEnd ?timeEnd .
    ?timeEnd time:inXSDDate ?xsdTimeEnd .
    BIND(xsd:dateTime(NOW()) AS ?now) .
    Filter(?xsdTimeBeginning <= ?now && ?now <= ?xsdTimeEnd) .
    ?operationalPoint era:inCountry <http://publications.europa.eu/resource/authority/country/DEU> . 
} ORDER BY ?kilometer ?offset LIMIT 100
```

## Sections of line 1733 and end kilometers 

See live at [https://rinf.data.era.europa.eu/](https://rinf.data.era.europa.eu/endpoint#query=PREFIX%20era%3A%20%3Chttp%3A%2F%2Fdata.europa.eu%2F949%2F%3E%0APREFIX%20wgs%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2003%2F01%2Fgeo%2Fwgs84_pos%23%3E%20%0APREFIX%20xsd%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2001%2FXMLSchema%23%3E%0APREFIX%20time%3A%20%3Chttp%3A%2F%2Fwww.w3.org%2F2006%2Ftime%23%3E%0APREFIX%20rdf%3A%09%3Chttp%3A%2F%2Fwww.w3.org%2F1999%2F02%2F22-rdf-syntax-ns%23%3E%0A%0ASELECT%20distinct%20%3FsectionOfLine%20%3FlineId%20%3FstartUopid%20%20%3FendUopid%20%3Fkilometer%20%3Foffset%0AWHERE%20%7B%0A%20%20%20%20%3FsectionOfLine%20a%20era%3ASectionOfLine%20.%0A%20%20%20%20%3FsectionOfLine%20era%3AnationalLine%20%3FnationalLine%20.%0A%20%20%20%20%3FnationalLine%20era%3AlineId%20%3FlineId%20.%0A%20%20%20%20%3FnationalLine%20era%3AlineId%20%221733%22%20.%0A%20%20%20%20%3FsectionOfLine%20era%3AnetReference%20%3FnetReference%20.%0A%09%3FnetReference%20rdf%3Atype%20era%3ANetLinearReference%20.%0A%20%20%20%20%3FnetReference%20era%3AendsAt%20%3FendsAt%20.%0A%20%20%20%20%3FendsAt%20era%3AhasLrsCoordinate%20%3FlrsCoordinate%20.%0A%20%20%20%20%3FlrsCoordinate%20era%3AkmPost%20%3FkmPost%20.%0A%20%20%20%20%3FlrsCoordinate%20era%3AoffsetFromKilometricPost%20%3Foffset%20.%0A%20%20%20%20%3FkmPost%20era%3Akilometer%20%3Fkilometer%20.%0A%20%20%20%20%3FsectionOfLine%20era%3AopStart%20%3FstartOp%20.%0A%20%20%20%20%3FstartOp%20era%3Auopid%20%3FstartUopid%20.%0A%20%20%20%20%3FsectionOfLine%20era%3AopEnd%20%3FendOp%20.%0A%20%20%20%20%3FendOp%20era%3Auopid%20%3FendUopid%20.%0A%20%20%20%20%3FsectionOfLine%20era%3Avalidity%20%3Fvalidity%20.%0A%20%20%20%20%3Fvalidity%20time%3AhasBeginning%20%3FtimeBeginning%20.%0A%20%20%20%20%3FtimeBeginning%20time%3AinXSDDate%20%3FxsdTimeBeginning%20.%0A%20%20%20%20%3Fvalidity%20time%3AhasEnd%20%3FtimeEnd%20.%0A%20%20%20%20%3FtimeEnd%20time%3AinXSDDate%20%3FxsdTimeEnd%20.%0A%20%20%20%20BIND(xsd%3AdateTime(NOW())%20AS%20%3Fnow)%20.%0A%20%20%20%20Filter(%3FxsdTimeBeginning%20%3C%3D%20%3Fnow%20%26%26%20%3Fnow%20%3C%3D%20%3FxsdTimeEnd)%20.%0A%20%20%20%20%3FsectionOfLine%20era%3AinCountry%20%3Chttp%3A%2F%2Fpublications.europa.eu%2Fresource%2Fauthority%2Fcountry%2FDEU%3E%20.%20%0A%7D%20ORDER%20BY%20%3Fkilometer%20%3Foffset%20LIMIT%20100&endpoint=https%3A%2F%2Frinf.data.era.europa.eu%2Fapi%2Fv1%2Fsparql%2Frinf&requestMethod=POST&tabTitle=Query%201&headers=%7B%7D&contentTypeConstruct=application%2Fn-triples%2C*%2F*%3Bq%3D0.9&contentTypeSelect=application%2Fsparql-results%2Bjson%2C*%2F*%3Bq%3D0.9&outputFormat=table).

```
PREFIX era: <http://data.europa.eu/949/>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdf:	<http://www.w3.org/1999/02/22-rdf-syntax-ns#>

SELECT distinct ?sectionOfLine ?lineId ?startUopid  ?endUopid ?kilometer ?offset
WHERE {
    ?sectionOfLine a era:SectionOfLine .
    ?sectionOfLine era:nationalLine ?nationalLine .
    ?nationalLine era:lineId ?lineId .
    ?nationalLine era:lineId "1733" .
    ?sectionOfLine era:netReference ?netReference .
	?netReference rdf:type era:NetLinearReference .
    ?netReference era:endsAt ?endsAt .
    ?endsAt era:hasLrsCoordinate ?lrsCoordinate .
    ?lrsCoordinate era:kmPost ?kmPost .
    ?lrsCoordinate era:offsetFromKilometricPost ?offset .
    ?kmPost era:kilometer ?kilometer .
    ?sectionOfLine era:opStart ?startOp .
    ?startOp era:uopid ?startUopid .
    ?sectionOfLine era:opEnd ?endOp .
    ?endOp era:uopid ?endUopid .
    ?sectionOfLine era:validity ?validity .
    ?validity time:hasBeginning ?timeBeginning .
    ?timeBeginning time:inXSDDate ?xsdTimeBeginning .
    ?validity time:hasEnd ?timeEnd .
    ?timeEnd time:inXSDDate ?xsdTimeEnd .
    BIND(xsd:dateTime(NOW()) AS ?now) .
    Filter(?xsdTimeBeginning <= ?now && ?now <= ?xsdTimeEnd) .
    ?sectionOfLine era:inCountry <http://publications.europa.eu/resource/authority/country/DEU> . 
} ORDER BY ?kilometer ?offset LIMIT 100
```
