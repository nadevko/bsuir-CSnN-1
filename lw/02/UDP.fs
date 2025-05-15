module CSnN1.Lw02.UDP
open CSnN1.Lw02.Config

open System
open System.Net
open System.Net.Sockets

let tryGetHostName (ipAddress : IPAddress) (resolveNames : bool) : string =
    if resolveNames then
        try
            let hostEntry = Dns.GetHostEntry ipAddress

            if String.IsNullOrEmpty hostEntry.HostName then
                ipAddress.ToString ()
            else
                sprintf "%s (%s)" hostEntry.HostName (ipAddress.ToString ())
        with _ ->
            ipAddress.ToString ()
    else
        ipAddress.ToString ()

let traceroute (options : TracerouteOptions) : unit =
    let destinationIP =
        try
            Dns.GetHostEntry(options.Hostname).AddressList
            |> Array.find (fun ip -> ip.AddressFamily = AddressFamily.InterNetwork)
        with _ ->
            failwithf "Не удалось разрешить хост: %s" options.Hostname

    use receiver =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    receiver.SendTimeout <- options.SendTimeout
    receiver.ReceiveTimeout <- options.ReceiveTimeout
    receiver.Bind (new IPEndPoint (IPAddress.Any, 0))

    let endpoint = new IPEndPoint (destinationIP, int options.Port)

    printfn "traceroute to %s (%A), %d hops max" options.Hostname destinationIP options.MaxHops

    let mutable ttl = options.FirstTtl
    let mutable reached = false

    while ttl <= options.MaxHops && not reached do
        use udpSender = new UdpClient ()
        udpSender.Client.Ttl <- int16 ttl

        let responseMap = new System.Collections.Generic.Dictionary<string, bool> ()

        for _ in 1u .. options.Queries do
            udpSender.Send ([| 0uy |], 1, endpoint) |> ignore

            try
                let mutable buffer = Array.zeroCreate 512
                let mutable remoteEP = new IPEndPoint (IPAddress.Any, 0) :> EndPoint
                let bytes = receiver.ReceiveFrom (buffer, &remoteEP)
                let respEP = remoteEP :?> IPEndPoint

                let ipStr = respEP.Address.ToString ()

                if not (responseMap.ContainsKey ipStr) then
                    responseMap.Add (ipStr, true)
                    let addrStr = tryGetHostName respEP.Address options.ResolveNames
                    printfn "%2d  %s" ttl addrStr

                    if respEP.Address.Equals destinationIP then
                        reached <- true
            with :? SocketException ->
                if responseMap.Count = 0 then
                    printfn "%2d  *" ttl
                    responseMap.Add ("*", true)

        ttl <- ttl + 1u
