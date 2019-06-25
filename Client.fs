module Client

open DiscordRPC
open MPC_HC.Domain
open System.Diagnostics
open System
open Network

type MPCObject =
    {   Cinema : MPCHomeCinema
        Observer : MPCHomeCinemaObserver
        Process : Process   }

type Presence = 
    {   Details : string
        State : string
        Timestamps : Option<Timestamps> 
        Secrets : Option<Secrets>   }

let getMPC = 
    let processRunning = Process.GetProcessesByName "mpc-be64"
    match Array.length processRunning with //start mpc if not started, return running process otherwise
        | 0 -> Process.Start @"C:\Program Files\MPC-BE x64\mpc-be64.exe" 
        | 1 -> processRunning.[0]
        | _ -> 
            printf "Warning: Multiple instances of mpc running, application may not track correctly"
            processRunning.[0]

let createJoinSecret (fileName : string) (position : TimeSpan) =  
    Secrets ( JoinSecret = sprintf "%s-%i" fileName position.Seconds )

let getStartTime position =
    DateTime.UtcNow - position

let positionToTimestamp position = 
    Some(Timestamps ( start = getStartTime position ))

let setRichPresence presence (discord : DiscordRpcClient) =    
    let secret = 
        match presence.Secrets with
        | Some t -> t
        | None -> null
    let presence = 
        new RichPresence ( 
            Details = presence.Details,            
            State = presence.State,
            Secrets = secret,                
            Timestamps = 
                match presence.Timestamps with
                | Some t -> t
                | None -> null )
    discord.SetPresence presence    
    discord    

let updateStartTime position (discord : DiscordRpcClient) = 
    getStartTime position |> discord.UpdateStartTime |> ignore
    position, discord

let updateSecrets filename position (discord : DiscordRpcClient) = 
   createJoinSecret filename position |> discord.UpdateSecrets |> ignore
   position, discord
   
let updateTimestamp (discord : DiscordRpcClient) filename position = 
    updateStartTime position discord ||> updateSecrets filename |> ignore
    position, discord   

let InitializeDiscord =    
    let logger = new DiscordRPC.Logging.ConsoleLogger()
    let discord = new DiscordRpcClient("352057029000560659", -1,logger ,true, null)
    discord.OnReady.Add(fun msg -> printf "Connected to client %s" msg.User.Username)
    discord.OnPresenceUpdate.Add(fun msg -> printf "Received update: %s" msg.Presence.Details)
    
    discord.Initialize() |> function
        | true -> discord
        | false -> failwith "Rich Presense failed to initialize, please ensure Discord is running"         
        |> setRichPresence {Details = "Selecting a video"; State = "Chilling"; Timestamps = None; Secrets = None }

let InitializeMPC = async {
    let proc = getMPC // initialize client first
    let mpcClient = new MPCHomeCinema("http://localhost:13579")
    let! info = mpcClient.GetInfo() |> Async.AwaitTask 
    let observ = new MPCHomeCinemaObserver(mpcClient)
    do! observ.Start() |> Async.AwaitTask
    printf "State %s" <| info.State.ToString()   
    return { 
        Cinema = mpcClient
        Observer = observ
        Process = proc    }    
}    

let mpcPropertyChanged (arg :  PropertyChangedEventArgs) =
    let filename, position = arg.NewInfo.FileName, arg.NewInfo.Position
    match arg.Property with
    | Property.File | Property.State -> 
        Some({Details = "Watching a video"; State = filename; Timestamps = positionToTimestamp position; Secrets = Some(createJoinSecret filename position) })
    | _ -> None

let setupNetwork = async {
    let! device = mapAll 50000 50000    
    let sock = Async.RunSynchronously <| getIP device |> getSocket 50000
    sock
    
}
    

[<EntryPoint>]
let main argv =   
    let discord = InitializeDiscord
    let mpc = Async.RunSynchronously InitializeMPC    
    let mpcObserver = mpc.Observer.PropertyChanged.Subscribe(fun arg -> mpcPropertyChanged arg |> function
        | Some m -> setRichPresence m discord |> ignore
        | None -> ()    )
    
    Console.ReadKey() |> ignore
    0



                

    
       
    


    
    




