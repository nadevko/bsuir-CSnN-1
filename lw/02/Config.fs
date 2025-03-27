module Traceroute.Config

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type Config =
    { device: System.Net.IPAddress option
      protocol: Protocols
      tos: uint
      port: int
      first_ttl: uint
      max_ttl: uint
      squeries: uint
      nqueries: uint
      msec: float
      length: uint
      ip: System.Net.IPAddress
      host: string }
