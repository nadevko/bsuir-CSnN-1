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
    abstract member Probe : int -> ProbeResult option
    inherit System.IDisposable

type TraceOptions =
    { Hostname : string
      Protocol : Protocol
      Queries : int
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
      Addresses : IPAddress array }
