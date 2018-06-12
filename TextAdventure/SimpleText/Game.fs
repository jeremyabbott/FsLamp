module Game
open Domain
open GameState
open Actions
open Environment
open Parser
open System

let title = """
,d88~~\ ,e,                         888                 ~~~888~~~                      d8   
8888     "  888-~88e-~88e 888-~88e  888  e88~~8e           888     e88~~8e  Y88b  /  _d88__ 
`Y88b   888 888  888  888 888  888b 888 d888  88b          888    d888  88b  Y88b/    888   
 `Y88b, 888 888  888  888 888  8888 888 8888__888          888    8888__888   Y88b    888   
   8888 888 888  888  888 888  888P 888 Y888    ,          888    Y888    ,   /Y88b   888   
\__88P' 888 888  888  888 888-_88"  888  "88___/           888     "88___/   /  Y88b  "88_/ 
                          888                                                               
"""

// this key is used to open
let keyItem = Item.createInventoryItem "key" "laying in a pile of debris" [Unlock (ExitId 5, "After a few minutes of getting the key to fit correctly, the lock releases and the door creakily opens.")]

// lantern is an item you can take that allows you to see in dark places.
// it can be turned on & off
// it consumes battery life when it's turned on
let lanternItem =
    Item.createTemporaryItem 
        "lantern" "with a full battery" [] 15 
            [
            GameBehaviors.Inventory.add GameBehaviors.Inventory.decrementLifeOnUpdateBehavior;
            GameBehaviors.Inventory.add 
                (GameBehaviors.Inventory.rangedOutputBehavior 
                    [
                        (0,0, "Lantern's batteries are dead.");
                        (5,5, "Lantern is getting extremely dim.");
                        (10,10, "Lantern is getting dim.");]);
                    ]
let swordItem =
    Item.createAttackItem 
let defaultMap =
    [|
        (Environment.create 1 "Origin"
            "A moment ago you were just in bed floating above your mind, dreaming about how to add zebras to spreadsheets.  Now it appears you've awakened in a dimlit room. Many unfamiliar smells lurk around you. There's an old creaky door to the north."
            [Exit.create 1 2 Open North (Steps 2) "Creaky door"]
            [keyItem; lanternItem]
            []
        );
        (Environment.create 2 "Long Hallway, South End"
            "The door opens into what appears to be a really long hallway continuing North. There's no light at the other end."
            [
                Exit.create 2 1 Open South (Steps 2) "Creaky door";
                Exit.create 3 3 Open North (Steps 6) "Dark hallway";]
            []
            [Encounter.create "Green Slime appears and is attacking you!" [
                Monster.create 1 "Green Slime" (DefenseStat 10) (AttackStat 1) (Damage 2) (Health (10, 10)) 100
            ]]
        );
        (Environment.create 3 "Long Hallway, North End"
            "It gets so dark you have to feel your way around. Thankfully there's nothing too dangerous in your path."
            [ 
                Exit.create 4 2 Open South (Steps 6) "The south end of the hallway";
                Exit.create 5 4 Locked East (Steps 6) "A door with no features, labeled 'Private'"]
            []
            [Encounter.create "A gruet jumps out from the darkness." [
                Monster.create 2 "Gruet" (DefenseStat 14) (AttackStat 1) (Damage 3) (Health (12, 12)) 200
            ]]
        );
        (Environment.create 4 "Office"
            "As the door opens, you begin to see the remnants of an old dusty office.  This place hasn't been used in years. An old typewriter on the desk is missing most of its keys."
            [ Exit.create 6 3 Open West (Steps 6) "Door with no features"; Exit.create 7 5 Hidden East (Steps 2) "Secret Passage"]
            []
            [Item.createEnvironmentItem "typewriter" [Unhide (ExitId 7, "As you press down hard on one of the keys. The air begins to move around you. Suddenly, a secret passage opens up from within the wall.")]]
        );
        (Environment.create 5 "Secret Passage"
            """The path leads downward with considerable gradient. Things turn cold as you hear a voice... 'stoi impul chani, mario.' Frozen, but unable to make out any figures ahead of you, you shout back 'Who's there?'
A few seconds pass, finally a response... 'die!'.  As you fall backward you stumble over a rock.            
            """
            [ Exit.create 7 4 Open West (Steps 2) "Secret entrance"; Exit.create 8 6 Hidden East (Steps 10) "Dark Passage towards the footsteps"]
            []
            [Item.createEnvironmentItem "rock" [Unhide (ExitId 8, "You throw the rock directly at the voice and hear a terrible scream.  Moments later you can hear footsteps running to the east away from you.")]]
        );
        (Environment.create 6 "Dark Passage"
            """Is it really a good idea to go chasing after such a terrible, unknown, thing? Probably not, but that hasn't stopped you so far."""
            [ Exit.create 9 5 Open West (Steps 10) "Secret Passage"]
            [Item.createInventoryItem "Gold" "that probably fell out of someones pocket" []]
            []
        );
    |]

