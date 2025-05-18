module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

let createUdpPacket
    (sourcePort : int)
    (destinationPort : int)
    (payloadSize : int)
    (localIP : IPAddress)
    (remoteIP : IPAddress)
    =
    let headerSize = 8
    let totalSize = headerSize + payloadSize

    let packet =
        [|
           // Source port (2 bytes)
           byte (sourcePort >>> 8)
           byte sourcePort
           // Destination port (2 bytes)
           byte (destinationPort >>> 8)
           byte destinationPort
           // Length (2 bytes) - includes header and data
           byte (totalSize >>> 8)
           byte totalSize
           // Checksum (2 bytes) - set to 0 for now
           0uy
           0uy
           // Payload (zeros)
           yield! [| for _ in 1..payloadSize -> 0uy |] |]

    // Calculate UDP checksum using IPv4 pseudo-header, UDP header, payload
    let pseudoHeader =
        [|
           // Source IP (4 bytes)
           yield! localIP.GetAddressBytes ()
           // Destination IP (4 bytes)
           yield! remoteIP.GetAddressBytes ()
           // Zero (1 byte)
           0uy
           // Protocol (1 byte) - 17 for UDP
           17uy
           // UDP Length (2 bytes)
           byte (totalSize >>> 8)
           byte totalSize |]

    // Combine pseudo-header and UDP packet for checksum calculation
    let checksumData = Array.concat [ pseudoHeader ; packet ]

    // Calculate checksum over the combined data
    let mutable checksum = 0us

    for i in 0..2 .. checksumData.Length - 1 do
        if i + 1 < checksumData.Length then
            checksum <- checksum + (uint16 checksumData.[i] <<< 8) + uint16 checksumData.[i + 1]
        else
            checksum <- checksum + (uint16 checksumData.[i] <<< 8)

    while checksum >>> 16 > 0us do
        checksum <- (checksum &&& 0xFFFFus) + (checksum >>> 16)

    checksum <- ~~~checksum

    // If checksum is zero, use all ones (0xFFFF)
    checksum <- if checksum = 0us then 0xFFFFus else checksum

    packet.[6] <- byte (checksum >>> 8)
    packet.[7] <- byte checksum

    packet

let probe : ProbeFactory =
    fun traceOpts probeOpts ->

        let icmpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

        let udpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

        udpSocket.SendTimeout <- traceOpts.SendTimeout
        udpSocket.Bind probeOpts.LocalEP
        let mutable stopwatch = System.Diagnostics.Stopwatch ()

        let send ttl =
            let RemoteEP = probeOpts.RemoteEP ttl

            let packet =
                createUdpPacket
                    probeOpts.LocalEP.Port
                    (probeOpts.LocalEP.Port + ttl)
                    traceOpts.PayloadSize
                    probeOpts.LocalEP.Address
                    RemoteEP.Address

            stopwatch.Start ()

            try
                udpSocket.Ttl <- int16 ttl
                udpSocket.SendTo (packet, RemoteEP) |> ignore
            with ex ->
                printfn "Error sending UDP packet: %s" ex.Message

        let receive () =
            match ICMP.receiveResponse icmpSocket (56 + traceOpts.PayloadSize) stopwatch probeOpts.Addresses with
            | Some response ->
                Some
                    { ttl =
                        (int response.buffer.[48] <<< 8 ||| int response.buffer.[49])
                        - probeOpts.LocalEP.Port
                      ip = response.ip
                      ms = response.ms
                      hostName = ICMP.tryGetHostName traceOpts response.ip
                      isSuccess = response.isSuccess }
            | None -> None

        let dispose () =
            icmpSocket.Dispose ()
            udpSocket.Dispose ()

        send, receive, dispose
