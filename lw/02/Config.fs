module CSnN1.Lw02.Config

open System.Net
open System

type IpVersion =
    | Auto
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP
    | TCP

type ProbeResult = (IPAddress * int64 * int * int * bool) option
type Probe = uint16 -> IPAddress -> IPAddress array -> int -> ProbeResult

type TracerouteOptions =
    { Hostname : string
      MaxTTL : uint16
      Port : uint16
      SendTimeout : int
      ReceiveTimeout : int
      FirstTTL : uint16
      Queries : uint
      ResolveNames : bool
      IpVersion : IpVersion
      PacketLen : uint }

type Traceroute = TracerouteOptions -> unit
