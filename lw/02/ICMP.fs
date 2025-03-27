module Traceroute.ICMP

open System.Net
open Config
open System.Net.Sockets

let route (config: Config) =
    let ttlFieldSize = config.max_ttl.ToString().Length
    let remoteEP = config.remoteEP :> EndPoint

    use socket =
        new Socket(config.localEP.AddressFamily, SocketType.Raw, ProtocolType.Icmp)

    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.TypeOfService, int config.tos)
    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, int config.first_ttl)
    socket.ReceiveTimeout <- int config.maxwait
