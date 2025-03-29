module Traceroute.ICMP

open Config
open System.Net.Sockets
open System

type addresess2 = (byte array * byte array) option

let calculateChecksum (data: byte array) (offset: int) (length: int) (acc: uint32) =
    let rec loop (offset: int) (length: int) (acc: uint32) =
        if length <= 1 then
            let acc = acc + (if length = 1 then uint32 data.[offset] else 0u)
            (acc &&& 0xFFFFu) + (acc >>> 16)
        else
            let word = uint32 (data.[offset] <<< 8 ||| data.[offset + 1])
            let acc = acc + word
            loop (offset + 2) (length - 2) ((acc &&& 0xFFFFu) + (acc >>> 16))

    loop offset length acc

let createRawIcmpPacket
    (typeField: byte)
    (codeField: byte)
    (identifierField: uint16)
    payloadSize
    (sequenceField: uint16)
    =
    [| typeField
       codeField
       yield! BitConverter.GetBytes 0us
       yield! BitConverter.GetBytes identifierField
       yield! BitConverter.GetBytes sequenceField
       yield! Array.zeroCreate<byte> payloadSize |]

let createIcmpPacket
    (typeField: byte)
    (codeField: byte)
    (identifierField: uint16)
    (addresses: addresess2)
    payloadSize
    (sequenceField: uint16)
    =
    let rawPacket =
        createRawIcmpPacket typeField codeField identifierField payloadSize sequenceField

    if addresses.IsNone then
        let checksumField =
            let sum = calculateChecksum rawPacket 0 rawPacket.Length 0u
            let carry = (sum >>> 16) + (sum &&& 0xFFFFu)
            uint16 (~~~carry)

        rawPacket.[2..3] <- BitConverter.GetBytes checksumField
    else
        let (localAddr, remoteAddr) = addresses.Value

        let pseudoHeader =
            [| yield! localAddr
               yield! remoteAddr
               yield! BitConverter.GetBytes(uint32 rawPacket.Length)
               yield! [| 0uy; 0uy; 0uy; 58uy |] |]

        let checksumField =
            let sumPseudo = calculateChecksum pseudoHeader 0 pseudoHeader.Length 0u
            let sumPacket = calculateChecksum rawPacket 0 rawPacket.Length sumPseudo
            let carry = (sumPacket >>> 16) + (sumPacket &&& 0xFFFFu)
            uint16 (~~~carry)

        rawPacket.[2..3] <- BitConverter.GetBytes checksumField

    rawPacket

let icmpId = uint16 (Diagnostics.Process.GetCurrentProcess().Id % 65536)

let createIcmpEcho addresess =
    createIcmpPacket
        (match addresess with
         | None -> 8uy
         | Some _ -> 128uy)
        0uy
        icmpId
        addresess

let route (config: Config) =
    let isIPv6 = config.remoteEP.AddressFamily = AddressFamily.InterNetworkV6

    use socket =
        new Socket(
            config.remoteEP.AddressFamily,
            SocketType.Raw,
            if isIPv6 then ProtocolType.IcmpV6 else ProtocolType.Icmp
        )

    socket.Bind config.localEP
    socket.SendTimeout <- config.sendTime
    socket.ReceiveTimeout <- config.receiveTime

    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, config.tos)

    let echoRequest =
        createIcmpEcho
            (if isIPv6 then
                 Some(config.localEP.Address.GetAddressBytes(), config.localEP.Address.GetAddressBytes())
             else
                 None)
            config.bytes

    let rec loop ttl =
        let mutable remoteEP: Net.EndPoint = config.remoteEP :> Net.EndPoint
        socket.Ttl <- int16 ttl

        printf "%*d" (config.max_ttl.ToString().Length) ttl

        try
            let reqBuffer = echoRequest ttl
            let resBuffer = Array.zeroCreate<byte> 1024

            let sw = Diagnostics.Stopwatch.StartNew()

            socket.SendTo(reqBuffer, remoteEP) |> ignore

            socket.ReceiveFrom(resBuffer, 0, resBuffer.Length, SocketFlags.None, &remoteEP)
            |> ignore

            sw.Stop()
            let ep = (remoteEP :?> Net.IPEndPoint).Address

            if ep = config.localEP.Address then
                printfn "  *"
            else
                printfn "  %s  %.3f ms" (makeHostIPPair ep) sw.Elapsed.TotalMilliseconds
        with :? SocketException ->
            printfn "  *"

        if
            ttl <= config.max_ttl
            && (remoteEP :?> Net.IPEndPoint).Address <> config.remoteEP.Address
        then
            loop (ttl + 1us)

    loop config.first_ttl
