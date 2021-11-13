namespace OSM

module OverpassRequest =

    open System.Net

    // see http://overpass-api.de/command_line.html
    let private baseurl = "https://overpass-api.de/api/"

    let private createHandler () =
        let handler = new Http.HttpClientHandler()
        handler.AutomaticDecompression <- DecompressionMethods.GZip
        handler

    let getClient () = new Http.HttpClient(createHandler ())

    let private checkAvailable (input: string) =
        let m = System.Text.RegularExpressions.Regex.Match(input, "in ([0-9]+) seconds")

        if m.Success then
            int m.Groups.[1].Value
        else
            raise (System.InvalidOperationException("match failed: " + input))

    let private checkAvailability (client: Http.HttpClient) =
        async {

            let! response =
                client.GetAsync(baseurl + "/status")
                |> Async.AwaitTask

            let! body =
                response.Content.ReadAsStringAsync()
                |> Async.AwaitTask

            if not (body.Contains "slots available") then
                let timeout = checkAvailable body
                printfn "sleeping ... at %A for %d seconds" System.DateTime.Now timeout
                do! Async.Sleep(timeout * 1000)
        }

    let exec (client: Http.HttpClient) (query: string) =

        async {

            do! checkAvailability client

            let url = baseurl + "interpreter?data=" + query

            let! response = client.GetAsync(url) |> Async.AwaitTask

            let! body =
                response.Content.ReadAsStringAsync()
                |> Async.AwaitTask

            return
                match response.IsSuccessStatusCode with
                | true -> body
                | false -> raise (System.InvalidOperationException(response.StatusCode.ToString()))
        }
