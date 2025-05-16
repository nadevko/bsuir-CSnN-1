module CSnN1.Lw02.Config

open System.Net

type IpVersion =
    | Any
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP
    | TCP

type ProbeResult = (IPAddress * int64 * int * int * bool) option

type TracerouteOptions =
    { Hostname : string
      FirstTTL : int
      MaxTTL : int
      Port : int
      SendTimeout : int
      ReceiveTimeout : int
      Queries : uint
      ResolveNames : bool
      IpVersion : IpVersion
      datagramLength : int }

type Probe = TracerouteOptions -> IPAddress -> IPAddress array -> int -> ProbeResult
