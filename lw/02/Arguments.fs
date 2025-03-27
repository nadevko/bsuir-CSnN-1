module internal Traceroute.Arguments

open Config
open Argu
open System.Net

type IpVersions =
    | IPv4
    | IPv6
    | Auto

[<CliPrefix(CliPrefix.DoubleDash)>]
[<NoAppSettings>]
type CliArguments =
    | [<AltCommandLine "-i">] Interface of device: string
    | [<AltCommandLine "-v">] IP_Version of version: uint
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
            | IP_Version _ -> "Specify preferred IP version: 4 or 6 (default or any other value means auto)"
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

    let ipVersion =
        match results.TryGetResult IP_Version with
        | Some 4u -> IPv4
        | Some 6u -> IPv6
        | _ -> Auto

    let ip =
        try
            let hostName = results.GetResult Host

            match IPAddress.TryParse(hostName) with
            | true, ip -> ip
            | false, _ ->
                let addresses = Dns.GetHostAddresses(hostName)

                let addresses' =
                    match ipVersion with
                    | Auto -> addresses
                    | IPv4 ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetwork)
                    | IPv6 ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetworkV6)

                addresses'[0]
        with :? Sockets.SocketException as ex ->
            failwithf "Failed to resolve host: %s" ex.Message

    let protocol = results.TryGetResult Protocol |> Option.defaultValue ICMP

    let port =
        results.TryGetResult Port
        |> Option.defaultValue (
            match protocol with
            | ICMP -> 33434u
            | TCP -> 80u
            | UDP -> 53u
            | UDDP -> 33434u
            | UDPLITE -> 53u
        )

    let device: IPAddress option =
        match results.TryGetResult Interface with
        | None -> None
        | Some device ->
            try
                NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                |> Array.find (fun x -> device = x.Id)
                |> fun x -> x.GetIPProperties().UnicastAddresses
                |> Seq.map (fun x -> x.Address)
                |> Seq.find (fun x ->
                    match ipVersion, x.AddressFamily with
                    | Auto, (Sockets.AddressFamily.InterNetwork | Sockets.AddressFamily.InterNetworkV6) -> true
                    | IPv4, Sockets.AddressFamily.InterNetwork -> true
                    | IPv6, Sockets.AddressFamily.InterNetworkV6 -> true
                    | _, _ -> false)
                |> Some
            with :? System.Collections.Generic.KeyNotFoundException as ex ->
                failwith $"Device with ID '{device}' not found."

    { protocol = protocol
      port = int port
      ip = ip
      host = (Dns.GetHostEntry ip).HostName
      device = device
      first_ttl = results.TryGetResult First |> Option.defaultValue 1u
      length = results.TryGetResult Packet_Length |> Option.defaultValue 40u
      max_ttl = results.TryGetResult Max |> Option.defaultValue 30u
      msec = results.TryGetResult Sendwait |> Option.defaultValue 0.0
      nqueries = results.TryGetResult Queries |> Option.defaultValue 3u
      squeries = results.TryGetResult Sim_Queries |> Option.defaultValue 16u
      tos = results.TryGetResult Tos |> Option.defaultValue 0u }
