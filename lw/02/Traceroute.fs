module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Net
open System.Net.Sockets

type Traceroute (probeFactory : ProbeFactory, options : TraceOptions) =
    let addresses, remoteIP =
        let resolveHostname (hostname : string) (ipVersion : IpVersion) =
            let hostEntry = Dns.GetHostEntry hostname
            let addresses = hostEntry.AddressList

            if addresses.Length = 0 then
                raise (WebException (sprintf "Could not resolve hostname: %s" hostname))

            let ip =
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
                        WebException (
                            sprintf "No matching %s address found for hostname: %s" (ipVersion.ToString ()) hostname
                        )
                    )

            addresses, ip

        resolveHostname options.Hostname options.IpVersion

    let Send, Receive, Dispose =
        probeFactory
            options
            { LocalEP = new IPEndPoint (options.interfaceIP, 0)
              RemoteEP = fun ttl -> new IPEndPoint (remoteIP, options.Port + ttl)
              Addresses = addresses }

    let padding = options.MaxTTL.ToString().Length

    member private _.PrintHopResult (ttl : int) (result : ProbeResult option) =
        printf "%*d  " padding ttl

        match result with
        | Some (_, addr, elapsed, _) ->
            try
                let hostEntry = Dns.GetHostEntry addr
                printfn "%s  (%s)  %dms" hostEntry.HostName (addr.ToString ()) (int elapsed)
            with _ ->
                printfn "%s  %dms" (addr.ToString ()) (int elapsed)
        | None -> printfn "*"

    member private this.Hop (ttl : int) =
        Send ttl
        let result = Receive()
        this.PrintHopResult ttl result

        let shouldStop =
            match result with
            | Some (_, _, _, isSuccess) -> isSuccess
            | None -> ttl >= options.MaxTTL

        if not shouldStop && ttl < options.MaxTTL then
            this.Hop (ttl + 1)

    member this.Start () =
        try
            printfn
                "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
                options.Hostname
                (remoteIP.ToString ())
                options.MaxTTL
                options.PayloadSize

            if options.FirstTTL <= options.MaxTTL then
                this.Hop options.FirstTTL

        with ex ->
            printfn "Error: %s" ex.Message

    interface IDisposable with
        member _.Dispose () = Dispose ()
