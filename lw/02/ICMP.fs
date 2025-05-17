module CSnN1.Lw02.ICMP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets
open System.Diagnostics

let pid = Process.GetCurrentProcess().Id

let createIcmpPacket (payloadSize : int) (sequence : int) =
    let headerSize = 8
    let totalSize = headerSize + payloadSize

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
           byte (pid >>> 8)
           byte pid
           // Sequence number
           byte (sequence >>> 8)
           byte sequence
           // Payload (zeros)
           yield! [| for _ in 1..payloadSize -> 0uy |] |]

    // Calculate ICMP checksum
    let mutable checksum = 0us

    for i in 0..2 .. totalSize - 1 do
        if i + 1 < totalSize then
            checksum <- checksum + (uint16 packet.[i] <<< 8) + uint16 packet.[i + 1]
        else
            checksum <- checksum + (uint16 packet.[i] <<< 8)

    while checksum >>> 16 > 0us do
        checksum <- (checksum &&& 0xFFFFus) + (checksum >>> 16)

    checksum <- ~~~checksum

    packet.[2] <- byte (checksum >>> 8)
    packet.[3] <- byte checksum

    packet

let receiveResponse (icmpSocket : Socket) (payload : int) (stopwatch : Stopwatch) (allAddresses : IPAddress array) =
    let buffer = Array.zeroCreate<byte> (28 + payload)
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

        let isSuccess =
            icmpType = 0 && icmpCode = 0
            || // Echo Reply
            Array.exists (fun addr -> addr.Equals receiveAddress) allAddresses

        Some (receiveAddress, stopwatch.ElapsedMilliseconds, isSuccess, buffer)
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
            let packet = createIcmpPacket (max 0 traceOpts.PayloadSize) ttl
            stopwatch.Start ()

            try
                icmpSocket.SendTo (packet, RemoteEP) |> ignore
            with ex ->
                printfn "Error sending ICMP packet: %s" ex.Message

        let receive () =
            match receiveResponse icmpSocket traceOpts.PayloadSize stopwatch probeOpts.Addresses with
            | Some (receiveAddress, elapsed, isSuccess, receiveBuffer) ->
                Some
                    { ttl = int receiveBuffer.[26] <<< 8 ||| int receiveBuffer.[27]
                      ip = receiveAddress
                      ms = elapsed
                      isSuccess = isSuccess }
            | _ -> None

        let dispose () = icmpSocket.Dispose ()

        send, receive, dispose
