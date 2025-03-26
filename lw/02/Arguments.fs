module internal Traceroute.Arguments

open Argu

type Protocols =
    | ICMP
    | TCP
    | UDP
    | UDDP
    | UDPLITE

type ResolutionModes =
    | IP
    | IPv4
    | IPv6
    | DNS
    | Auto

[<CliPrefix(CliPrefix.DoubleDash)>]
[<NoAppSettings>]
type CliArguments =
    | [<AltCommandLine "-i">] Interface of device: string
    | [<AltCommandLine "-r">] Resolve of mode: ResolutionModes
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
            | Resolve _ -> "Set the host resolution mode: ip (ipv4 or ipv6), ipv4, ipv6, dns, auto (ip or dns, default)"
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

let configure argv =
    (ArgumentParser
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
        argv)
        .GetAllResults()
