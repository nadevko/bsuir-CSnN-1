module Traceroute.Config

open System.Net

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type Config =
    { protocol: Protocols
      localEP: IPEndPoint
      remoteEP: IPEndPoint
      timeout: int
      bytes: int
      tos: int
      first_ttl: int
      max_ttl: int
      queries: int }
