namespace RInf

open System.Text.Json.Serialization

type Token =
    { access_token: string
      token_type: string
      expires_in: int }

module internal Request =

    open System.Text.Json
    open System.Net
    open System.Collections.Generic

    type HttpClient(username, password) =

        let baseurl = "https://rinf.era.europa.eu/api/"
        let mutable token: Token option = None

        let createHandler () =
            let handler = new Http.HttpClientHandler()
            handler.AutomaticDecompression <- DecompressionMethods.GZip
            handler

        let client = new Http.HttpClient(createHandler ())

        let getToken () =
            async {
                if token.IsSome then
                    return token.Value
                else
                    let url = baseurl + "token"

                    let formVals =
                        [ "grant_type", "password"
                          "username", username
                          "password", password ]
                        |> List.map (fun (x, y) -> new KeyValuePair<string, string>(x, y))

                    let content = new Http.FormUrlEncodedContent(formVals)

                    let! response = client.PostAsync(url, content) |> Async.AwaitTask

                    let! body =
                        response.Content.ReadAsStringAsync()
                        |> Async.AwaitTask

                    return
                        match response.IsSuccessStatusCode with
                        | true -> JsonSerializer.Deserialize<Token> body
                        | false -> raise (System.Exception("token failed"))
            }

        member __.Dispose() = client.Dispose()

        member __.Get(method: string, ?withMetadata: bool) =
            async {

                let! token = getToken ()

                let url = baseurl + method

                fprintfn stderr "url: %s" url

                if not (client.DefaultRequestHeaders.Contains("Authorization")) then
                    client.DefaultRequestHeaders.Add("Authorization", token.token_type + " " + token.access_token)

                if defaultArg withMetadata false then
                    if not (client.DefaultRequestHeaders.Contains("Accept")) then
                        client.DefaultRequestHeaders.Add("Accept", "application/json;odata.metadata=full")

                let! response = client.GetAsync(url) |> Async.AwaitTask

                let! body =
                    response.Content.ReadAsStringAsync()
                    |> Async.AwaitTask

                return
                    match response.IsSuccessStatusCode with
                    | true -> body
                    | false -> raise (System.InvalidOperationException(body))
            }
