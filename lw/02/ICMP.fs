module Traceroute.ICMP

open Config
open System.Net.Sockets
open System

let calculateChecksum (data: uint8 array) (offset: int) (length: int) (acc: uint32) =
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
    (typeField: uint8)
    (codeField: uint8)
    (identifierField: uint16)
    payloadSize
    (sequenceField: uint16)
    =
    [| typeField
       codeField
       yield! BitConverter.GetBytes 0us
       yield! BitConverter.GetBytes identifierField
       yield! BitConverter.GetBytes sequenceField
       yield! Array.zeroCreate<uint8> payloadSize |]

let createIcmpPacket typeField codeField identifierField payloadSize sequenceField =
    let rawPacket =
        createRawIcmpPacket typeField codeField identifierField payloadSize sequenceField

    let checksumField =
        let sum = calculateChecksum rawPacket 0 rawPacket.Length 0u
        let carry = (sum >>> 16) + (sum &&& 0xFFFFu)
        uint16 (~~~carry)

    rawPacket.[2..3] <- BitConverter.GetBytes checksumField

    rawPacket

let icmpId = uint16 (Diagnostics.Process.GetCurrentProcess().Id % 65536)

let createIcmpEcho = createIcmpPacket 8uy 0uy icmpId

let route (config: Config) =
    // use socket =
        // new Socket()

    // socket.Bind config.localEP
    // socket.SendTimeout <- config.sendTime
    // socket.ReceiveTimeout <- config.receiveTime

    // socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, config.tos)

    let rec loop ttl =
        let mutable remoteEP: Net.EndPoint = config.remoteEP :> Net.EndPoint
        // socket.Ttl <- int16 ttl

        printf "%*d" (config.max_ttl.ToString().Length) ttl

        try
            let reqBuffer = createIcmpEcho config.bytes ttl
            let resBuffer = Array.zeroCreate<uint8> 1024

            let sw = Diagnostics.Stopwatch.StartNew()

            // socket.SendTo(reqBuffer, remoteEP) |> ignore

            // socket.ReceiveFrom(resBuffer, 0, resBuffer.Length, SocketFlags.None, &remoteEP)
            // |> ignore

            sw.Stop()
            let ep = (remoteEP :?> Net.IPEndPoint).Address

            if ep = config.localEP.Address then
                printfn "  *"
            else
                printfn "  %s  %.3f ms" (makeHostIPPair ep) sw.Elapsed.TotalMilliseconds
        with :? SocketException ->
            printfn "  *"

        if ttl <= config.max_ttl then
            loop (ttl + 1us)

    loop config.first_ttl
