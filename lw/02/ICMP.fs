module CSnN1.Lw02.ICMP

open System
open System.Net
open System.Net.Sockets

let receiveIcmpResponse (icmpSocket : Socket) (allAddresses : IPAddress array) (stopwatch : Diagnostics.Stopwatch) =
    let buffer = Array.zeroCreate<byte> 1024
    let endPoint = ref (new IPEndPoint(IPAddress.Any, 0) :> EndPoint)

    try
        let bytesReceived = icmpSocket.ReceiveFrom (buffer, endPoint)
        stopwatch.Stop ()

        let responseAddress =
            match endPoint.Value with
            | :? IPEndPoint as ipEndPoint -> ipEndPoint.Address
            | _ -> IPAddress.None

        let icmpType = if bytesReceived >= 20 then int buffer.[20] else -1
        let icmpCode = if bytesReceived >= 21 then int buffer.[21] else -1
        let isTargetHost = Array.exists (fun addr -> addr.Equals responseAddress) allAddresses

        Some (responseAddress, stopwatch.ElapsedMilliseconds, icmpType, icmpCode, isTargetHost)
    with
    | :? SocketException as ex when ex.SocketErrorCode = SocketError.TimedOut -> None
    | ex ->
        printfn "Socket error: %s" ex.Message
        None
