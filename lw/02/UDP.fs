module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

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

            ICMP.receiveIcmpResponse icmpSocket allAddresses stopwatch
        with ex ->
            printfn "Error sending UDP packet: %s" ex.Message
            None
