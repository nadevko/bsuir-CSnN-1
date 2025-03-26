module internal Traceroute.Program

let argv = System.Environment.GetCommandLineArgs() |> Array.skip 1

try
    Arguments.configure argv |> Tracert.run
with ex ->
#if DEBUG
    raise ex
#else
    System.Console.ForegroundColor <- System.ConsoleColor.Red
    printfn "[ERROR] %s" ex.Message
    System.Console.ResetColor()
    exit 1
#endif
