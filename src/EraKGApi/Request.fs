namespace EraKG

module Request =

    open System.Net

    let applicationSparqlResults = "application/sparql-results+json"

    let private client =
        let handler =
            new Http.SocketsHttpHandler(PooledConnectionLifetime = System.TimeSpan.FromMinutes(int64 5))

        handler.AutomaticDecompression <- DecompressionMethods.GZip
        let client = new Http.HttpClient(handler)
        client.DefaultRequestHeaders.Add("Accept", applicationSparqlResults)
        client

    let rec private retry (work: Async<'a>) resultOk (retries: int) (delay: int) =
        async {
            match! Async.Catch work with
            | Choice1Of2 res when resultOk res -> return res
            | _ ->
                if retries = 0 then
                    raise (System.InvalidOperationException "Request: execute retry, max retries reached")

                fprintfn stderr $"Request: execute retry {retries} delay {delay}"
                System.Threading.Thread.Sleep delay
                return! retry work resultOk (retries - 1) (delay * 10)
        }

    let GetAsync (endpoint: string) (query: string) : Async<string> =
        async {
            let baseurl = endpoint + "?query="

            let url = baseurl + System.Web.HttpUtility.UrlEncode(query)

            let! response = retry (client.GetAsync url |> Async.AwaitTask) _.IsSuccessStatusCode 3 10

            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false -> raise (System.InvalidOperationException(response.ToString()))
        }

    let PostAsync (endpoint: string) (query: string) : Async<string> =
        async {
            let url = endpoint

            let dict = new System.Collections.Generic.Dictionary<string, string>()
            dict.Add("query", query)

            use content = new Http.FormUrlEncodedContent(dict)

            use! response = retry (client.PostAsync(url, content) |> Async.AwaitTask) _.IsSuccessStatusCode 3 10

            let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false -> raise (System.InvalidOperationException(response.ToString()))
        }