let player1 = Player.create "P1" (AttackStat 2) (DefenseStat 14) 15

let defaultGamestate map =
    { Player = player1;
        Inventory = [];
        World = { Time = DateTime.Parse("1971-01-01 06:01:42"); Map = map };
        Environment = map.[0];
        GameScene = MainMenu;
        LastCommand = NoCommand;
        Output = Header [title; "Type GO to start the game, or LOAD to start from saved game."]}

let getCommand (parseInput: CommandParser) =
    Console.Write("\n> ")
    let readline = Console.ReadLine()
    match readline |> parseInput with
    | Some command -> command
    | None -> 
        printfn "I don't understand %s." readline
        NoCommand

let getAction gameScene (dispatcher: Command -> GamePart) = 
    let parser = 
        match gameScene with
        | MainMenu -> mainMenuParser
        | OpenExplore -> exploreParser
        | InEncounter _ -> encounterParser
    getCommand parser |> dispatcher

let handleHeader : GamePart =
    fun gamestate ->
        match gamestate.Output with
        | Header log -> log |> List.iter (printfn "%s")
        | _ -> ()
        gamestate

let updateGameObjects : GamePart =
    fun gamestate ->
        gamestate.Inventory
        |> List.collect (fun i -> Item.inventoryItemBehaviors i |> List.map (fun b -> (i, b)))
        |> List.map (fun (i, b) -> (i, GameBehaviors.Inventory.find b))
        |> List.filter (fun (_, b) -> b.IsSome)
        // thread gamestate through all the update functions
        |> List.fold (fun gs (item, behavior) ->
            match behavior.Value with
            | UpdateBehavior update ->
                let (item', gs') = update item gs
                // update item in gamestate
                let inventory' = updateInventory item' gs.Inventory
                gs' |> setInventory inventory'
            | _ -> gs
        ) gamestate

let getGameObjectOutputs : GamePart =
    fun gamestate ->
        gamestate.Inventory
        |> List.collect (fun i -> Item.inventoryItemBehaviors i |> List.map (fun b -> (i, b)))
        |> List.map (fun (i, b) -> (i, GameBehaviors.Inventory.find b))
        |> List.filter (fun (_, b) -> b.IsSome)
        // collect all the outputs
        |> List.collect (fun (item, behavior) ->
            match behavior.Value with
            | OutputBehavior getOutputs ->
                getOutputs item gamestate
            | _ -> []
        )
        // append all the outputs
        |> List.fold (fun gs s -> addOutput s gs) gamestate      
        // |> List.iter (printfn "%s")
        // gamestate

// loop: Read -> Parse -> Command -> Action -> Print -> Loop
let RunGame
    (dispatcher: Command -> GamePart)
    initialState =
    let rec loop history gamestate =
        // print header
        gamestate |> handleHeader |> ignore

        // get action from dispatcher based on input
        let action = getAction gamestate.GameScene dispatcher

        // execute the action to get next gamestate
        let nextGameState = gamestate |> updateGameObjects |> action  |> getGameObjectOutputs

        // handle output
        match nextGameState.Output with
        | Output log ->
            log |> List.iter (printfn "%s")
            loop (nextGameState :: history) nextGameState
        | Header _ | DoNothing ->
            loop (nextGameState :: history) nextGameState
        | Rollback ->
            printfn "Rolling back..."
            let (oldState, newHistory) =
                match history with
                | _ :: old :: tail  -> (old, tail)
                | _                 -> (gamestate, history)
            loop newHistory oldState
        | ExitGame -> ()
    
    loop [] initialState