open System.Net
open System.Net.Sockets

let getResultString (ipAddress: IPAddress) =
    try
        let hostEntry = Dns.GetHostEntry(ipAddress)
        if System.String.IsNullOrEmpty(hostEntry.HostName) then ipAddress.ToString()
        else sprintf "%s (%s)" hostEntry.HostName (ipAddress.ToString())
    with
    | _ -> ipAddress.ToString()

let traceroute (hostname : string) (maxHops : int) (port: int) (timeout: int) =
    let destIP =
        try
            Dns.GetHostEntry(hostname).AddressList
            |> Array.find (fun ip -> ip.AddressFamily = AddressFamily.InterNetwork)
        with _ ->
            failwithf "Host are not resolved: %s" hostname

    use receiver =
        new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

    receiver.ReceiveTimeout <- timeout
    receiver.Bind (new IPEndPoint (IPAddress.Any, 0))
    let destEP = new IPEndPoint (destIP, port)

    printfn "traceroute to %s (%A), %d hops max" hostname destIP maxHops

    let mutable isReached = false
    let mutable ttl = 1

    while ttl <= maxHops && not isReached do
        use client = new UdpClient ()
        client.Client.Ttl <- int16 ttl
        client.Send ([| 0uy |], 1, destEP) |> ignore

        let mutable result = "*"

        try
            let mutable buffer = Array.zeroCreate 512
            let mutable remoteEP = new IPEndPoint (IPAddress.Any, 0) :> EndPoint
            receiver.ReceiveFrom (buffer, &remoteEP) |> ignore
            let remoteEP' = remoteEP :?> IPEndPoint
            let remoteIP = remoteEP'.Address
            result <- getResultString remoteIP
            printfn "%2d  %s" ttl result

            if remoteEP'.Address.Equals destIP then
                isReached <- true
        with :? SocketException ->
            printfn "%2d  *" ttl

        ttl <- ttl + 1

traceroute "1.1.1.1" 30 33434 3000
