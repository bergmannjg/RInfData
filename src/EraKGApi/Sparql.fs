// see SPARQL Query Results JSON Format https://www.w3.org/TR/sparql11-results-json/
namespace Sparql

open System.Text.Json.Serialization
open FSharp.Collections

type Head = { vars: string[] }

type Rdf =
    { ``type``: string
      datatype: string option
      [<JsonPropertyName("xml:lang")>]
      language: string option
      value: string }

type Results = { bindings: Map<string, Rdf>[] }

type QueryResults = { head: Head; results: Results }

type Item =
    { id: string
      properties: Map<string, obj[]> }

type Microdata = { items: Item[] }
