namespace Suave.Swagger

open System
open System.Linq.Expressions
open System.Reflection
open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open Quotations.ExprShape
open Quotations.Patterns
open Microsoft.FSharp.Reflection
open FSharp.Quotations.Evaluator
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Common
open Generator
open Analyzer
open System.Text

module Dsl =
// used to facilitate quotation expression analysis
  //let (==>) = (>=>)
  
  let operationId (opId:string) next = next
  let consumes (modelType:Type) next = next
  let produces (modelType:Type) next = next
  
  let definitionOfType (t:Type) =
    t.Describes()
  
  let swaggerDoc docCtx addendums (description:ApiDescription -> ApiDescription) schemes host basePath =
    let rawJson (str : string) : WebPart =
        fun ctx ->
          let bytes = Encoding.UTF8.GetBytes str
          { ctx
              with
                response =
                  { ctx.response
                      with
                        status = { ctx.response.status with code = 200 }
                        content = Bytes bytes
                        headers = ("Content-Type", "application/json") :: ctx.response.headers
                  }
          } |> succeed
    let paths = documentRoutes docCtx.Routes addendums
    let definitions = 
      paths
      |> Seq.collect(
           fun p -> 
              p.Value 
                |> Seq.collect (
                     fun v -> 
                        v.Value.Responses |> Seq.choose(fun r -> r.Value.Schema) 
                         |> Seq.collect(fun d -> d.FlattenComplexDefinitions()))) 
      |> Seq.toList
      |> List.distinct
    let doc =
        { Swagger="2.0"
          Info=description ApiDescription.Empty
          BasePath=basePath
          Host=host
          Schemes=schemes
          Paths=paths
          Definitions = (definitions |> List.map (fun d -> d.Id,d) |> Map) }
    doc.ToJson() |> rawJson

  type DocumentationConfig =
      { MethodCallRules : Map<MethodCallId, AnalyzeRuleBody> -> Map<MethodCallId, AnalyzeRuleBody>
        DocumentationAddendums : DocumentationAddendumProvider
        Description : ApiDescription -> ApiDescription
        BasePath : string
        Host : string
        Schemes : string list
        SwaggerUrl : string
        SwaggerUiUrl : string }
      static member Default =
          { MethodCallRules=fun m -> m
            Description=fun d -> d
            BasePath="/"
            Host="localhost"
            Schemes=["http"]
            DocumentationAddendums=DefaultDocumentationAddendumProvider
            SwaggerUrl="/swagger.json"
            SwaggerUiUrl="/swaggerui/"}
  
  //type swaggerOf ([<ReflectedDefinition(true)>] webappWithVal:Expr<HttpFunc -> HttpContext -> HttpFuncResult>) =
  type swaggerOf ([<ReflectedDefinition(true)>] webappWithVal:Expr<WebPart>) =
    member __.Documents (configuration:DocumentationConfig->DocumentationConfig) =
      match webappWithVal with 
      | WithValue(v, ty, webapp) -> 
          let app = unbox<WebPart> v
          let config = configuration DocumentationConfig.Default
          let rules = { AppAnalyzeRules.Default with MethodCalls=(config.MethodCallRules AppAnalyzeRules.Default.MethodCalls) }
          let docCtx = analyze webapp rules
          let webPart = swaggerDoc docCtx config.DocumentationAddendums config.Description config.Schemes config.Host config.BasePath
          let swaggerJson = path config.SwaggerUrl >=> webPart
          choose [ 
                  swaggerJson
                  Suave.SwaggerUi.swaggerUiWebPart config.SwaggerUiUrl config.SwaggerUrl
                  app
            ]
      | other ->
          failwith "Invalid arg"

  let withConfig configuration (s:swaggerOf) =
    s.Documents configuration
         