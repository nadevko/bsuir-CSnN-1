module Traceroute.ICMP

open System.Net
open Config
open System.Net.Sockets
open System.Diagnostics

// let sendRequest (host: IPAddress) (ttl: int) =
//     client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl)
//     let icmpPacket = Array.create 32 0uy
//     client.Send(icmpPacket, icmpPacket.Length, IPEndPoint(host, 0)) |> ignore

// let receiveReply (timeout: int) =
//     let asyncReceive = Async.AwaitTask(client.ReceiveAsync())

//     async {
//         let! result = Async.Catch(asyncReceive)

//         match result with
//         | Choice1Of2 udpResult -> let result = Some(udpResult.Buffer, udpResult.RemoteEndPoint) in return result
//         | Choice2Of2 _ -> return None
//     }
//     |> Async.RunSynchronously

let route (config: Config) =
    let ttlFieldSize = config.max_ttl.ToString().Length

    new UdpClient(config.port)


// printf "%A" deviceIp

// for ttl in uint config.first_ttl .. uint config.max_ttl do
//     printfn "%*d  " ttlFieldSize ttl

//     for query in 1 .. int config.nqueries do
//         let stopwatch = Stopwatch.StartNew()
//         sendRequest ipAddress ttl
//         use client = new UdpClient(0)

//         match receiveReply client (int config.msec) with
//         | Some(data, endpoint) ->
//             stopwatch.Stop()
//             printfn "Reply from %s: time=%dms" (endpoint.Address.ToString()) stopwatch.ElapsedMilliseconds
//         | None -> printfn "Request timed out."
