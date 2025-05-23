module CSnN1.Lw02.ICMP

open CSnN1.Lw02.Config
open CSnN1.Lw02.Checksum

open System.Net
open System.Net.Sockets
open System.Diagnostics

let id = Process.GetCurrentProcess().Id

let tryGetHostName (resolveNames : bool) (ip : IPAddress) =
    if not resolveNames then
        None
    else
        try
            let host = Dns.GetHostEntry ip
            Some host.HostName
        with _ ->
            None

let createIcmpPacket (payloadSize : int) (sequence : int) =
    let packet =
        [| 8uy
           0uy
           0uy
           0uy
           byte (id >>> 8)
           byte id
           byte (sequence >>> 8)
           byte sequence
           yield! [| for _ in 1..payloadSize -> 0uy |] |]

    let checksum = calculateChecksum packet
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
        icmpSocket.ReceiveFrom (buffer, remoteEP) |> ignore
        stopwatch.Stop ()

        let receiveAddress =
            match remoteEP.Value with
            | :? IPEndPoint as ep -> ep.Address
            | _ -> IPAddress.None

        let icmpType = int buffer.[20]
        let icmpCode = int buffer.[21]

        Some
            { ip = receiveAddress
              ms = stopwatch.ElapsedMilliseconds
              buffer = buffer
              icmpType = icmpType
              icmpCode = icmpCode }
    with _ ->
        None

type Prober (options : ProbeOptions) =
    let icmpSocket =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    do
        icmpSocket.ReceiveTimeout <- options.ReceiveTimeout
        icmpSocket.Bind options.LocalEP

    let remoteEP = options.RemoteEP 0

    interface IProber with
        member _.Probe ttl sequence =
            icmpSocket.Ttl <- int16 ttl

            let packet = createIcmpPacket options.PayloadSize sequence
            let stopwatch = new Stopwatch ()

            stopwatch.Start ()
            icmpSocket.SendTo (packet, remoteEP) |> ignore

            match receiveResponse icmpSocket 56 stopwatch with
            | Some response ->
                Some
                    { ttl =
                        (if response.icmpType = 0 then
                            int response.buffer.[26] <<< 8 ||| int response.buffer.[27]
                        else
                            int response.buffer.[54] <<< 8 ||| int response.buffer.[55]) / options.Queries + 1
                      ip = response.ip
                      ms = response.ms
                      hostName = tryGetHostName options.ResolveNames response.ip
                      isSuccess =
                        response.icmpType = 0 && response.icmpCode = 0
                        || Array.exists (fun addr -> addr.Equals response.ip) options.Addresses }
            | _ -> None

    interface System.IDisposable with
        member _.Dispose () = icmpSocket.Dispose ()
