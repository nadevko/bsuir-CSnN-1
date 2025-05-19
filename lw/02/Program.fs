module CSnN1.Lw02.Program

open CSnN1.Lw02.Config

open System
open System.CommandLine
open System.Net
open System.Net.NetworkInformation
open System.Collections.Generic
open Traceroute

let rec checkIPVersion (ipVersion : IpVersion) (ipAddress : IPAddress) =
    match ipVersion with
    | Any -> checkIPVersion IPv4 ipAddress || checkIPVersion IPv6 ipAddress
    | IPv4 -> ipAddress.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork
    | IPv6 -> ipAddress.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6

let getNetworkInterfaces (ipVersion : IpVersion) =
    let interfaces = new Dictionary<string, IPAddress> ()

    try
        NetworkInterface.GetAllNetworkInterfaces ()
        |> Array.filter (fun netInterface -> netInterface.OperationalStatus = OperationalStatus.Up)
        |> Array.iter (fun netInterface ->
            let properties = netInterface.GetIPProperties ()

            properties.UnicastAddresses
            |> Seq.filter (fun unicast -> checkIPVersion ipVersion unicast.Address)
            |> Seq.iter (fun unicast -> interfaces[netInterface.Name] <- unicast.Address)
        )
    with ex ->
        printfn "Error getting network interfaces: %s" ex.Message

    interfaces

