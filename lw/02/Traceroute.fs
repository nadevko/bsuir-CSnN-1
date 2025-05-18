module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections.Generic

type Traceroute (probeFactory : ProbeFactory, options : TraceOptions) =
    let padding = options.MaxTTL.ToString().Length

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

    let results =
        let dict = new Dictionary<int, Stack<string>> ()

        for i in 1 .. options.MaxTTL do
            dict.[i] <- new Stack<string> ()

        dict

    member this.Start () =
        try
            printfn
                "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
                options.Hostname
                (remoteIP.ToString ())
                options.MaxTTL
                options.PayloadSize

            let rec sendAll ttl remainingQueries =
                if ttl <= options.MaxTTL then
                    let batchSize = min options.Jobs remainingQueries

                    for _ in 1u .. batchSize do
                        this.SendAsync ttl |> Async.Start

                    if remainingQueries > batchSize then
                        sendAll ttl (remainingQueries - batchSize)
                    else
                        Thread.Sleep options.SendTimeout
                        sendAll (ttl + 1) options.Queries

            sendAll options.FirstTTL options.Queries

            this.PrintAsync () |> Async.RunSynchronously
        with ex ->
            printfn "Error: %s" ex.Message

    member private this.SendAsync (ttl : int) =
        async {
            Send ttl
            let result = Receive ()

            let updateTtl =
                match Receive () with
                | None -> ttl
                | Some result -> result.ttl

            results.[updateTtl].Push (this.FormatResult result)
        }

    member private _.FormatResult (result : ProbeResult option) =
        match result with
        | Some result ->
            let address = result.ip.ToString ()
            let fallback () = sprintf "  %s %dms" address result.ms

            if options.ResolveNames then
                try
                    let hostEntry = Dns.GetHostEntry result.ip
                    sprintf "%s (%s) %dms" hostEntry.HostName address result.ms
                with _ ->
                    fallback ()
            else
                fallback ()
        | None -> "*"

    member private this.PrintAsync () =
        async {
            for ttl in options.FirstTTL..options.MaxTTL do
                for query in 1u..options.Queries do
                    while results.[ttl].Count = 0 do
                        Thread.Sleep options.ReceiveTimeout
                    let result = results.[ttl].Pop ()
                    if query = 1u then
                        printf "%*d" padding ttl
                    printf "  %s" result
                    if query = options.Queries then
                        printfn ""
        }

    interface IDisposable with
        member _.Dispose () = Dispose ()
