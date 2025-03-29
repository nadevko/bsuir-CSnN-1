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
      sendTime: int
      receiveTime: int
      bytes: int
      tos: int
      first_ttl: uint16
      max_ttl: uint16
      queries: int }

let makeHostIPPair (ip: IPAddress) =
    try
        (System.Net.Dns.GetHostEntry ip).HostName + " (" + (ip.ToString()) + ")"
    with :? Sockets.SocketException ->
        ip.ToString()
