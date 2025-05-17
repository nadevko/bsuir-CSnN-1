module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Net
open System.Net.Sockets
open System.Threading

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

    let completionEvent = new ManualResetEvent (false)

    let printLock = new Object ()

    member private _.SendAsync (ttl : int) =
        async {
            Send ttl
            let result = Receive ()

            lock
                printLock
                (fun () ->
                    match result with
                    | Some result' ->
                        try
                            let hostEntry = Dns.GetHostEntry result'.ip
                            printfn "%*d  (%s)  %dms" padding ttl hostEntry.HostName (int result'.ms)
                        with _ ->
                            printfn "%*d  %s  %dms" padding ttl (result'.ip.ToString ()) (int result'.ms)
                    | None -> printfn "%*d  *" padding ttl

                    if ttl >= options.MaxTTL then
                        completionEvent.Set () |> ignore
                )
        }
        |> Async.Start

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
                        this.SendAsync ttl

                    if remainingQueries > batchSize then
                        sendAll ttl (remainingQueries - batchSize)
                    else
                        Thread.Sleep options.SendTimeout
                        sendAll (ttl + 1) options.Queries

            sendAll options.FirstTTL options.Queries

            completionEvent.WaitOne () |> ignore

        with ex ->
            printfn "Error: %s" ex.Message

    interface IDisposable with
        member _.Dispose () = Dispose ()
