// Learn more about F# at http://fsharp.org

open System
open System.IO
open Microsoft.Extensions.Configuration
open Gremlin.Net.Driver
open Gremlin.Net.Structure.IO.GraphSON
open System.Collections.Generic

type AzureCosmosDbConfiguration =
    {
        Hostname: string
        Port: int
        AuthKey: string
        Username: string
        GraphName: string
    }

// Configuration
let getConfiguration() =
    let configurationBuilder = new ConfigurationBuilder()
    configurationBuilder
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build()

let readAzureCosmosDbConfiguration (configuration: IConfigurationRoot) =
    let username = "/dbs/" + configuration.["AzureCosmosDb:DatabaseName"] + "/colls/" + configuration.["AzureCosmosDb:GraphName"]

    {
        Hostname = configuration.["AzureCosmosDb:Hostname"]
        Port = (configuration.["AzureCosmosDb:Port"] |> int)
        AuthKey = configuration.["AzureCosmosDb:AuthKey"]
        Username = username
        GraphName = configuration.["AzureCosmosDb:GraphName"]
    }

// Azure Cosmos DB
let createServer (configuration: AzureCosmosDbConfiguration) =
    new GremlinServer(configuration.Hostname, configuration.Port, true, configuration.Username, configuration.AuthKey)

let createClient server =
    new GremlinClient(server, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType)

let runQueryWithResults<'T> (client: GremlinClient) (query: string, parameters: Map<string, string>) = async {
    Console.WriteLine(query)
    Console.WriteLine(parameters.ToString())

    let param = parameters |> Map.map (fun _ v -> v :> obj) |> Dictionary

    return! client.SubmitAsync<'T>(query, param)
            |> Async.AwaitTask
            |> (fun asyncResult -> async {
                let! seq = asyncResult
                return seq |> List.ofSeq
            })
}

let runQuery client = runQueryWithResults<obj> client >> Async.Ignore

let asyncMain() = async {
    let azureCosmosDbConfiguration = getConfiguration() |> readAzureCosmosDbConfiguration
    let server = createServer azureCosmosDbConfiguration
    let client = createClient server
    let runQueryFunc = runQuery client

    let query = "g.addV('person').property('id', id).property('firstName', firstName).property('age', age)"
    let parameters = Map.ofList [
                        ("id", "thomas");
                        ("firstName", "Thomas");
                        ("age", "44")
                    ]

    do! runQueryFunc (query, parameters)

    Console.WriteLine("Done")

    return ()
}

[<EntryPoint>]
let main argv =
    asyncMain() |> Async.RunSynchronously
    0 // return an integer exit code
