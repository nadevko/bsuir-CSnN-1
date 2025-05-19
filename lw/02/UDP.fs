module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

let createUdpPacket (localEP : IPEndPoint) (remoteEP : IPEndPoint) (payloadSize : int) =
    let headerSize = 8
    let totalSize = headerSize + payloadSize

    let packet =
        [|
           // Source port (2 bytes)
           byte (localEP.Port >>> 8)
           byte localEP.Port
           // Destination port (2 bytes)
           byte (remoteEP.Port >>> 8)
           byte remoteEP.Port
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
           yield! localEP.Address.GetAddressBytes ()
           // Destination IP (4 bytes)
           yield! remoteEP.Address.GetAddressBytes ()
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

type Prober (traceOpts : TraceOptions, probeOpts : ProbeOptions) =
    let icmpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    do
        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

    let udpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

    do udpSocket.SendTimeout <- traceOpts.SendTimeout

    let isDisposed = ref false

    interface IProber with
        member _.Probe ttl =
            let RemoteEP = probeOpts.RemoteEP ttl
            udpSocket.Ttl <- int16 ttl

            let packet = createUdpPacket probeOpts.LocalEP RemoteEP traceOpts.PayloadSize
            let stopwatch = new System.Diagnostics.Stopwatch()

            try
                stopwatch.Start()
                udpSocket.SendTo (packet, RemoteEP) |> ignore
            with ex ->
                printfn "Error sending UDP packet: %s" ex.Message

            match ICMP.receiveResponse icmpSocket 576 stopwatch with
            | Some response ->
                try
                    let ttl =
                        if response.buffer.Length >= 52 then
                            try
                                let responsePort = int response.buffer.[50] <<< 8 ||| int response.buffer.[51]
                                let basePort = (probeOpts.RemoteEP 0).Port

                                if responsePort >= basePort && responsePort <= basePort + 100 then
                                    responsePort - basePort
                                else
                                    int udpSocket.Ttl
                            with _ ->
                                int udpSocket.Ttl
                        else
                            int udpSocket.Ttl

                    Some
                        { ttl = ttl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName traceOpts response.ip
                          isSuccess = response.icmpType = 3 && response.icmpCode = 3 }
                with ex ->
                    printfn "Error extracting port from ICMP response: %s" ex.Message

                    Some
                        { ttl = int udpSocket.Ttl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName traceOpts response.ip
                          isSuccess = false }
            | None -> None

    interface System.IDisposable with
        member _.Dispose () =
            if not isDisposed.Value then
                icmpSocket.Dispose ()
                udpSocket.Dispose ()
                isDisposed.Value <- true
