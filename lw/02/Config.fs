module Traceroute.Config

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type Config =
    { protocol: Protocols
      localEP: System.Net.IPEndPoint option
      remoteEP: System.Net.IPEndPoint
      msec: float
      bytes: uint
      tos: uint8
      first_ttl: uint
      max_ttl: uint
      squeries: uint
      nqueries: uint
      }
