module Network

open Open.Nat
open System.Net.Sockets
open System.Net
open System.Reactive

let startNAT = async {
    let discover = new NatDiscoverer()
    let! device = Async.AwaitTask <| discover.DiscoverDeviceAsync()        
    return device
}

let createPortMapping privatePort publicPort (device : NatDevice) = async {
    return! Mapping(Protocol.Udp, privatePort, publicPort) |> device.CreatePortMapAsync |> Async.AwaitTask
}

let mapAll privatePort publicPort = async {
    let! device = startNAT
    do! createPortMapping privatePort publicPort device
    return device
}

let getIP (device:NatDevice) = device.GetExternalIPAsync() |> Async.AwaitTask

let getSocket port (ip:IPAddress) = 
    let sock = new Socket(SocketType.Stream, ProtocolType.Udp)
    IPEndPoint(ip, port) |> sock.Bind
    sock
 
 

