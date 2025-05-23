module CSnN1.Lw02.Config

open System.Net

type Protocol =
    | ICMP = 1uy
    | UDP = 17uy
    | Auto = 0uy

type ProbeResult =
    { ttl : int
      ip : IPAddress
      ms : int64
      hostName : string option
      isSuccess : bool }

type IProber =
    abstract member Probe : int -> int -> ProbeResult option
    inherit System.IDisposable

type TraceOptions =
    { Hostname : string
      Protocol : Protocol
      Jobs : int }

type ProbeOptions =
    { LocalEP : IPEndPoint
      RemoteEP : int -> IPEndPoint
      SendTimeout : int
      ReceiveTimeout : int
      FirstTTL : int
      MaxTTL : int
      PayloadSize : int
      ResolveNames : bool
      Queries : int
      Addresses : IPAddress array }

type Pool private () =
    static let mutable icmpSocket : Sockets.Socket option = None
    static let mutable udpSocket : Sockets.Socket option = None
    static let locker = obj ()

    static member GetIcmpSocket addressFamily =
        lock
            locker
            (fun () ->
                match icmpSocket with
                | Some socket -> socket
                | _ ->
                    let socket =
                        new Sockets.Socket (addressFamily, Sockets.SocketType.Raw, Sockets.ProtocolType.Icmp)

                    icmpSocket <- Some socket
                    socket
            )

    static member GetUdpSocket addressFamily =
        lock
            locker
            (fun () ->
                match udpSocket with
                | Some socket -> socket
                | _ ->
                    let socket =
                        new Sockets.Socket (addressFamily, Sockets.SocketType.Dgram, Sockets.ProtocolType.Udp)

                    udpSocket <- Some socket
                    socket
            )

    static member Dispose () =
        lock
            locker
            (fun () ->
                icmpSocket
                |> Option.iter (fun s ->
                    try
                        s.Close ()
                    with _ ->
                        ()
                )

                udpSocket
                |> Option.iter (fun s ->
                    try
                        s.Close ()
                    with _ ->
                        ()
                )

                icmpSocket <- None
                udpSocket <- None
            )
