namespace EraKG

module Request =

    open System.Net

    let applicationQleverResults = "application/qlever-results+json"

    let applicationSparqlResults = "application/sparql-results+json"

    let applicationMicrodata = "application/microdata+json"

    let GetAsync (endpoint: string) (query: string) (format: string) : Async<string> =
        async {
            let baseurl = endpoint + "?query="

            let createHandler () =
                let handler = new Http.HttpClientHandler()
                handler.AutomaticDecompression <- DecompressionMethods.GZip
                handler

            let client = new Http.HttpClient(createHandler ())
            client.DefaultRequestHeaders.Add("Accept", format)

            let url = baseurl + System.Web.HttpUtility.UrlEncode(query)

            let! response = client.GetAsync(url) |> Async.AwaitTask

            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false ->
                    fprintfn stderr $"query {query}"
                    raise (System.InvalidOperationException(body))
        }

    let PostAsync (endpoint: string) (query: string) (format: string) : Async<string> =
        async {
            let url = endpoint

            let createHandler () =
                let handler = new Http.HttpClientHandler()
                handler.AutomaticDecompression <- DecompressionMethods.GZip
                handler

            let client = new Http.HttpClient(createHandler ())
            client.DefaultRequestHeaders.Add("Accept", format)

            let dict = new System.Collections.Generic.Dictionary<string, string>()
            dict.Add("query", query)

            use content = new Http.FormUrlEncodedContent(dict)

            let! response = client.PostAsync(url, content) |> Async.AwaitTask

            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false -> raise (System.InvalidOperationException(body))
        }
