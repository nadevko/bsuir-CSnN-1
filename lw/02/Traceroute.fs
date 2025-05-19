module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

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
                    | IPv6 when ip.AddressFamily = AddressFamily.InterNetworkV6 -> true
                    | _ -> true
                )
            )
        with
        | Some ip -> ip
        | None ->
            raise (
                WebException (sprintf "No matching %s address found for hostname: %s" (ipVersion.ToString ()) hostname)
            )

    addresses, ip

let trace (options : TraceOptions) =
    let padding = options.MaxTTL.ToString().Length
    let printLock = new Object ()

    let cancellationEvent = new ManualResetEvent false
    let printEvent = new Event<unit> ()

    let mutable currentTtl = options.FirstTTL
    let mutable currentQuery = 1
    let mutable printed = Set.empty<string>

    let addresses, remoteIP = resolveHostname options.Hostname options.IpVersion

    let probeOptions =
        { LocalEP = new IPEndPoint (options.interfaceIP, 0)
          RemoteEP = fun ttl -> new IPEndPoint (remoteIP, options.Port + ttl)
          Addresses = addresses }

    let prober =
        match options.Protocol with
        | ICMP -> new ICMP.Prober (options, probeOptions) :> IProber
        | _ -> new UDP.Prober (options, probeOptions) :> IProber

    let results =
        let dict = new Dictionary<int, List<ProbeResult option>> ()

        for i in 1 .. options.MaxTTL do
            dict.[i] <- new ResizeArray<ProbeResult option> ()

        dict

    let printResults () : Task =
        task {
            if results.[currentTtl].Count < currentQuery then
                return ()

            lock
                printLock
                (fun () ->
                    if currentQuery = 1 then
                        printed <- Set.empty<string>
                        printf "%*d" padding currentTtl

                    match results.[currentTtl].[currentQuery - 1] with
                    | None -> printf "  *"
                    | Some result ->
                        let ip = result.ip.ToString ()

                        if result.hostName.IsSome && not (printed.Contains result.hostName.Value) then
                            printf "  %s" result.hostName.Value
                            printed <- printed.Add result.hostName.Value

                            if not (printed.Contains ip) then
                                printf " (%s)" ip
                                printed <- printed.Add ip
                        elif not (printed.Contains ip) then
                            printf "  %s" ip
                            printed <- printed.Add ip
                        else
                            printf " "

                        printed <- printed.Add ip
                        printf " %dms" result.ms

                    if currentQuery = options.Queries then
                        printfn ""

                        match results.[currentTtl].[currentQuery - 1] with
                        | Some result when result.isSuccess -> cancellationEvent.Set () |> ignore
                        | _ -> ()

                        currentTtl <- currentTtl + 1
                        currentQuery <- 1

                        if currentTtl > options.MaxTTL then
                            cancellationEvent.Set () |> ignore
                    else
                        currentQuery <- currentQuery + 1
                )
        }

    let rec sendProbes ttl remainingQueries =
        if ttl <= options.MaxTTL && cancellationEvent.WaitOne 0 |> not then
            let batchSize = min options.Jobs remainingQueries

            [ for _ in 1..batchSize do
                  let result = prober.Probe ttl

                  let updateTtl =
                      match result with
                      | None -> ttl
                      | Some result -> result.ttl

                  lock printLock (fun () -> results.[updateTtl].Add result)
                  printEvent.Trigger () ]
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore

            if remainingQueries > batchSize then
                sendProbes ttl (remainingQueries - batchSize)
            else
                Thread.Sleep options.SendTimeout
                sendProbes (ttl + 1) options.Queries

    try
        printfn
            "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
            options.Hostname
            (remoteIP.ToString ())
            options.MaxTTL
            options.PayloadSize

        printEvent.Publish.Add (fun () -> Task.Run (new Func<Task> (printResults)) |> ignore)

        sendProbes options.FirstTTL options.Queries

        cancellationEvent.WaitOne () |> ignore
    finally
        try
            prober.Dispose ()
        with _ ->
            ()
