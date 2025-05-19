module CSnN1.Lw02.Auto

open CSnN1.Lw02.Config

type Prober (traceOpts : TraceOptions, probeOpts : ProbeOptions) =
    let udpProber = new UDP.Prober (probeOpts)
    let icmpProber = new ICMP.Prober (probeOpts)

    interface IProber with
        member _.Probe ttl =
            match (icmpProber :> IProber).Probe ttl with
            | Some result -> Some result
            | None ->
                System.Threading.Thread.Sleep 20
                (udpProber :> IProber).Probe ttl

    interface System.IDisposable with
        member _.Dispose () =
            (icmpProber :> System.IDisposable).Dispose ()
            (udpProber :> System.IDisposable).Dispose ()
