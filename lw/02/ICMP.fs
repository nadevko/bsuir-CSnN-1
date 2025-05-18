module CSnN1.Lw02.ICMP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets
open System.Diagnostics

let id = Process.GetCurrentProcess().Id

let tryGetHostName options (ip : IPAddress) =
    if not options.ResolveNames then
        None
    else
        try
            let host = Dns.GetHostEntry ip
            Some host.HostName
        with _ ->
            None

let calculateIcmpChecksum (packet : byte array) : uint16 =
    let mutable sum = 0

    for i in 0..2 .. packet.Length - 1 do
        let highByte = if i < packet.Length then int packet.[i] else 0
        let lowByte = if i + 1 < packet.Length then int packet.[i + 1] else 0
        let word = highByte <<< 8 ||| lowByte
        sum <- sum + word

    while sum >>> 16 > 0 do
        sum <- (sum &&& 0xFFFF) + (sum >>> 16)

    uint16 ~~~sum

let createIcmpPacket (payloadSize : int) (sequence : int) =
    // Create the initial packet with header values
    let packet =
        [|
           // Type (8 = Echo Request)
           8uy
           // Code (0 for Echo)
           0uy
           // Checksum placeholder
           0uy
           0uy
           // Identifier
           byte (id >>> 8)
           byte id
           // Sequence number
           byte (sequence >>> 8)
           byte sequence
           // Payload (zeros)
           yield! [| for _ in 1..payloadSize -> 0uy |] |]

    // Calculate ICMP checksum
    let checksum = calculateIcmpChecksum packet
    packet.[2] <- byte (checksum >>> 8)
    packet.[3] <- byte checksum

    packet

type IcmpResponse =
    { ip : IPAddress
      ms : int64
      buffer : byte array
      icmpType : int
      icmpCode : int }

let receiveResponse (icmpSocket : Socket) (bufferSize : int) (stopwatch : Stopwatch) =
    let buffer = Array.zeroCreate<byte> bufferSize
    let remoteEP = ref (new IPEndPoint (IPAddress.Any, 0) :> EndPoint)

    try
        let receiveBuffer = icmpSocket.ReceiveFrom (buffer, remoteEP)
        stopwatch.Stop ()

        let receiveAddress =
            match remoteEP.Value with
            | :? IPEndPoint as ep -> ep.Address
            | _ -> IPAddress.None

        let icmpType = if receiveBuffer >= 20 then int buffer.[20] else -1
        let icmpCode = if receiveBuffer >= 21 then int buffer.[21] else -1

        Some
            { ip = receiveAddress
              ms = stopwatch.ElapsedMilliseconds
              buffer = buffer
              icmpType = icmpType
              icmpCode = icmpCode }
    with _ ->
        None

let probe : ProbeFactory =
    fun traceOpts probeOpts ->
        let icmpSocket =
            new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

        let mutable stopwatch = Stopwatch ()

        let send ttl =
            let RemoteEP = probeOpts.RemoteEP ttl
            icmpSocket.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl)
            let packet = createIcmpPacket traceOpts.PayloadSize ttl
            stopwatch.Start ()

            try
                icmpSocket.Ttl <- int16 ttl
                icmpSocket.SendTo (packet, RemoteEP) |> ignore
            with ex ->
                printfn "Error sending ICMP packet: %s" ex.Message

        let receive () =
            match receiveResponse icmpSocket (56 + traceOpts.PayloadSize) stopwatch with
            | Some response ->
                let packetId =
                    if response.icmpType = 0 then
                        int response.buffer.[24] <<< 8 ||| int response.buffer.[25]
                    else
                        int response.buffer.[52] <<< 8 ||| int response.buffer.[53]

                if packetId <> id then
                    None
                else
                    Some
                        { ttl =
                            if response.icmpType = 0 then
                                int response.buffer.[26] <<< 8 ||| int response.buffer.[27]
                            else
                                int response.buffer.[54] <<< 8 ||| int response.buffer.[55]
                          ip = response.ip
                          ms = response.ms
                          hostName = tryGetHostName traceOpts response.ip
                          isSuccess =
                            response.icmpType = 0 && response.icmpCode = 0
                            || // Echo Reply
                            Array.exists (fun addr -> addr.Equals response.ip) probeOpts.Addresses }
            | _ -> None

        let dispose () = icmpSocket.Dispose ()

        send, receive, dispose
