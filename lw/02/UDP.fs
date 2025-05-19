module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config
open CSnN1.Lw02.ICMP
open CSnN1.Lw02.Checksum

open System
open System.Net
open System.Net.Sockets
open System.Diagnostics

/// Создает UDP заголовок и данные
let createUdpPacket (sourcePort : int) (destPort : int) (payloadSize : int) =
    let headerSize = 8
    let totalSize = headerSize + payloadSize

    let packet =
        [|
           // Source port (2 bytes)
           byte (sourcePort >>> 8)
           byte sourcePort
           // Destination port (2 bytes)
           byte (destPort >>> 8)
           byte destPort
           // Length (2 bytes) - includes header and data
           byte (totalSize >>> 8)
           byte totalSize
           // Checksum (2 bytes) - set to 0 for now
           0uy
           0uy
           // Payload (zeros)
           yield! [| for _ in 1..payloadSize -> 0uy |]
        |]

    packet

let setUdpChecksum (udpPacket : byte[]) (sourceIP : IPAddress) (destIP : IPAddress) =
    // Создаем копию пакета для модификации
    let packet = Array.copy udpPacket

    // Сначала обнуляем поле контрольной суммы
    packet.[6] <- 0uy
    packet.[7] <- 0uy

    let totalSize = packet.Length

    // Создаем псевдо-заголовок для расчета контрольной суммы UDP
    let pseudoHeader =
        [|
           // Source IP (4 bytes)
           yield! sourceIP.GetAddressBytes()

           // Destination IP (4 bytes)
           yield! destIP.GetAddressBytes()

           // Zero (1 byte)
           0uy

           // Protocol (1 byte) - 17 for UDP
           17uy

           // UDP Length (2 bytes)
           byte (totalSize >>> 8)
           byte totalSize
        |]

    // Вычисляем контрольную сумму с помощью общей функции из модуля Checksum
    let checksum =
        let sum = calculateChecksum (Array.concat [pseudoHeader; packet])
        if sum = 0us then 0xFFFFus else sum

    // Устанавливаем контрольную сумму в пакет
    packet.[6] <- byte (checksum >>> 8)
    packet.[7] <- byte checksum

    packet

type Prober (traceOpts : Config.TraceOptions, probeOpts : ProbeOptions) =
    // Сокет для прослушивания ICMP ответов для перехвата сообщений об ошибках UDP
    let icmpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    do
        icmpSocket.ReceiveTimeout <- traceOpts.ReceiveTimeout
        icmpSocket.Bind probeOpts.LocalEP

    // UDP сокет для отправки запросов
    let udpSocket =
        new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

    do
        udpSocket.SendTimeout <- traceOpts.SendTimeout

    let isDisposed = ref false

    interface IProber with
        member _.Probe ttl =
            // Используем функцию RemoteEP для получения конечной точки с портом, увеличенным на ttl
            let remoteEP = probeOpts.RemoteEP ttl

            // Устанавливаем TTL для UDP-пакета
            udpSocket.Ttl <- int16 ttl

            // Создаем UDP пакет
            let packet = createUdpPacket probeOpts.LocalEP.Port remoteEP.Port traceOpts.PayloadSize

            // Устанавливаем контрольную сумму для UDP пакета
            let packetWithChecksum = setUdpChecksum packet probeOpts.LocalEP.Address remoteEP.Address

            let stopwatch = new Stopwatch()

            try
                stopwatch.Start()
                // Отправляем UDP пакет
                udpSocket.SendTo(packetWithChecksum, remoteEP) |> ignore
            with ex ->
                printfn "Error sending UDP packet: %s" ex.Message

            // Ожидаем ICMP ответ (Time Exceeded или Destination Unreachable)
            match ICMP.receiveResponse icmpSocket 576 stopwatch with
            | Some response ->
                try
                    // Определяем TTL на основе порта
                    let calculatedTtl =
                        if response.buffer.Length >= 52 then
                            try
                                // Извлекаем порт назначения из ICMP-ответа
                                let responsePort = int response.buffer.[50] <<< 8 ||| int response.buffer.[51]
                                // Получаем базовый порт из функции RemoteEP(0)
                                let basePort = (probeOpts.RemoteEP 0).Port

                                if responsePort >= basePort && responsePort <= basePort + 100 then
                                    // Извлекаем ttl из порта назначения
                                    responsePort - basePort
                                else
                                    ttl  // Используем переданный ttl как fallback
                            with _ ->
                                ttl
                        else
                            ttl

                    Some
                        { ttl = calculatedTtl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName traceOpts response.ip
                          // Считаем успешным достижение цели, если получили ICMP Destination Unreachable
                          isSuccess = response.icmpType = 3 && response.icmpCode = 3 }
                with ex ->
                    printfn "Error extracting port from ICMP response: %s" ex.Message
                    Some
                        { ttl = ttl
                          ip = response.ip
                          ms = response.ms
                          hostName = ICMP.tryGetHostName traceOpts response.ip
                          isSuccess = false }
            | None -> None

    interface System.IDisposable with
        member _.Dispose () =
            if not isDisposed.Value then
                icmpSocket.Dispose()
                udpSocket.Dispose()
                isDisposed.Value <- true
