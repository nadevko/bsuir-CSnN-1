module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net.Sockets

let probe : Probe =
    fun traceOpts probeOpts  ->

        use icmpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

        use udpClient = new UdpClient ()
        udpClient.Client.SendTimeout <- traceOpts.SendTimeout
        udpClient.Ttl <- int16  probeOpts.Ttl

        let stopwatch = System.Diagnostics.Stopwatch.StartNew ()
        let packet = Array.zeroCreate<byte> traceOpts.PayloadSize

        try
            udpClient.Send (packet, traceOpts.PayloadSize, probeOpts.RemoteEP) |> ignore

            ICMP.receiveIcmpResponse icmpSocket stopwatch probeOpts.Addresses
        with ex ->
            printfn "Error sending UDP packet: %s" ex.Message
            None
