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
      maxwait: float
      herewait: float
      nearwait: float
      sendwait: float
      bytes: uint
      tos: uint8
      first_ttl: uint
      max_ttl: uint
      squeries: uint
      nqueries: uint }
