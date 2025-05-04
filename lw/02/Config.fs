module Traceroute.Config

open System.Net

type Protocols =
    | ICMP
    | ICMPv6
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type Config =
    { isIPv6: bool option
      protocol: Protocols
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
