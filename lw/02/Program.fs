module internal Traceroute.Program

open Config

let argv = System.Environment.GetCommandLineArgs() |> Array.skip 1

let main (argv: string array) =
    let cfg = Arguments.configure argv
    let remoteIP = cfg.remoteEP.Address
    printfn "Traceroute to %s (%s)" (System.Net.Dns.GetHostEntry remoteIP).HostName (remoteIP.ToString())

    cfg
    |> match cfg.protocol with
       | ICMP -> ICMP.route
       | TCP -> failwith "TCP is not implemented"
       | UDP -> failwith "UDP is not implemented"
       | UDDP -> failwith "UDDP is not implemented"
       | UDPLITE -> failwith "UDP lite is not implemented"

#if DEBUG
main argv |> printfn "%A"
#else
try
    main argv
with ex ->
    System.Console.ForegroundColor <- System.ConsoleColor.Red
    printfn "[ERROR] %s" ex.Message
    System.Console.ResetColor()
    exit 1
#endif
