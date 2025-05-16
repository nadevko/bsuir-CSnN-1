module CSnN1.Lw02.Config

open System.Net
open System.Diagnostics

let pid = Process.GetCurrentProcess().Id

type IpVersion =
    | Any
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP

type ProbeResult = (IPAddress * int64 * int * int * bool) option

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
      PayloadSize : int }

type ProbeOptions =
    { LocalEP : IPEndPoint
      RemoteEP : int -> IPEndPoint
      Addresses : IPAddress array }

type Probe = TraceOptions -> ProbeOptions -> (int -> ProbeResult) * (unit -> unit)
