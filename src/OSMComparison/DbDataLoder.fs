// data from Open-Data-Portal of Deutsche Bahn to map UIC station codes to RInf OPIDs
// url: https://data.deutschebahn.com/dataset/data-haltestellen.html

namespace DB

type UicRefMapping =
    { EVA_NR: int
      DS100: string
      IFOPT: string }

module DBLoader =

    open System.IO
    open System.Text.Json

    let private readFile<'a> path name =
        JsonSerializer.Deserialize<'a>(File.ReadAllText(path + name))

    let private fixErrors (m: UicRefMapping) =
        if m.DS100 = "EBILP" then
            { m with DS100 = "EBIL" } // according to DB Netze Infrastrukturregister
        else
            m

    let loadMappings (dbDataDir) =
        readFile<UicRefMapping []> dbDataDir "D_Bahnhof_2020_alle.json"
        |> Array.map fixErrors
