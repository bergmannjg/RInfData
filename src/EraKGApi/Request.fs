namespace EraKG

module Request =

    open System.Net

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

            let url =
                baseurl
                + System.Web.HttpUtility.UrlEncode(query)

            let! response = client.GetAsync(url) |> Async.AwaitTask

            let! body =
                response.Content.ReadAsStringAsync()
                |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false -> raise (System.InvalidOperationException(body))
        }
