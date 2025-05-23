module CSnN1.Lw02.Auto

open CSnN1.Lw02.Config

type Prober (traceOpts : TraceOptions, probeOpts : ProbeOptions) =
    let udpProber = new UDP.Prober (probeOpts)
    let icmpProber = new ICMP.Prober (probeOpts)

    interface IProber with
        member _.Probe ttl sequence =
            match (udpProber :> IProber).Probe ttl sequence with
            | Some result -> Some result
            | None ->
                System.Threading.Thread.Sleep 20
                (icmpProber :> IProber).Probe ttl sequence

    interface System.IDisposable with
        member _.Dispose () =
            (udpProber :> System.IDisposable).Dispose ()
            (icmpProber :> System.IDisposable).Dispose ()
