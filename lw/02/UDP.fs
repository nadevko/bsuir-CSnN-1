module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

let receiveIcmpResponse (icmpSocket : Socket) (allAddresses : IPAddress[]) (stopwatch : System.Diagnostics.Stopwatch) =
    let buffer = Array.zeroCreate<byte> 1024
    let endPoint = ref (new IPEndPoint (IPAddress.Any, 0) :> EndPoint)

    try
        let bytesReceived = icmpSocket.ReceiveFrom (buffer, endPoint)
        stopwatch.Stop ()

        let responseAddress =
            match endPoint.Value with
            | :? IPEndPoint as ipEndPoint -> ipEndPoint.Address
            | _ -> IPAddress.None

        let icmpType = if bytesReceived >= 20 then int buffer.[20] else -1
        let icmpCode = if bytesReceived >= 21 then int buffer.[21] else -1

        let isTargetHost =
            Array.exists (fun addr -> addr.Equals responseAddress) allAddresses

        Some (responseAddress, stopwatch.ElapsedMilliseconds, icmpType, icmpCode, isTargetHost)
    with
    | :? SocketException as ex when ex.SocketErrorCode = SocketError.TimedOut -> None
    | ex ->
        printf "Socket error: %s" ex.Message
        None

let probe : Probe =
    fun options targetIp allAddresses ttl ->
        let localEP = new IPEndPoint (IPAddress.Any, 0)
        let remoteEP = new IPEndPoint (targetIp, options.Port)

        use icmpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

        icmpSocket.ReceiveTimeout <- options.ReceiveTimeout
        icmpSocket.Bind localEP

        use udpClient = new UdpClient ()
        udpClient.Client.SendTimeout <- options.SendTimeout
        udpClient.Ttl <- int16 ttl

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        let datagram = Array.zeroCreate<byte> options.datagramLength

        try
            udpClient.Send (datagram, options.datagramLength, remoteEP) |> ignore

            receiveIcmpResponse icmpSocket allAddresses stopwatch
        with ex ->
            printfn "Error sending UDP packet: %s" ex.Message
            None
