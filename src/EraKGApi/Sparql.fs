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

module QueryResults =
    let concat (q1: QueryResults) (q2: QueryResults) =
        { q1 with
            head = if q1.head.vars.Length > 0 then q1.head else q2.head
            results = { bindings = Array.concat [ q1.results.bindings; q2.results.bindings ] } }

    let empty =
        { head = { vars = Array.empty }
          results = { bindings = Array.empty } }

    let length (data: QueryResults) : int = data.results.bindings.Length

    let fold (data: QueryResults[]) : QueryResults =
        Array.fold (fun acc (r: QueryResults) -> concat acc r) empty data

type Item =
    { id: string
      properties: Map<string, obj[]> }

type Microdata = { items: Item[] }

module Microdata =
    let concat (q1: Microdata) (q2: Microdata) (chooser: (Item -> option<Item>)) : Microdata =
        { items = Array.concat [ q1.items; q2.items |> Array.choose chooser ] }

    let empty: Microdata = { items = Array.empty }

    let length (data: Microdata) : int = data.items.Length

    let fold (chooser: (Item -> option<Item>)) (data: Microdata[]) : Microdata =
        Array.fold (fun acc (r: Microdata) -> concat acc r chooser) empty data
