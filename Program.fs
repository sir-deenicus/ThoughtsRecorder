open Terminal.Gui.Elmish
open ModelState
open View
open AudioService
open System
open System.Text    
open System.Collections.Generic
open Paths

let whisperConfig =
    { ModelPath = @"D:\Downloads\ggml-medium.en-q5_0.bin"
      UseGPU = true
      NumThreads = Environment.ProcessorCount / 2 }

let logMessages = Stack()

printfn "Loading Whisper model..."

let transcriber =
    new Transcriber(whisperConfig, logDispatch = (fun _ message -> logMessages.Push(message)))

let audioRecorder = new WindowsAudioRecorder()

let peakAddedEvent dispatch =
    (audioRecorder :> IAudioRecorder).PeakTracker.PeakAdded.Add(fun () -> dispatch PeakAdded)

//create timer for autosave
let autoSaveTimer = new System.Timers.Timer(60000.0)

let saveTick dispatch =
    autoSaveTimer.Elapsed.Add(fun _ -> dispatch AutoSaveTick)
    autoSaveTimer.Start()

Program.mkProgram (init autoSaveTimer audioRecorder transcriber logMessages) update view
|> Program.withSubscription (fun _ -> Cmd.ofSub peakAddedEvent)
|> Program.withSubscription (fun _ -> Cmd.ofSub saveTick)
|> Program.run
