module CSnN1.Lw02.Config

type IpVersion =
    | Auto
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP
    | TCP

type TracerouteOptions =
    { Hostname : string
      MaxHops : uint
      Port : uint16
      SendTimeout : int
      ReceiveTimeout : int
      FirstTtl : uint
      Queries : uint
      ResolveNames : bool
      IpVersion : IpVersion
      PacketLen : uint }

type Traceroute = TracerouteOptions -> unit
