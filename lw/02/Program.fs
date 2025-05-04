module internal Traceroute.Program

open Config

let argv = System.Environment.GetCommandLineArgs() |> Array.skip 1

let main (argv: string array) =
    let cfg = Arguments.configure argv
    printfn "Traceroute to %s" (makeHostIPPair cfg.remoteEP.Address)

    cfg
    |> match cfg.protocol with
       | ICMP -> ICMP.route
       | _ -> cfg.protocol.ToString() |> sprintf "%s is not implemented" |> failwith

#if DEBUG
main argv
#else
try
    main argv
with ex ->
    System.Console.ForegroundColor <- System.ConsoleColor.Red
    printfn "[ERROR] %s" ex.Message
    System.Console.ResetColor()
    exit 1
#endif
