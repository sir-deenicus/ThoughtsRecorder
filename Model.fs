module ModelState
open AudioService
open System 
open Terminal.Gui.Elmish
open System.Text
open System.Collections.Generic
open System.Text.RegularExpressions
open Paths

type RecordingState =
    | Idle
    | Recording
    | Transcribing

type Msg =
    | StartRecording
    | StopRecording 
    | TranscribeComplete of TranscriptionResult
    | TextChanged of string  
    | AppendToDocument
    | SaveDocument
    | OpenDialog
    | SaveDialog
    | FileSelected of content:Result<string, string>
    | NewDocument 
    | WindowSizeUpdate of int * int
    | PeakAdded
    | Quit 
    | AutoSaveTick
    | Error of string
    | SaveResult of Result<string option, string>

type Model = {
    recordingState: RecordingState
    currentTranscript: string
    fullDocument: string
    currentLog : Stack<string>
    errorMessage: string option
    audioRecorder: IAudioRecorder 
    ModelSize: int * int
    currentDocumentPath: string option
    RecordingLength: unit -> TimeSpan
    transcriber : Transcriber
    Timer: Timers.Timer
}

let stripBrackets (input: string) =
    Regex.Replace(input, @"\[.*?\]", "").Trim()

let openFileDialogCmd () =
    Cmd.OfAsync.perform (
        fun () ->
            async {
                let file = Dialogs.openFileDialog "Open TextFile" "Select Textfile to Open"            
                match file with
                | None ->
                    return (Ok "")
                | Some f ->
                    try
                        let! content = System.IO.File.ReadAllTextAsync(f) |> Async.AwaitTask
                        return (Ok content)
                    with
                    | _ as e ->
                        return (Result.Error e.Message)
            }
    ) () FileSelected

let saveFileDialogCmd (content: string) = 
    Cmd.OfAsync.perform (
        fun () ->
            async {
                let filepath = Dialogs.saveFileDialog "Save TextFile" "Select Textfile to Save"            
                match filepath with
                | None ->
                    return (Ok None)
                | Some path ->
                    try
                        do! System.IO.File.WriteAllTextAsync(path, content) |> Async.AwaitTask
                        return (Ok (Some path))
                    with
                    | _ as e ->
                        return (Result.Error e.Message)
            }
    ) () SaveResult


let init autoSaveTimer (audioRecorder: IAudioRecorder) transcriber logMessages _ =  
    {
        recordingState = Idle
        currentTranscript = ""
        fullDocument = ""
        errorMessage = None
        audioRecorder = audioRecorder
        transcriber = transcriber
        ModelSize = (0, 0)
        currentDocumentPath = None
        RecordingLength = audioRecorder.RecordingLength
        currentLog = logMessages
        Timer = autoSaveTimer
    }, Cmd.none

let pathProvider = PathProvider()

let update (msg: Msg) (model: Model) =
    match msg with
    | StartRecording ->
        match model.audioRecorder.Start() with
        | Ok () -> 
            { model with 
                recordingState = Recording
                errorMessage = None }, Cmd.none
        | Result.Error err -> 
            model.currentLog.Push err
            { model with errorMessage = Some err }, Cmd.none
    | StopRecording ->
        match model.recordingState with
        | Recording ->
            let audioData = model.audioRecorder.Stop() 
            let cmd = 
                Cmd.OfAsync.either
                    (fun () -> model.transcriber.TranscribeAsync(audioData))
                    ()
                    (function | Ok result -> TranscribeComplete result | Result.Error err -> Error err)
                    (fun err -> Error err.Message)
            { model with recordingState = Transcribing }, cmd
        | _ -> model, Cmd.none

    | WindowSizeUpdate (width, height) -> 
        { model with ModelSize = (width, height) }, Cmd.none
        
    | TranscribeComplete result ->
        { model with 
            errorMessage = None
            currentTranscript = model.currentTranscript + stripBrackets result.Text; recordingState = Idle }, Cmd.none 
    | TextChanged text -> 
        { model with currentTranscript = text }, Cmd.none  
    | AppendToDocument ->
        { model with
            fullDocument = 
                if String.IsNullOrEmpty model.fullDocument then
                    model.currentTranscript
                else
                    model.fullDocument + Environment.NewLine + Environment.NewLine + model.currentTranscript
            currentTranscript = "" }, Cmd.none
    | NewDocument -> 
        { model with 
            fullDocument = ""
            currentDocumentPath = None }, Cmd.none
    | SaveDocument ->
        match model.currentDocumentPath with
        | Some path ->
            System.IO.File.WriteAllText(path, model.fullDocument)
            model, Cmd.none
        | None -> model, Cmd.none      
    | OpenDialog -> model, openFileDialogCmd()
    | SaveDialog -> model, saveFileDialogCmd(model.fullDocument)
    | FileSelected result ->
        match result with
        | Ok content -> { model with fullDocument = content; errorMessage = None }, Cmd.none
        | Result.Error err -> 
            model.currentLog.Push err
            { model with errorMessage = Some err }, Cmd.none 
    | SaveResult (Ok (Some path)) -> 
        { model with currentDocumentPath = Some path; errorMessage = None }, Cmd.none
    | SaveResult (Ok None) -> { model with errorMessage = None }, Cmd.none
    | SaveResult (Result.Error err) -> 
        model.currentLog.Push err
        { model with errorMessage = Some err }, Cmd.none
    | PeakAdded -> model, Cmd.none
    | AutoSaveTick ->   
        if not(String.IsNullOrEmpty model.fullDocument) then
            pathProvider.SaveContent model.fullDocument   
        model, Cmd.none
    | Error err ->
        model.currentLog.Push err
        { model with 
            errorMessage = Some err }, Cmd.none
    | Quit -> 
        model.audioRecorder.Stop() |> ignore
        model.audioRecorder.Dispose()   
        model.Timer.Stop()
        model.Timer.Dispose()
        (model.transcriber :> IDisposable).Dispose() 
        Program.quit()
        model, Cmd.none    