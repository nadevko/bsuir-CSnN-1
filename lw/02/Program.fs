module CSnN1.Lw02.Program

open CSnN1.Lw02.Config

open System
open System.CommandLine

[<EntryPoint>]
let main (args : string[]) : int =
    let rootCommand = new RootCommand "F# implementation of traceroute utility"

    let protoOption = new Option<Protocol> ("--proto", "Set the protocol to use.")
    protoOption.AddAlias "-P"
    protoOption.SetDefaultValue UDP
    rootCommand.AddOption protoOption

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

    let packetLenArgument =
        new Argument<Nullable<uint>> ("packetlen", "The full packet length in bytes")

    packetLenArgument.SetDefaultValue (Nullable<uint> ())
    rootCommand.AddArgument packetLenArgument

    rootCommand.SetHandler (fun (ctx : Invocation.InvocationContext) ->
        let packetLen = ctx.ParseResult.GetValueForArgument packetLenArgument

        try
            let options =
                { Hostname = ctx.ParseResult.GetValueForArgument hostArgument
                  Port = int (ctx.ParseResult.GetValueForOption portOption)
                  SendTimeout = int (ctx.ParseResult.GetValueForOption sendTimeoutOption * 1000.0)
                  ReceiveTimeout = int (ctx.ParseResult.GetValueForOption receiveTimeoutOption * 1000.0)
                  MaxTTL = ctx.ParseResult.GetValueForOption maxHopsOption
                  FirstTTL = ctx.ParseResult.GetValueForOption firstTtlOption
                  Queries = ctx.ParseResult.GetValueForOption queriesOption
                  ResolveNames = not (ctx.ParseResult.GetValueForOption noResolveOption)
                  IpVersion =
                    if ctx.ParseResult.GetValueForOption ipv6Option then IPv6
                    elif ctx.ParseResult.GetValueForOption ipv4Option then IPv4
                    else Any
                  datagramLength = if packetLen.HasValue then int packetLen.Value - 28 else 0 }

            let probe =
                match ctx.ParseResult.GetValueForOption protoOption with
                | _ -> UDP.probe

            Traceroute.trace probe options
            ctx.ExitCode <- 0
        with ex ->
            printfn "Error: %s" ex.Message
            ctx.ExitCode <- 1
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
        let protocol = result.GetValueForOption protoOption

        if protocol <> UDP then
            result.ErrorMessage <- "Only UDP protocol is supported."
    )

    rootCommand.AddValidator (fun result ->
        let packetLen = result.GetValueForArgument packetLenArgument

        if packetLen.HasValue && packetLen.Value <= 28u then
            result.ErrorMessage <- "Packet can't be less than 28 bytes."
    )

    rootCommand.Invoke args
