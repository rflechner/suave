module Giraffe.SwaggerUi

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open Suave
open Suave.Operators
open Suave.Filters
open Suave.Writers
open Suave.Sockets.Control
open Suave.Successful
open Suave.Logging
open Suave.EventSource
open Suave.Files
open Suave.State.CookieStateStore
open Suave.Sockets.AsyncSocket
open Suave.Filters
open Suave.RequestErrors
open System.Xml.Serialization
open Suave.Sockets

let combineUrls (u1:string) (u2:string) =
    let sp = if u2.StartsWith "/" then u2.Substring 1 else u2
    u1 + sp

let swaggerUiWebPart (swPath:string) (swJsonPath:string) =
    let wp : WebPart =
      fun ctx ->
        let p =
          match ctx.request.url.AbsolutePath.Substring(swPath.Length) with
          | v when String.IsNullOrWhiteSpace v -> "index.html"
          | v -> v

        let streamZipContent () =
          let assembly = System.Reflection.Assembly.GetExecutingAssembly()
          let fs = assembly.GetManifestResourceStream "swagger-ui.zip"
          
          let zip = new ZipArchive(fs)
          match zip.Entries |> Seq.tryFind (fun e -> e.FullName = p) with
          | Some ze ->
            let headers =
              match defaultMimeTypesMap (System.IO.Path.GetExtension p) with
              | Some mimetype -> ("Content-Type", mimetype.name) :: ctx.response.headers
              | None -> ctx.response.headers
            let write (conn, _) =
              socket {
                  let! (_, conn) = asyncWriteLn (sprintf "Content-Length: %d\r\n" ze.Length) conn 
                  let! conn = flush conn
                  do! transferStream conn (ze.Open())
                  return conn
              }
            if p = "index.html"
            then
              use r = new StreamReader(ze.Open())
              let bytes =
                r.ReadToEnd()
                  .Replace("http://petstore.swagger.io/v2/swagger.json", (combineUrls "/" swJsonPath))
              |> r.CurrentEncoding.GetBytes
              { ctx
                  with
                    response =
                      { ctx.response
                          with
                            status = { ctx.response.status with code = 200 }
                            content = Bytes bytes
                            headers = headers
                      }
              } |> succeed
            else
              { ctx with
                  response =
                    { ctx.response with
                        status = { ctx.response.status with code = 200 }
                        content = SocketTask write
                        headers = headers
                    }
              }
              |> succeed
          | None ->
              ctx |> NOT_FOUND "Ressource not found"
        streamZipContent()
    pathStarts swPath >=> wp
