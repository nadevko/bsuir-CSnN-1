module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config
open CSnN1.Lw02.Checksum

open System.Net
open System.Net.Sockets
open System.Diagnostics

let createUdpPacket (sourcePort : int) (destPort : int) (payloadSize : int) =
    let headerSize = 8
    let totalSize = headerSize + payloadSize

    let packet =
        [| byte (sourcePort >>> 8)
           byte sourcePort
           byte (destPort >>> 8)
           byte destPort
           byte (totalSize >>> 8)
           byte totalSize
           0uy
           0uy
           yield! [| for _ in 1..payloadSize -> 0uy |] |]

    packet

let setUdpChecksum (udpPacket : byte[]) (sourceIP : IPAddress) (destIP : IPAddress) =
    let packet = Array.copy udpPacket

    packet.[6] <- 0uy
    packet.[7] <- 0uy

    let totalSize = packet.Length

    let pseudoHeader =
        [| yield! sourceIP.GetAddressBytes ()
           yield! destIP.GetAddressBytes ()
           0uy
           17uy
           byte (totalSize >>> 8)
           byte totalSize |]

    let checksum =
        let sum = calculateChecksum (Array.concat [ pseudoHeader ; packet ])
        if sum = 0us then 0xFFFFus else sum

    packet.[6] <- byte (checksum >>> 8)
    packet.[7] <- byte checksum

    packet

type Prober (traceOpts : Config.TraceOptions, probeOpts : ProbeOptions) =
    let icmpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    do
        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

    let udpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

    do udpSocket.SendTimeout <- traceOpts.SendTimeout

    interface IProber with
        member _.Probe ttl =
            let remoteEP = probeOpts.RemoteEP ttl

            udpSocket.Ttl <- int16 ttl

            let packet =
                createUdpPacket probeOpts.LocalEP.Port remoteEP.Port traceOpts.PayloadSize

            let packetWithChecksum =
                setUdpChecksum packet probeOpts.LocalEP.Address remoteEP.Address

            let stopwatch = new Stopwatch ()

            try
                stopwatch.Start ()
                udpSocket.SendTo (packetWithChecksum, remoteEP) |> ignore
            with ex ->
                printfn "Error sending UDP packet: %s" ex.Message

            match ICMP.receiveResponse icmpSocket 52 stopwatch with
            | Some response ->
                let responsePort = int response.buffer.[50] <<< 8 ||| int response.buffer.[51]
                let basePort = probeOpts.RemoteEP(0).Port

                let isTtlExtractable =
                    response.buffer.Length = 52
                    && responsePort >= basePort
                    && responsePort <= basePort + (traceOpts.MaxTTL - traceOpts.FirstTTL)

                try
                    Some
                        { ttl = if isTtlExtractable then responsePort - basePort else ttl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName traceOpts response.ip
                          isSuccess =
                            response.icmpType = 3
                            && response.icmpCode = 3
                            && isTtlExtractable
                            && Array.exists (fun addr -> addr.Equals response.ip) probeOpts.Addresses }
                with _ ->
                    None
            | None -> None

    interface System.IDisposable with
        member _.Dispose () =
            icmpSocket.Dispose ()
            udpSocket.Dispose ()
