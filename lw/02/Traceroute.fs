module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

type PrintEventArgs () =
    inherit EventArgs ()

type Traceroute (probeFactory : ProbeFactory, options : TraceOptions) =
    let padding = options.MaxTTL.ToString().Length
    let mutable currentTtl = options.FirstTTL
    let mutable currentQuery = 1
    let mutable printed = Set.empty<string>
    let printLock = new Object ()
    let cancellationEvent = new ManualResetEvent (false)

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

    let send, receive, dispose =
        probeFactory
            options
            { LocalEP = new IPEndPoint (options.interfaceIP, 0)
              RemoteEP = fun ttl -> new IPEndPoint (remoteIP, options.Port + ttl)
              Addresses = addresses }

    let results =
        let dict = new Dictionary<int, List<ProbeResult option>> ()

        for i in 1 .. options.MaxTTL do
            dict.[i] <- new ResizeArray<ProbeResult option> ()

        dict

    let printEvent = new Event<unit> ()

    [<CLIEvent>]
    member _.PrintEvent = printEvent.Publish

    member this.Start () =
        try
            printfn
                "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
                options.Hostname
                (remoteIP.ToString ())
                options.MaxTTL
                options.PayloadSize

            this.PrintEvent.Add (fun () -> Task.Run (fun () -> this.PrintAsync ()) |> ignore)

            this.SendAll options.FirstTTL options.Queries

            cancellationEvent.WaitOne () |> ignore
        with ex ->
            printfn "Error: %s" ex.Message

    member private this.SendAll (ttl : int) (remainingQueries : int) =
        if ttl <= options.MaxTTL && cancellationEvent.WaitOne 0 |> not then
            let batchSize = min options.Jobs remainingQueries

            [ for _ in 1..batchSize do
                  send ttl
                  let result = receive ()

                  let updateTtl =
                      match result with
                      | None -> ttl
                      | Some result -> result.ttl

                  results.[updateTtl].Add result
                  printEvent.Trigger () ]
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore

            if remainingQueries > batchSize then
                this.SendAll ttl (remainingQueries - batchSize)
            else
                Thread.Sleep options.SendTimeout
                this.SendAll (ttl + 1) options.Queries

    member private _.PrintAsync () =
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

    interface IDisposable with
        member _.Dispose () = dispose ()
