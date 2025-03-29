module internal Traceroute.Arguments

open Config
open Argu
open System.Net

[<CliPrefix(CliPrefix.DoubleDash)>]
[<NoAppSettings>]
type CliArguments =
    | [<AltCommandLine "-i">] Interface of device: string
    | [<AltCommandLine "-v">] IP_Version of version: uint8
    | [<AltCommandLine "-t">] Tos of tos: uint8
    | [<AltCommandLine "-P">] Protocol of protocol: Protocols
    | [<AltCommandLine "-p">] Port of port: uint16
    | [<AltCommandLine "-f">] First of first_ttl: uint16
    | [<AltCommandLine "-m">] Max of max_ttl: uint16
    | [<AltCommandLine "-q">] Queries of nqueries: int
    | [<AltCommandLine "-s">] Send_Timeout of sec: float
    | [<AltCommandLine "-r">] Receive_Timeout of sec: float
    | [<AltCommandLine "-b">] Bytes of length: uint
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
            | Queries _ -> "Set the number of probes per each hop"
            | Send_Timeout _ -> "Maximum time to send for each probe"
            | Receive_Timeout _ -> "Maximum time to receive for each probe"
            | Bytes _ -> "How many bytes should be sent in each probe"
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
        | Some 4uy -> Some Sockets.AddressFamily.InterNetwork
        | Some 6uy -> Some Sockets.AddressFamily.InterNetworkV6
        | _ -> None

    let protocol = results.TryGetResult Protocol |> Option.defaultValue ICMP

    let localEPAdress =
        match results.TryGetResult Interface with
        | None -> IPAddress.Any
        | Some device ->
            try
                NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                |> Array.find (fun x -> device = x.Id)
                |> fun x -> x.GetIPProperties().UnicastAddresses
                |> Seq.map (fun x -> x.Address)
                |> match ipVersion with
                   | None -> Seq.head
                   | Some af -> Seq.find (fun x -> af = x.AddressFamily)
            with :? System.Collections.Generic.KeyNotFoundException as ex ->
                failwith $"Device with ID '{device}' not found."

    let remote =
        try
            let hostName = results.GetResult Host

            match IPAddress.TryParse(hostName) with
            | true, ip -> ip
            | false, _ ->
                let addresses = Dns.GetHostAddresses hostName

                let addresses' =
                    match ipVersion with
                    | Some Sockets.AddressFamily.InterNetwork ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetwork)
                    | Some Sockets.AddressFamily.InterNetworkV6 ->
                        addresses
                        |> Array.filter (fun x -> x.AddressFamily = Sockets.AddressFamily.InterNetworkV6)
                    | _ -> addresses

                addresses'[0]
        with :? Sockets.SocketException as ex ->
            failwithf "Failed to resolve host: %s" ex.Message

    let port =
        results.TryGetResult Port
        |> Option.defaultValue (
            match protocol with
            | ICMP -> 33434us
            | TCP -> 80us
            | UDP -> 53us
            | UDDP -> 33434us
            | UDPLITE -> 53us
        )

    { protocol = protocol
      localEP = IPEndPoint(localEPAdress, 0)
      remoteEP = IPEndPoint(remote, int port)

      sendTime = 1000 * int (results.TryGetResult Send_Timeout |> Option.defaultValue 3.0)
      receiveTime = 1000 * int (results.TryGetResult Receive_Timeout |> Option.defaultValue 3.0)

      bytes = int (results.TryGetResult Bytes |> Option.defaultValue 40u)
      tos = int (results.TryGetResult Tos |> Option.defaultValue 0uy)

      first_ttl = results.TryGetResult First |> Option.defaultValue 1us
      max_ttl = results.TryGetResult Max |> Option.defaultValue 30us
      queries = results.TryGetResult Queries |> Option.defaultValue 3 }
