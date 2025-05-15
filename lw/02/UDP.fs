module CSnN1.Lw02.UDP

open CSnN1.Lw02.Config

open System.Net
open System.Net.Sockets
open System.Text

let traceroute (options : TracerouteOptions) : unit =
    let hostname = options.Hostname

    try
        // Resolve hostname to get all IP addresses and the selected one
        let allAddresses, targetIp = resolveHostname hostname options.IpVersion

        printfn "Tracing route to %s [%s]" hostname (targetIp.ToString ())

        if allAddresses.Length > 1 then
            printfn
                "Host has multiple addresses: %s"
                (allAddresses |> Array.map (fun ip -> ip.ToString ()) |> String.concat ", ")

        printfn "over a maximum of %d hops:" options.MaxTTL

        // Recursive function to trace each hop
        let rec traceHop (ttl : uint16) =
            if ttl > options.MaxTTL then
                printfn "Trace terminated: reached maximum number of hops."
                ()
            else
                printf "%2d  " ttl

                // Create sockets for sending and receiving
                use icmpSocket =
                    new Socket (AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp)

                icmpSocket.SetSocketOption (
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReceiveTimeout,
                    options.ReceiveTimeout
                )

                icmpSocket.Bind (new IPEndPoint (IPAddress.Any, 0))

                use udpClient = new UdpClient ()
                udpClient.Ttl <- int16 ttl

                // Start the timer
                let stopwatch = System.Diagnostics.Stopwatch.StartNew ()

                // Send UDP packet - use constant port 33434 for better compatibility
                let port = 33434
                let message = Encoding.ASCII.GetBytes "Traceroute Packet"

                try
                    udpClient.Send (message, message.Length, new IPEndPoint (targetIp, port))
                    |> ignore

                    // Buffer for receiving ICMP response
                    let buffer = Array.zeroCreate<byte> 1024
                    let endPoint = ref (new IPEndPoint (IPAddress.Any, 0) :> EndPoint)

                    try
                        // Try to receive ICMP response
                        let bytesReceived = icmpSocket.ReceiveFrom (buffer, endPoint)
                        stopwatch.Stop ()

                        // Get the address of responding host
                        let responseAddress =
                            match endPoint.Value with
                            | :? IPEndPoint as ipEndPoint -> ipEndPoint.Address
                            | _ -> IPAddress.None

                        // Analyze ICMP packet (IP header = 20 bytes + ICMP header starts with type and code)
                        let icmpType = if bytesReceived >= 20 then int buffer.[20] else -1
                        let icmpCode = if bytesReceived >= 21 then int buffer.[21] else -1

                        // Print debug info if we're at the expected last hop
                        let isLastHop =
                            Array.exists (fun addr -> addr.Equals (responseAddress)) allAddresses

                        // Print the result with ICMP info if it's interesting
                        if icmpType = 3 || icmpType = 11 || isLastHop then
                            printfn
                                "%s  %dms  [ICMP Type:%d Code:%d]"
                                (responseAddress.ToString ())
                                (int stopwatch.ElapsedMilliseconds)
                                icmpType
                                icmpCode
                        else
                            printfn "%s  %dms" (responseAddress.ToString ()) (int stopwatch.ElapsedMilliseconds)

                        // If we've received Destination Unreachable (type 3) or reached the target
                        // ICMP Type 3 = Destination Unreachable, Code 3 = Port Unreachable (typical for UDP traceroute)
                        if
                            (icmpType = 3 && icmpCode = 3)
                            || Array.exists (fun addr -> addr.Equals (responseAddress)) allAddresses
                        then
                            printfn "Trace complete."
                            ()
                        else
                            // Continue tracing with the next TTL
                            traceHop (ttl + 1us)
                    with
                    | :? SocketException as ex ->
                        if ex.SocketErrorCode = SocketError.TimedOut then
                            // If we hit a timeout at exactly the expected last hop TTL
                            let nextHopMightBeTarget =
                                // Check if previous hop was one step away from target
                                ttl > 1us
                                &&
                                // If this is near expected TTL range for target
                                (ttl >= options.MaxTTL - 2us)

                            if nextHopMightBeTarget then
                                printfn "*     Request timed out. (Possibly reached target but no ICMP response)"
                                printfn "Many hosts block ICMP 'Destination Unreachable' messages."
                                printfn "Trace probably complete."
                                ()
                            else
                                printfn "*     Request timed out."
                                traceHop (ttl + 1us)
                        else
                            printfn "Error: %s" ex.Message
                            traceHop (ttl + 1us)
                    | ex ->
                        printfn "Error: %s" ex.Message
                        traceHop (ttl + 1us)
                with ex ->
                    printfn "Error sending UDP packet: %s" ex.Message
                    traceHop (ttl + 1us)

        // Start tracing from the first TTL
        traceHop options.FirstTTL

    with ex ->
        printfn "Error: %s" ex.Message

let udpTraceroute : Traceroute = traceroute
