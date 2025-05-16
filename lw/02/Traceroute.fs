module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

let resolveHostname (hostname : string) (ipVersion : IpVersion) =
    let hostEntry = Dns.GetHostEntry hostname
    let allAddresses = hostEntry.AddressList

    if allAddresses.Length = 0 then
        raise (WebException (sprintf "Could not resolve hostname: %s" hostname))

    let selectedIp =
        match
            allAddresses
            |> Array.tryFind (fun ip ->
                match ipVersion with
                | Any -> true
                | IPv4 when ip.AddressFamily = AddressFamily.InterNetwork -> true
                | IPv6 when ip.AddressFamily = AddressFamily.InterNetworkV6 -> true
                | _ -> false
            )
        with
        | Some ip -> ip
        | None ->
            raise (
                WebException (sprintf "No matching %s address found for hostname: %s" (ipVersion.ToString ()) hostname)
            )

    allAddresses, selectedIp

let trace (probe : Probe) (options : TracerouteOptions) =
    let nSpace = options.MaxTTL.ToString().Length

    let shouldEndTrace (result : ProbeResult) (ttl : int) =
        match result with
        | Some (_, _, icmpType, icmpCode, isTarget) -> isTarget || icmpType = 3 && icmpCode = 3
        | None -> ttl >= options.MaxTTL - 2

    let printHopResult (ttl : int) (result : ProbeResult) =
        printf "%*d  " nSpace ttl

        match result with
        | Some (addr, elapsed, icmpType, icmpCode, isTarget) -> printfn "%s  %dms" (addr.ToString ()) (int elapsed)
        | None -> printfn "*     Request timed out."

    try
        let allAddresses, targetIp = resolveHostname options.Hostname options.IpVersion

        printfn
            "traceroute to %s (%s), %i hops max, %i byte packets"
            options.Hostname
            (targetIp.ToString ())
            options.MaxTTL
            options.datagramLength

        let rec traceHop ttl =
            if ttl > options.MaxTTL then
                printfn "Trace terminated: reached maximum number of hops."
                ()

            let result = probe options targetIp allAddresses ttl

            printHopResult ttl result

            if not (shouldEndTrace result ttl) then
                traceHop (ttl + 1)

        traceHop options.FirstTTL

    with ex ->
        printfn "Error: %s" ex.Message
