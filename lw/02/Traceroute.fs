module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets

let resolveHostname (hostname : string) (ipVersion : IpVersion) =
    let hostEntry = Dns.GetHostEntry hostname
    let addresses = hostEntry.AddressList

    if addresses.Length = 0 then
        raise (WebException (sprintf "Could not resolve hostname: %s" hostname))

    let selectedIp =
        match
            addresses
            |> Array.tryFind (fun ip -> ip.ToString () = hostname)
            |> Option.orElse (
                addresses
                |> Array.tryFind (fun ip ->
                    match ipVersion with
                    | Any -> true
                    | IPv4 when ip.AddressFamily = AddressFamily.InterNetwork -> true
                    | IPv6 when ip.AddressFamily = AddressFamily.InterNetworkV6 -> true
                    | _ -> false
                )
            )
        with
        | Some ip -> ip
        | None ->
            raise (
                WebException (sprintf "No matching %s address found for hostname: %s" (ipVersion.ToString ()) hostname)
            )

    addresses, selectedIp

let trace (probe : Probe) (options : TraceOptions) =
    let nSpace = options.MaxTTL.ToString().Length

    let mustEndTrace (result : ProbeResult) (ttl : int) =
        match result with
        | Some (_, _, icmpType, icmpCode, isTarget) -> isTarget || icmpType = 3 && icmpCode = 3
        | None -> ttl > options.MaxTTL

    let printHopResult (ttl : int) (result : ProbeResult) =
        printf "%*d  " nSpace ttl

        match result with
        | Some (addr, elapsed, _, _, _) ->
            try
                let hostEntry = Dns.GetHostEntry addr
                printfn "%s  (%s)  %dms" hostEntry.HostName (addr.ToString ()) (int elapsed)
            with _ ->
                printfn "%s  %dms" (addr.ToString ()) (int elapsed)
        | None -> printfn "*"

    try
        let allAddresses, targetIp = resolveHostname options.Hostname options.IpVersion

        printfn
            "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
            options.Hostname
            (targetIp.ToString ())
            options.MaxTTL
            options.PayloadSize

        let probe', dispose =
            probe
                options
                { LocalEP = new IPEndPoint (options.interfaceIP, 0)
                  RemoteEP = fun ttl -> new IPEndPoint (targetIp, options.Port + ttl)
                  Addresses = allAddresses }

        let rec traceHop ttl =
            let result = probe' ttl

            printHopResult ttl result

            if not (mustEndTrace result ttl) then
                traceHop (ttl + 1)

        if options.FirstTTL <= options.MaxTTL then
            traceHop options.FirstTTL

        dispose ()

    with ex ->
        printfn "Error: %s" ex.Message
