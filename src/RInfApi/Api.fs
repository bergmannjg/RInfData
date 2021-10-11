/// see https://rinf.era.europa.eu/API/Help
/// see https://www.era.europa.eu/registers_en#rinf
namespace RInf

open System
open System.Text.Json.Serialization

type OData<'a> =
    { [<JsonPropertyName("@odata.context")>]
      context: string
      [<JsonPropertyName("@odata.nextLink")>]
      nextLink: string
      value: 'a }

type DatasetImport =
    { Code: string
      Name: string
      Version: int
      LastImportDate: string }

type SOLTrackParameter =
    { TrackID: int
      VersionID: int
      ParameterID: int
      Value: string
      OptionalValue: string
      IsApplicable: string
      ID: string
      Identity: int }

type SOLTunnel =
    { SOLTunnelIMCode: string
      SOLTunnelIdentification: string
      StartLong: float
      StartLat: float
      StartKm: float
      EndLong: float
      EndLat: float
      EndKm: float
      ITU_Length: float }

type SOLTrack =
    { SOLTrackIdentification: string
      SOLTrackDirection: string
      SectionOfLineID: int
      VersionID: int
      TrackID: int
      SOLTrackParameters: SOLTrackParameter []
      SOLTunnels: SOLTunnel [] }

type RailwayLocation =
    { Id: int
      NationalIdentNum: string
      Kilometer: float }

type SectionOfLine =
    { ID: int
      VersionID: int
      solName: string
      Country: string
      ValidityDateStart: System.DateTime
      ValidityDateEnd: System.DateTime
      Length: float
      Nature: string
      LineIdentification: string
      IMCode: string
      OPStartID: int
      OPEndID: int
      SOLTracks: SOLTrack [] }

type OPTrackParameter =
    { ID: string
      Value: string
      OptionalValue: string
      IsApplicable: string }

type OPTrack =
    { OPTrackIdentification: string
      OPTrackIMCode: string
      OperationalPointID: int
      VersionID: int
      TrackID: int
      OPTrackParameters: OPTrackParameter [] }

type OperationalPoint =
    { ID: int
      VersionID: int
      Name: string
      Type: string
      Country: string
      ValidityDateStart: System.DateTime
      ValidityDateEnd: System.DateTime
      Latitude: float
      Longitude: float
      UOPID: string
      RailwayLocations: RailwayLocation []
      OPTracks: OPTrack [] }

type Node =
    { OperationalPoint: OperationalPoint
      Name: string
      Type: string
      Country: string
      Latitude: float
      Longitude: float
      UOPID: string }

type NodeContainer = { Node: Node }

type Edge =
    { SectionOfLine: SectionOfLine
      Length: float
      Nature: string }

type Route =
    { Nodes: NodeContainer []
      Edges: Edge [] }

module Api =

    open System.Text.Json

    type Client(username, password, country) =

        let client = Request.HttpClient(username, password)

        let expandSOLTrackParameters (withTrackParameters: bool option) (prefix: string) =
            if defaultArg withTrackParameters false then
                prefix + "$expand=SOLTracks/SOLTrackParameters"
            else
                ""

        let expandOPTrackParameters (withTrackParameters: bool option) (prefix: string) =
            if defaultArg withTrackParameters false then
                prefix
                + "$expand=OPTracks($expand=OPTrackParameters)"
            else
                ""

        let getAllData (getData: int -> Async<'a []>) : Async<'a []> =
            let rec loop skip (results: array<'a> list) =
                async {
                    let! result = getData skip

                    if result.Length > 0 then
                        return! loop (skip + 100) (result :: results)
                    else
                        return results
                }

            async {
                let! results = loop 0 List.empty

                return results |> List.toArray |> Array.concat
            }

        interface IDisposable with
            member __.Dispose() = client.Dispose()

        member __.GetDatasetImports() =
            async {
                let! response = client.Get("DatasetImports")

                let imports =
                    JsonSerializer.Deserialize<OData<DatasetImport []>> response

                return imports.value
            }

        member __.GetNextSectionsOfLines(skip: int) =
            async {
                let! response =
                    client.Get(
                        "SectionsOfLine?$filter=Country eq '"
                        + country
                        + "'&$top=100&$skip="
                        + skip.ToString()
                    )

                return
                    (JsonSerializer.Deserialize<OData<SectionOfLine []>> response)
                        .value
            }

        member __.GetSectionsOfLines() = getAllData __.GetNextSectionsOfLines

        member __.GetSectionsOfLine(keyID: int, keyVersionID: int, ?withTrackParameters: bool, ?logJson: bool) =
            async {
                let expand =
                    expandSOLTrackParameters withTrackParameters "?"

                let! response =
                    client.Get(
                        "SectionsOfLine("
                        + keyID.ToString()
                        + ","
                        + keyVersionID.ToString()
                        + ")"
                        + expand
                    )

                if defaultArg logJson false then
                    printfn "%s" response

                return JsonSerializer.Deserialize<SectionOfLine> response
            }

        member __.GetNextOperationalPoints(skip: int) =
            async {
                let expand = expandOPTrackParameters (Some true) "&"

                let! response =
                    client.Get(
                        "OperationalPoints?$filter=Country eq '"
                        + country
                        + "'&$top=100&$skip="
                        + skip.ToString()
                        + expand
                    )

                return
                    (JsonSerializer.Deserialize<OData<OperationalPoint []>> response)
                        .value
            }

        member __.GetOperationalPoints() = getAllData __.GetNextOperationalPoints

        member __.GetOperationalPoint(keyID: int, keyVersionID: int, ?withTrackParameters: bool, ?logJson: bool) =
            async {
                let expand =
                    expandOPTrackParameters withTrackParameters "?"

                let! response =
                    client.Get(
                        "OperationalPoints("
                        + keyID.ToString()
                        + ","
                        + keyVersionID.ToString()
                        + ")"
                        + expand
                    )

                if defaultArg logJson false then
                    printfn "%s" response

                return JsonSerializer.Deserialize<OperationalPoint> response
            }

        member __.GetRoutes(from: string, ``to``: string, ?logJson: bool) =
            async {
                let! response = client.Get("Routes/From/" + from + "/To/" + ``to``)

                if defaultArg logJson false then
                    printfn "%s" response

                return
                    if response = "\"Sequence contains no matching element\"" then
                        { Nodes = [||]; Edges = [||] }
                    else
                        JsonSerializer.Deserialize<Route> response
            }
