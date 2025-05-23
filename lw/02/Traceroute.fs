module CSnN1.Lw02.Traceroute

open CSnN1.Lw02.Config
open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

let trace (traceOpts : TraceOptions) (probeOpts : ProbeOptions) =
    let padding = probeOpts.MaxTTL.ToString().Length
    let printLock = new Object ()

    let cancellationEvent = new ManualResetEvent false
    let printEvent = new Event<unit> ()

    let mutable currentTtl = probeOpts.FirstTTL
    let mutable currentQuery = 1
    let mutable printed = Set.empty<string>

    let prober =
        match traceOpts.Protocol with
        | Protocol.ICMP -> new ICMP.Prober (probeOpts) :> IProber
        | Protocol.UDP -> new UDP.Prober (probeOpts) :> IProber
        | Protocol.Auto
        | _ -> new Auto.Prober (traceOpts, probeOpts) :> IProber

    let results =
        let dict = new Dictionary<int, List<ProbeResult option>> ()

        for i in 1 .. probeOpts.MaxTTL do
            dict.[i] <- new ResizeArray<ProbeResult option> ()

        dict

    let printResults () : Task =
        task {
            lock
                printLock
                (fun () ->
                    if results.[currentTtl].Count >= currentQuery then
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

                            printf " %dms" result.ms

                        if currentQuery = probeOpts.Queries then
                            printfn ""

                            let lastResult = results.[currentTtl].[currentQuery - 1]

                            if lastResult.IsSome && lastResult.Value.isSuccess then
                                cancellationEvent.Set () |> ignore

                            currentTtl <- currentTtl + 1
                            currentQuery <- 1

                            if currentTtl > probeOpts.MaxTTL then
                                cancellationEvent.Set () |> ignore
                        else
                            currentQuery <- currentQuery + 1
                )
        }

    let rec sendProbes sendedSize ttl remainingQueries =
        if ttl <= probeOpts.MaxTTL && cancellationEvent.WaitOne 0 |> not then
            let batchSize = min traceOpts.Jobs remainingQueries
            let sendNext = sendProbes (sendedSize + batchSize)

            [ for seq in 0 .. batchSize - 1 do
                  let result = prober.Probe ttl (sendedSize + seq)

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
                sendNext ttl (remainingQueries - batchSize)
            else
                Thread.Sleep probeOpts.SendTimeout
                sendNext (ttl + 1) probeOpts.Queries

    try
        printfn
            "traceroute to %s (%s), %i hops max, %i extra bytes of payload"
            traceOpts.Hostname
            (probeOpts.RemoteEP(0).Address.ToString ())
            probeOpts.MaxTTL
            probeOpts.PayloadSize

        printEvent.Publish.Add (fun _ -> Task.Run (fun () -> printResults().GetAwaiter().GetResult ()) |> ignore)

        sendProbes 0 probeOpts.FirstTTL probeOpts.Queries

        cancellationEvent.WaitOne () |> ignore
    finally
        try
            prober.Dispose ()
        with _ ->
            ()
