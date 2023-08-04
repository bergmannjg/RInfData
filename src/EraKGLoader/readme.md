# EraKGLoader

Load data from [ERA knowledge graph](https://era-web.linkeddata.es/sparql.html).

## Build

* run: `dotnet build EraKGLoader.fsproj`

## Usage

```txt
USAGE: EraKGLoader --Build <dataDir> <countries>          
    
        load OperationalPoints and SectionsOfLines of countries from knowledge graph 
        and build graph of OperationalPoints and SectionsOfLines

        <dataDir>: directory
        <countries>: list of countries separated by semicolon
```
