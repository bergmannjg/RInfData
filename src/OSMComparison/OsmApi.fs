namespace OSM

type Element =
    { ``type``: string
      id: int64
      lat: float option
      lon: float option
      timestamp: string
      version: int32
      changeset: int64
      user: string
      tags: Map<string, string> option }

type OsmJson = { elements: Element[] }

module Api =

    open System.Net
    open System.Text.Json
    open OSM.Sparql

    let applicationJson = "application/json"

    let private GetAsync (url: string) : Async<OsmJson option> =
        async {

            let client = new Http.HttpClient()
            client.DefaultRequestHeaders.Add("Accept", applicationJson)

            let! response = client.GetAsync(url) |> Async.AwaitTask

            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> Some(JsonSerializer.Deserialize<OsmJson>(body))
                | false -> None
        }

    let private endpoint = "https://api.openstreetmap.org/api/0.6/"

    let loadOsmData (``type``: string) (id: string) : Async<OsmJson option> =
        GetAsync(endpoint + ``type`` + "/" + id)

    let private maybeGetTag (e: Element) (key: string) =
        match e.tags with
        | Some tags -> if tags.ContainsKey key then Some tags.[key] else None
        | None -> None

    let private getTag (e: Element) (key: string) =
        match maybeGetTag e key with
        | Some v -> v
        | None -> ""

    let ToEntries (json: OsmJson) : Entry[] =
        json.elements
        |> Array.map (fun e ->
            { Stop = ""
              Name = getTag e "name"
              PublicTransport = maybeGetTag e "public_transport"
              Railway = maybeGetTag e "railway"
              RailwayRef = maybeGetTag e "railway:ref"
              UicRef = maybeGetTag e "uic_ref"
              Operator = None
              Latitude = e.lat |> Option.defaultValue 0.0
              Longitude = e.lon |> Option.defaultValue 0.0
              OsmType = e.``type`` })
