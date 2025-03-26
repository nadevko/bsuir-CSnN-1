module Traceroute.Tracert

open System
open System.Net

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type Config =
    { device: string
      protocol: Protocols
      tos: uint
      port: uint
      first_ttl: uint
      max_ttl: uint
      squeries: uint
      nqueries: uint
      msec: float
      length: uint
      host: IPHostEntry }

let run (cfg: Config) =
    match cfg.protocol with
    | ICMP -> raise (NotImplementedException "ICMP is not implemented")
    | TCP -> raise (NotImplementedException "TCP is not implemented")
    | UDP -> raise (NotImplementedException "UDP is not implemented")
    | UDDP -> raise (NotImplementedException "UDDP is not implemented")
    | UDPLITE -> raise (NotImplementedException "UDP lite is not implemented")
