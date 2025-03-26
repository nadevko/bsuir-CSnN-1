module Traceroute.Program

[<EntryPoint>]
let main argv =
    Arguments.configure argv |> printfn "Got parse results %A"
    0
