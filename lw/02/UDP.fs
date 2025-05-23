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

let setUdpChecksum (udpPacket : byte[]) (sourceIP : IPAddress) (destIP : IPAddress) (addressFamily : AddressFamily) =
    let packet = Array.copy udpPacket

    packet.[6] <- 0uy
    packet.[7] <- 0uy

    let totalSize = packet.Length

    let pseudoHeader =
        match addressFamily with
        | AddressFamily.InterNetworkV6 ->
            [| yield! sourceIP.GetAddressBytes ()
               yield! destIP.GetAddressBytes ()
               0uy
               0uy
               byte (totalSize >>> 8)
               byte totalSize
               0uy
               0uy
               0uy
               17uy |]
        | _ ->
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

type Prober (options : ProbeOptions) =
    let addressFamily = options.LocalEP.AddressFamily

    let icmpSocket =
        match addressFamily with
        | AddressFamily.InterNetworkV6 -> new Socket (AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IcmpV6)
        | _ -> new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    do
        icmpSocket.ReceiveTimeout <- options.ReceiveTimeout
        icmpSocket.Bind options.LocalEP

    let udpSocket = new Socket (addressFamily, SocketType.Dgram, ProtocolType.Udp)

    do udpSocket.SendTimeout <- options.SendTimeout

    interface IProber with
        member _.Probe ttl _ =
            let remoteEP = options.RemoteEP (options.LocalEP.Port + ttl)

            udpSocket.Ttl <- int16 ttl

            let packet = createUdpPacket options.LocalEP.Port remoteEP.Port options.PayloadSize

            let packetWithChecksum =
                setUdpChecksum packet options.LocalEP.Address remoteEP.Address addressFamily

            let stopwatch = new Stopwatch ()

            try
                stopwatch.Start ()
                udpSocket.SendTo (packetWithChecksum, remoteEP) |> ignore
            with ex ->
                printfn "Error sending UDP packet: %s" ex.Message

            let bufferSize =
                match addressFamily with
                | AddressFamily.InterNetworkV6 -> 72
                | _ -> 52

            match ICMP.receiveResponse icmpSocket bufferSize stopwatch with
            | Some response ->
                let responsePort =
                    match addressFamily with
                    | AddressFamily.InterNetworkV6 -> int response.buffer.[70] <<< 8 ||| int response.buffer.[71]
                    | _ -> int response.buffer.[50] <<< 8 ||| int response.buffer.[51]

                let basePort = options.RemoteEP(0).Port

                let isTtlExtractable =
                    responsePort >= basePort
                    && responsePort <= basePort + (options.MaxTTL - options.FirstTTL)

                try
                    Some
                        { ttl = if isTtlExtractable then responsePort - basePort else ttl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName options.ResolveNames response.ip
                          isSuccess =
                            response.icmpType = 3
                            && response.icmpCode = 3
                            && Array.exists (fun addr -> addr.Equals response.ip) options.Addresses }
                with _ ->
                    None
            | None -> None

    interface System.IDisposable with
        member _.Dispose () =
            icmpSocket.Dispose ()
            udpSocket.Dispose ()
