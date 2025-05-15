module CSnN1.Lw02.Config

open System.Net
open System

type IpVersion =
    | Auto
    | IPv4
    | IPv6

type Protocol =
    | ICMP
    | UDP
    | TCP

type TracerouteOptions =
    { Hostname : string
      MaxTTL : uint16
      Port : uint16
      SendTimeout : int
      ReceiveTimeout : int
      FirstTTL : uint16
      Queries : uint
      ResolveNames : bool
      IpVersion : IpVersion
      PacketLen : uint }

type Traceroute = TracerouteOptions -> unit

let resolveHostname (hostname : string) (ipVersion : IpVersion) : IPAddress array * IPAddress =
    try
        let hostEntry = Dns.GetHostEntry hostname

        if hostEntry.AddressList.Length = 0 then
            raise (WebException (sprintf "Could not resolve hostname: %s" hostname))
        else
            // Get all IP addresses of the host
            let allAddresses = hostEntry.AddressList

            // Select one IP address based on the requested version
            let selectedIp =
                match ipVersion with
                | IPv4 ->
                    match
                        allAddresses
                        |> Array.tryFind (fun ip -> ip.AddressFamily = Sockets.AddressFamily.InterNetwork)
                    with
                    | Some ip -> ip
                    | None ->
                        raise (WebException (sprintf "No IPv4 address found for hostname: %s" hostname))
                | IPv6 ->
                    match
                        allAddresses
                        |> Array.tryFind (fun ip -> ip.AddressFamily = Sockets.AddressFamily.InterNetworkV6)
                    with
                    | Some ip -> ip
                    | None ->
                        raise (WebException (sprintf "No IPv6 address found for hostname: %s" hostname))
                | Auto ->
                    // Try IPv4 first, then IPv6 if IPv4 is not available
                    match
                        allAddresses
                        |> Array.tryFind (fun ip -> ip.AddressFamily = Sockets.AddressFamily.InterNetwork)
                    with
                    | Some ip -> ip
                    | None ->
                        match
                            allAddresses
                            |> Array.tryFind (fun ip ->
                                ip.AddressFamily = Sockets.AddressFamily.InterNetworkV6
                            )
                        with
                        | Some ip -> ip
                        | None ->
                            raise (WebException (sprintf "No IP address found for hostname: %s" hostname))

            // Return both the array of all IPs and the selected IP
            allAddresses, selectedIp
    with
    | :? WebException as ex -> raise ex
    | ex -> raise (WebException (sprintf "Error resolving hostname: %s. %s" hostname ex.Message, ex))
