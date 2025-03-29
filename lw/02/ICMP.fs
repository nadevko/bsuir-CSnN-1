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
    use socket =
        new Socket(
            config.remoteEP.AddressFamily,
            SocketType.Raw,
            match config.remoteEP.AddressFamily with
            | AddressFamily.InterNetworkV6 -> ProtocolType.IcmpV6
            | _ -> ProtocolType.Icmp
        )

    let createEcho =
        createIcmpEcho
            (match config.remoteEP.AddressFamily with
             | AddressFamily.InterNetworkV6 ->
                 Some(config.localEP.Address.GetAddressBytes(), config.remoteEP.Address.GetAddressBytes())
             | _ -> None)
            config.bytes

    createEcho 0us
