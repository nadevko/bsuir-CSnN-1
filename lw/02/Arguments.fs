module internal Traceroute.Arguments

open Argu
open System.Net

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type IpVersions =
    | V4
    | V6
    | Auto

[<CliPrefix(CliPrefix.DoubleDash)>]
[<NoAppSettings>]
type CliArguments =
    | [<AltCommandLine "-i">] Interface of device: string
    | [<AltCommandLine "-v">] IP_Version of version: IpVersions
    | [<AltCommandLine "-t">] Tos of tos: uint
    | [<AltCommandLine "-P">] Protocol of protocol: Protocols
    | [<AltCommandLine "-p">] Port of port: uint
    | [<AltCommandLine "-f">] First of first_ttl: uint
    | [<AltCommandLine "-m">] Max of max_ttl: uint
    | [<AltCommandLine "-Q">] Sim_Queries of squeries: uint
    | [<AltCommandLine "-q">] Queries of nqueries: uint
    | [<AltCommandLine "-W">] Sendwait of msec: float
    | [<AltCommandLine "-l">] Packet_Length of length: uint
    | [<MainCommandAttribute; ExactlyOnce>] Host of host: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Interface _ -> "Specify a network interface to operate with"
            | IP_Version _ -> "Specify preferred IP version to resolve hostnames: v4, v6, auto"
            | Tos _ -> "Set the TOS (IPv4 type of service) or TC (IPv6 traffic class) value for outgoing packets"
            | Protocol _ -> "Set the protocol to be used for traceroute operations: icmp, tcp, udp, uddp, udplite"
            | Port _ -> "Set the destination port to use"
            | First _ -> "Set the initial TTL value"
            | Max _ -> "Set the max number of hops (max TTL to be reached)"
            | Sim_Queries _ -> "Set the number of probes to be tried simultaneously"
            | Queries _ -> "Set the number of probes per each hop"
            | Sendwait _ -> "Minimal time interval between probes"
            | Packet_Length _ -> "Set the packet length for outgoing packets"
            | Host _ -> "The host to trace the route to"

type Config =
    { device: string
      protocol: Protocols
      tos: uint option
      port: uint option
      first_ttl: uint
      max_ttl: uint
      squeries: uint
      nqueries: uint
      msec: float
      length: uint
      host: IPHostEntry }

let configure argv =
    let results =
        ArgumentParser
            .Create<CliArguments>(
                programName = "traceroute",
                errorHandler =
                    ProcessExiter(
                        colorizer =
                            function
                            | ErrorCode.HelpText -> None
                            | _ -> Some System.ConsoleColor.Red
                    ),
                checkStructure =
#if DEBUG
                    true
#else
                    false
#endif
            )
            .ParseCommandLine
            argv

    let device =
        results.TryGetResult Interface
        |> Option.defaultValue
            (NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
             |> Array.filter (fun x -> x.NetworkInterfaceType <> NetworkInformation.NetworkInterfaceType.Loopback)
             |> Array.head)
                .Name

    let ipVersion = results.TryGetResult IP_Version |> Option.defaultValue Auto

    let host =
        try
            let hostName = results.GetResult Host

            match IPAddress.TryParse(hostName) with
            | true, ip -> ip
            | false, _ ->
                let addresses = Dns.GetHostAddresses(hostName)

                let addresses' =
                    match ipVersion with
                    | Auto -> addresses
                    | V4 ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetwork)
                    | V6 ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetworkV6)

                addresses'[0]
            |> Dns.GetHostEntry
        with :? Sockets.SocketException as ex ->
            failwithf "Failed to resolve host: %s" ex.Message

    { device = device
      tos = results.TryGetResult Tos
      protocol = results.TryGetResult Protocol |> Option.defaultValue ICMP
      port = results.TryGetResult Port
      first_ttl = results.TryGetResult First |> Option.defaultValue 1u
      max_ttl = results.TryGetResult Max |> Option.defaultValue 30u
      squeries = results.TryGetResult Sim_Queries |> Option.defaultValue 16u
      nqueries = results.TryGetResult Queries |> Option.defaultValue 3u
      msec = results.TryGetResult Sendwait |> Option.defaultValue 0.0
      length = results.TryGetResult Packet_Length |> Option.defaultValue 40u
      host = host }