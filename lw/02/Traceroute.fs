module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets
open System.Text

let resolveHostname (hostname : string) (ipVersion : IpVersion) : IPAddress array * IPAddress =
    let tryGetAddr (hostEntry : IPHostEntry) (family : System.Net.Sockets.AddressFamily) =
        hostEntry.AddressList |> Array.tryFind (fun ip -> ip.AddressFamily = family)

    try
        let hostEntry = Dns.GetHostEntry (hostname)

        if hostEntry.AddressList.Length = 0 then
            raise (System.Net.WebException (sprintf "Could not resolve hostname: %s" hostname))

        let allAddresses = hostEntry.AddressList

        let selectedIp =
            match ipVersion with
            | IPv4 ->
                match tryGetAddr hostEntry System.Net.Sockets.AddressFamily.InterNetwork with
                | Some ip -> ip
                | None -> raise (System.Net.WebException (sprintf "No IPv4 address found for hostname: %s" hostname))

            | IPv6 ->
                match tryGetAddr hostEntry System.Net.Sockets.AddressFamily.InterNetworkV6 with
                | Some ip -> ip
                | None -> raise (System.Net.WebException (sprintf "No IPv6 address found for hostname: %s" hostname))

            | Auto ->
                match tryGetAddr hostEntry System.Net.Sockets.AddressFamily.InterNetwork with
                | Some ip -> ip
                | None ->
                    match tryGetAddr hostEntry System.Net.Sockets.AddressFamily.InterNetworkV6 with
                    | Some ip -> ip
                    | None -> raise (System.Net.WebException (sprintf "No IP address found for hostname: %s" hostname))

        (allAddresses, selectedIp)

    with
    | :? System.Net.WebException as ex -> raise ex
    | ex -> raise (System.Net.WebException (sprintf "Error resolving hostname: %s. %s" hostname ex.Message, ex))

let trace (probe : Probe) (options : TracerouteOptions) : unit =
    let hostname = options.Hostname
    let maxTtl = options.MaxTTL
    let firstTtl = options.FirstTTL
    let receiveTimeout = options.ReceiveTimeout

    let shouldEndTrace (result : ProbeResult) (ttl : uint16) =
        match result with
        | Some (_, _, icmpType, icmpCode, isTarget) -> isTarget || (icmpType = 3 && icmpCode = 3)
        | None -> ttl >= maxTtl - 2us

    let printHopResult (ttl : uint16) (result : ProbeResult) =
        printf "%2d  " ttl

        match result with
        | Some (addr, elapsed, icmpType, icmpCode, isTarget) ->
            if icmpType = 3 || icmpType = 11 || isTarget then
                printfn "%s  %dms  [ICMP Type:%d Code:%d]" (addr.ToString ()) (int elapsed) icmpType icmpCode
            else
                printfn "%s  %dms" (addr.ToString ()) (int elapsed)

            if isTarget || (icmpType = 3 && icmpCode = 3) then
                printfn "Trace complete."
        | None ->
            if ttl >= maxTtl - 2us then
                printfn "*     Request timed out. (Possibly reached target but no ICMP response)"
                printfn "Many hosts block ICMP 'Destination Unreachable' messages."
                printfn "Trace probably complete."
            else
                printfn "*     Request timed out."

    try
        let (allAddresses, targetIp) = resolveHostname hostname options.IpVersion

        printfn "Tracing route to %s [%s]" hostname (targetIp.ToString ())

        if allAddresses.Length > 1 then
            printfn
                "Host has multiple addresses: %s"
                (allAddresses |> Array.map (fun ip -> ip.ToString ()) |> String.concat ", ")

        printfn "over a maximum of %d hops:" maxTtl

        let rec traceHop ttl =
            if ttl > maxTtl then
                printfn "Trace terminated: reached maximum number of hops."
            else
                let result = probe ttl targetIp allAddresses receiveTimeout

                printHopResult ttl result

                if not (shouldEndTrace result ttl) then
                    traceHop (ttl + 1us)

        traceHop firstTtl

    with ex ->
        printfn "Error: %s" ex.Message
