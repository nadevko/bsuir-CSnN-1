module CSnN1.Lw02.Config

open System.Net

type IpVersion =
    | Any
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP

type ProbeResult =
    { ttl : int
      ip : IPAddress
      ms : int64
      isSuccess : bool }

type TraceOptions =
    { Hostname : string
      FirstTTL : int
      MaxTTL : int
      Port : int
      SendTimeout : int
      ReceiveTimeout : int
      Queries : uint
      ResolveNames : bool
      IpVersion : IpVersion
      PayloadSize : int
      interfaceIP : IPAddress
      Jobs : uint }

type ProbeOptions =
    { LocalEP : IPEndPoint
      RemoteEP : int -> IPEndPoint
      Addresses : IPAddress array }

type ProbeFactory = TraceOptions -> ProbeOptions -> (int -> unit) * (unit -> ProbeResult option) * (unit -> unit)
