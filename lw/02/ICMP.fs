module Traceroute.ICMP

open Config
open System
open System.Net
open System.Net.Sockets
open System.Diagnostics

let route (config: Config) =
    use socket =
        new Socket(config.localEP.AddressFamily, SocketType.Raw, ProtocolType.Icmp)

    socket.ReceiveTimeout <- config.timeout

    let ttlFieldSize = config.max_ttl.ToString().Length
    let message = Array.zeroCreate<byte> config.bytes

    for ttl in config.first_ttl .. config.max_ttl do
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl)

        let results =
            Async.Parallel
                [ for _ in 1 .. int config.queries ->
                      async {
                          let mutable remoteEP = config.remoteEP :> EndPoint
                          let mutable isLoss = false
                          let buffer = Array.zeroCreate<byte> 1024
                          let replyTime = Stopwatch.StartNew()

                          try
                              let bytesSent = socket.SendTo(message, remoteEP)
                              replyTime.Stop()

                              if bytesSent <> config.bytes then
                                  isLoss <- true

                              socket.ReceiveFrom(buffer, &remoteEP) |> ignore

                              let ipEP = remoteEP :?> IPEndPoint
                              return Some(ipEP, replyTime.Elapsed, isLoss)
                          with :? SocketException as ex when ex.SocketErrorCode = SocketError.TimedOut ->
                              return None
                      } ]
            |> Async.RunSynchronously

        let endPoint =
            results
            |> Array.tryFind (fun x -> x.IsSome)
            |> fun x ->
                match x with
                | Some(Some(ep, _, isLoss)) ->
                    sprintf "  %s (%s)" (Dns.GetHostEntry(ep.Address).HostName) (ep.Address.ToString())
                | _ -> ""

        results
        |> Array.map (fun x ->
            match x with
            | Some(_, ts, isLoss) ->
                ts.TotalMilliseconds
                |> fun ms -> sprintf "%.3f%s ms" ms (if isLoss then sprintf "?" else "")
            | _ -> "*")
        |> String.concat "  "
        |> printfn "%*d%s  %s" ttlFieldSize ttl endPoint
