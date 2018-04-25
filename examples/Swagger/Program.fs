module SampleApp.App

open System
open System.Net

open Suave
open Suave.Sockets.Control
open Suave.Logging
open Suave.Operators
open Suave.EventSource
open Suave.Filters
open Suave.Writers
open Suave.Files
open Suave.Successful
open Suave.State.CookieStateStore

let app =
  choose [
    GET >=> choose
      [ path "/hello" >=> OK "Hello GET" ; path "/goodbye" >=> OK "Good bye GET" ];
    POST >=> choose
      [ path "/hello" >=> OK "Hello POST" ; path "/goodbye" >=> OK "Good bye POST" ];
    DELETE >=> choose
      [ path "/hello" >=> OK "Hello DELETE" ; path "/goodbye" >=> OK "Good bye DELETE" ];
    PUT >=> choose
      [ path "/hello" >=> OK "Hello PUT" ; path "/goodbye" >=> OK "Good bye PUT" ];
  ]

let loggingOptions =
  { Literate.LiterateOptions.create() with
      getLogLevelText = function Verbose->"V" | Debug->"D" | Info->"I" | Warn->"W" | Error->"E" | Fatal->"F" }

let logger = LiterateConsoleTarget(
                name = [|"Suave";"Examples";"Example"|],
                minLevel = Verbose,
                options = loggingOptions,
                outputTemplate = "[{level}] {timestampUtc:o} {message} [{source}]{exceptions}"
              ) :> Logger

[<EntryPoint>]
let main argv =
  startWebServer
    { bindings              = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082
                              ]
      serverKey             = Utils.Crypto.generateKey HttpRuntime.ServerKeyLength
      errorHandler          = defaultErrorHandler
      listenTimeout         = TimeSpan.FromMilliseconds 2000.
      cancellationToken     = Async.DefaultCancellationToken
      bufferSize            = 2048
      maxOps                = 100
      autoGrow              = true
      mimeTypesMap          = Writers.defaultMimeTypesMap
      homeFolder            = None
      compressedFilesFolder = None
      logger                = logger
      tcpServerFactory      = new DefaultTcpServerFactory()
      cookieSerialiser      = new BinaryFormatterSerialiser()
      tlsProvider           = new DefaultTlsProvider()
      hideHeader            = false
      maxContentLength      = 1000000 }
    app
  0

