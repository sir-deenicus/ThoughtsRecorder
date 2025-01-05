module AudioService

open NAudio.Wave
open System
open System.IO
open WhisperLite.Net
open System.Diagnostics
open System.Collections.Generic
open System.Threading.Tasks

type AudioFormat = { SampleRate: int; Channels: int }

type WhisperConfig =
    { ModelPath: string
      UseGPU: bool
      NumThreads: int }

type TranscriptionResult =
    { Text: string
      Duration: TimeSpan
      Language: string }

type PeakCollector(sampleSize: int, ?maxpeaks) =
    let mutable count = 0
    let mutable currentMax = 0s
    let maxPeaks = defaultArg maxpeaks 500
    let peaks = LinkedList<float>()
    let peakAdded = Event<unit>()

    member _.Add(sample: int16) =
        currentMax <- max currentMax (abs sample)
        count <- count + 1

        if count >= sampleSize then
            if peaks.Count >= maxPeaks then
                peaks.RemoveLast()

            peaks.AddFirst(float currentMax / 32768.0) |> ignore
            peakAdded.Trigger()
            count <- 0
            currentMax <- 0s

    member _.Peaks = peaks

    member _.PeakAdded = peakAdded.Publish

    member _.Reset() =
        count <- 0
        currentMax <- 0s
        peaks.Clear()

type IAudioRecorder =
    abstract member Start: unit -> Result<unit, string>
    abstract member Stop: unit -> byte[]
    abstract member IsRecording: bool
    abstract member RecordingLength: unit -> TimeSpan
    abstract member PeakTracker: PeakCollector
    inherit IDisposable

//logger has type delegate void WhisperLogger(GgmlLogLevel level, string message);
type Transcriber(config: WhisperConfig, ?logDispatch) =
    let whisper =
        new Whisper(config.ModelPath, useGPU = config.UseGPU, ?logger = logDispatch)

    do
        whisper.LoggingEnabled <- false
        whisper.InitToDefaultParameters(NumThreads = config.NumThreads)

    let toSamples (buffer: byte[]) =
        [| for i in 0..2 .. buffer.Length - 2 -> float32 (BitConverter.ToInt16(buffer, i)) / 32768.0f |]

    member _.Language = whisper.Language

    member _.Transcribe(audioData: byte[]) =
        try
            let sw = Stopwatch.StartNew()
            let result = whisper.TranscribeAudio(toSamples audioData)
            sw.Stop()

            Ok
                { Text = result
                  Duration = sw.Elapsed
                  Language = whisper.Language }
        with ex ->
            Result.Error ex.Message

    member this.TranscribeAsync(audioData: byte[]) =
        async {
            let! result = Task.Run(fun () -> this.Transcribe(audioData)) |> Async.AwaitTask
            return result
        }

    interface IDisposable with
        member _.Dispose() =
            if whisper <> null then
                (whisper :> IDisposable).Dispose()


type WindowsAudioRecorder() =
    let mutable waveIn: WaveInEvent = null
    let mutable memoryStream: MemoryStream = null
    let mutable isRecording = false
    let mutable startTime = DateTime.Now
    let peakCollector = PeakCollector(1000)

    let whisperFormat = { SampleRate = 16000; Channels = 1 }

    interface IAudioRecorder with
        member _.Start() =
            try
                memoryStream <- new MemoryStream()
                waveIn <- new WaveInEvent(WaveFormat = WaveFormat(whisperFormat.SampleRate, 16, whisperFormat.Channels))

                waveIn.DataAvailable.Add(fun args ->
                    try
                        memoryStream.Write(args.Buffer, 0, args.BytesRecorded)

                        for i in 0..2 .. args.BytesRecorded - 2 do
                            peakCollector.Add(BitConverter.ToInt16(args.Buffer, i))
                    with _ ->
                        ())

                startTime <- DateTime.Now
                isRecording <- true
                waveIn.StartRecording()
                Ok()
            with ex ->
                Result.Error ex.Message

        member _.Stop() =
            if isRecording then
                if waveIn <> null then
                    waveIn.StopRecording()
                    waveIn.Dispose()
                    waveIn <- null

                // Convert memory stream to WAV format
                let audioData = memoryStream.ToArray()
                memoryStream.Dispose()
                memoryStream <- null
                isRecording <- false
                peakCollector.Reset()
                audioData
            else
                Array.empty

        member _.IsRecording = isRecording

        member _.PeakTracker = peakCollector

        member _.RecordingLength() =
            if isRecording then
                DateTime.Now - startTime
            else
                TimeSpan.Zero

        member this.Dispose() =
            if waveIn <> null then
                waveIn.Dispose()

            if memoryStream <> null then
                memoryStream.Dispose()
