module CSnN1.Lw02.Auto

open CSnN1.Lw02.Config
open System.Net
open System.Threading

type Prober (traceOpts : TraceOptions, probeOpts : ProbeOptions) =
    let udpProber = new UDP.Prober (probeOpts)
    let icmpProber = new ICMP.Prober (probeOpts)

    let isDisposed = ref false

    interface IProber with
        member _.Probe ttl =
            match (udpProber :> IProber).Probe ttl with
            | Some result -> Some result
            | None ->
                System.Threading.Thread.Sleep 20
                (icmpProber :> IProber).Probe ttl

    interface System.IDisposable with
        member _.Dispose () =
            if not isDisposed.Value then
                (udpProber :> System.IDisposable).Dispose ()
                (icmpProber :> System.IDisposable).Dispose ()
                isDisposed.Value <- true
