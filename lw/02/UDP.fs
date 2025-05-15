module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config
open CSnN1.Lw02.Traceroute

open System.Net
open System.Net.Sockets
open System.Text

let probe : Probe =
    fun ttl targetIp allAddresses receiveTimeout ->
        use icmpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

        icmpSocket.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, receiveTimeout)
        icmpSocket.Bind (new IPEndPoint (IPAddress.Any, 0))

        use udpClient = new UdpClient ()
        udpClient.Ttl <- int16 ttl

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

        let port = 33434
        let message = Encoding.ASCII.GetBytes "Traceroute Packet"

        try
            udpClient.Send (message, message.Length, new IPEndPoint (targetIp, port))
            |> ignore

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
                    Array.exists (fun addr -> addr.Equals (responseAddress)) allAddresses

                Some (responseAddress, stopwatch.ElapsedMilliseconds, icmpType, icmpCode, isTargetHost)
            with
            | :? SocketException as ex when ex.SocketErrorCode = SocketError.TimedOut -> None
            | ex ->
                printfn "Socket error: %s" ex.Message
                None
        with ex ->
            printfn "Error sending UDP packet: %s" ex.Message
            None