[<EntryPoint>]
let main (args : string[]) : int =
    let rootCommand = new RootCommand "F# implementation of traceroute utility"

    let icmpOption = new Option<bool> ("--icmp", "Set the ICMP protocol to use.")
    icmpOption.AddAlias "-I"
    rootCommand.AddOption icmpOption

    let udpOption = new Option<bool> ("--udp", "Set the UDP protocol to use (default).")
    udpOption.AddAlias "-U"
    rootCommand.AddOption udpOption

    let ipv4Option = new Option<bool> ("--ipv4", "Use IPv4")
    ipv4Option.AddAlias "-4"
    rootCommand.AddOption ipv4Option

    let ipv6Option = new Option<bool> ("--ipv6", "Use IPv6")
    ipv6Option.AddAlias "-6"
    rootCommand.AddOption ipv6Option

    let firstTtlOption = new Option<uint16> ("--first", "Start from the first_ttl hop")
    firstTtlOption.AddAlias "-f"
    firstTtlOption.SetDefaultValue 1us
    rootCommand.AddOption firstTtlOption

    let maxHopsOption =
        new Option<uint16> ("--max", "Set the max number of hops (max TTL to be reached).")

    maxHopsOption.AddAlias "-m"
    maxHopsOption.SetDefaultValue 30us
    rootCommand.AddOption maxHopsOption

    let noResolveOption =
        new Option<bool> ("--no-resolve", "Do not resolve IP addresses to their domain names")

    noResolveOption.AddAlias "-n"
    rootCommand.AddOption noResolveOption

    let portOption = new Option<uint16> ("--port", "Set the destination port to use.")
    portOption.AddAlias "-p"
    portOption.SetDefaultValue 33434us
    rootCommand.AddOption portOption

    let queriesOption =
        new Option<uint> ("--queries", "Set the number of probes per each hop.")

    queriesOption.AddAlias "-q"
    queriesOption.SetDefaultValue 3u
    rootCommand.AddOption queriesOption

    let jobsOption = new Option<int> ("--jobs", "Set the number of parallel probes")
    jobsOption.AddAlias "-j"
    jobsOption.SetDefaultValue 6
    rootCommand.AddOption jobsOption

    let sendTimeoutOption =
        new Option<float> ("--send", "Maximum time in seconds to wait for sending packets")

    sendTimeoutOption.AddAlias "-s"
    rootCommand.AddOption sendTimeoutOption

    let receiveTimeoutOption =
        new Option<float> ("--receive", "Maximum time in seconds to wait for receiving responses")

    receiveTimeoutOption.AddAlias "-r"
    receiveTimeoutOption.SetDefaultValue 0.5
    rootCommand.AddOption receiveTimeoutOption

    let hostArgument = new Argument<string> ("host", "The host to traceroute to")
    rootCommand.AddArgument hostArgument

    let packetSizeArgument =
        new Argument<Nullable<uint>> ("packetsize", "The payload length in bytes")

    packetSizeArgument.SetDefaultValue (Nullable<uint> 28u)
    rootCommand.AddArgument packetSizeArgument

    let interfaceOption =
        new Option<string> ("--interface", "Set the network interface to use for sending packets")

    interfaceOption.AddAlias "-i"
    rootCommand.AddOption interfaceOption


    rootCommand.SetHandler (fun (ctx : Invocation.InvocationContext) ->
        let packetLen = ctx.ParseResult.GetValueForArgument packetSizeArgument

        try
            let interfaceIP = ctx.ParseResult.GetValueForOption interfaceOption

            let IpVersion =
                if ctx.ParseResult.GetValueForOption ipv6Option then IPv6
                elif ctx.ParseResult.GetValueForOption ipv4Option then IPv4
                else Any

            let interfaces = getNetworkInterfaces IpVersion

            let options =
                { Hostname = ctx.ParseResult.GetValueForArgument hostArgument
                  Protocol =
                    if ctx.ParseResult.GetValueForOption icmpOption then
                        ICMP
                    else
                        UDP
                  Port = int (ctx.ParseResult.GetValueForOption portOption)
                  SendTimeout = int (ctx.ParseResult.GetValueForOption sendTimeoutOption * 1000.0)
                  ReceiveTimeout = int (ctx.ParseResult.GetValueForOption receiveTimeoutOption * 1000.0)
                  MaxTTL = int (ctx.ParseResult.GetValueForOption maxHopsOption)
                  FirstTTL = int (ctx.ParseResult.GetValueForOption firstTtlOption)
                  Jobs = int (ctx.ParseResult.GetValueForOption queriesOption)
                  Queries = int (ctx.ParseResult.GetValueForOption queriesOption)
                  ResolveNames = not (ctx.ParseResult.GetValueForOption noResolveOption)
                  IpVersion = IpVersion
                  PayloadSize = if packetLen.HasValue then int packetLen.Value - 28 else 0
                  interfaceIP =
                    if String.IsNullOrEmpty interfaceIP then
                        IPAddress.Any
                    else
                        interfaces.[interfaceIP] }

            trace options
            ctx.ExitCode <- 0
        with ex ->
            printfn "Error: %s" ex.Message
            ctx.ExitCode <- 1
    )

    rootCommand.AddValidator (fun result ->
        let selectedInterface = result.GetValueForOption interfaceOption
        let interfaces = getNetworkInterfaces Any

        if not (String.IsNullOrEmpty selectedInterface) then
            if not (interfaces.ContainsKey selectedInterface) then
                result.ErrorMessage <-
                    sprintf
                        "Specified interface '%s' not found. Valid interfaces: %s"
                        selectedInterface
                        (String.Join (", ", interfaces.Keys))
    )

    rootCommand.AddValidator (fun result ->
        if result.GetValueForArgument hostArgument = null then
            result.ErrorMessage <- "The host argument is required."
    )

    rootCommand.AddValidator (fun result ->
        let firstTtl = result.GetValueForOption firstTtlOption
        let maxHops = result.GetValueForOption maxHopsOption

        if firstTtl > maxHops then
            result.ErrorMessage <- "First TTL must be less than or equal to max hops."
    )

    rootCommand.AddValidator (fun result ->
        let queries = result.GetValueForOption queriesOption

        if queries <= 0u then
            result.ErrorMessage <- "Number of queries must be greater than 0."
    )

    rootCommand.AddValidator (fun result ->
        let sendWait = result.GetValueForOption sendTimeoutOption
        let receiveWait = result.GetValueForOption receiveTimeoutOption

        if sendWait < 0.0 then
            result.ErrorMessage <- "Send timeout must be non-negative."
        elif receiveWait < 0.0 then
            result.ErrorMessage <- "Receive timeout must be non-negative."
    )

    rootCommand.AddValidator (fun result ->
        let ipv4 = result.GetValueForOption ipv4Option
        let ipv6 = result.GetValueForOption ipv6Option

        if ipv4 && ipv6 then
            result.ErrorMessage <- "Cannot use both IPv4 and IPv6 options at the same time."
    )

    rootCommand.AddValidator (fun result ->
        let packetSize = result.GetValueForArgument packetSizeArgument

        if packetSize.HasValue && packetSize.Value < 28u then
            result.ErrorMessage <- "Packet size must be at least 28 bytes."
    )

    rootCommand.AddValidator (fun result ->
        let icmp = result.GetValueForOption icmpOption
        let udp = result.GetValueForOption udpOption

        if icmp && udp then
            result.ErrorMessage <- "Cannot use both ICMP or UDP protocols at the same time."
    )

    rootCommand.Invoke args
