module View

open Terminal.Gui
open Terminal.Gui.Elmish
open ModelState
open AsciiChart.Sharp

let view (model: Model) (dispatch: Msg -> unit) =
    
    let chartOptions = Options(Height = 8, AxisLabelFormat = "00.0M", AxisLabelLeftMargin = 1, AxisLabelRightMargin = 1)

    let colorScheme () =
        let color = Attribute.Make(Color.Brown, Color.DarkGray)
        ColorScheme(Focus = color)
    
    View.page [ 
        page.onLoaded (fun (w,h) -> dispatch (WindowSizeUpdate (w,h)))
        page.onResized (fun size -> dispatch (WindowSizeUpdate (size.Width, size.Height)))
        page.children [
            View.frameView [ 
                prop.position.x.at 0
                prop.position.y.at 0
                prop.width.percent 45
                prop.height.percent 25 
                frameView.title "Current Transcript"
                frameView.children [ 
                    View.textView [
                        prop.position.x.at 0
                        prop.position.y.at 0
                        prop.color (Color.Green, Color.Black)
                        prop.width.filled
                        prop.height.filled 
                        textView.wordWrap true
                        textView.onContentsChanged (fun txt -> dispatch (TextChanged txt))  
                        textView.text model.currentTranscript 
                    ]
                ]
            ]
            View.frameView [
                prop.position.x.percent 0
                prop.position.y.percent 25
                prop.width.percent 45
                prop.height.percent 15 
                //buttons
                frameView.children [  
                    View.button [
                        prop.position.x.percent 0
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme())
                        (match model.recordingState with Transcribing -> prop.disabled | _ -> prop.enabled)
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ -> 
                            match model.recordingState with
                            | Idle -> dispatch StartRecording
                            | Recording -> dispatch StopRecording
                            | Transcribing -> ())
                        button.text (
                            match model.recordingState with
                            | Idle -> "Start Recording ●"
                            | Recording -> "Stop Recording ■"
                            | Transcribing -> "Transcribing..."
                        )
                    ] 
                    View.button [
                        prop.position.x.percent 41
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme())
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ -> dispatch AppendToDocument)
                        button.text "S_end →"
                    ]                    
                ]
            ] 
            View.frameView [
                prop.position.x.percent 0
                prop.position.y.percent 38
                prop.width.percent 45
                prop.height.percent 38
                frameView.title "Waveform"
                frameView.children [ 
                    View.textView [
                        prop.position.x.at 0
                        prop.position.y.at 0
                        prop.color (Color.Green, Color.Black)
                        prop.width.filled
                        prop.height.filled
                        textView.readOnly true 
                        textView.text (
                            if model.audioRecorder.PeakTracker.Peaks.Count > 0 then 
                                AsciiChart.Plot(model.audioRecorder.PeakTracker.Peaks, chartOptions) 
                            else ""
                        )
                    ]
                ]
            ]
            //rest is log 
            View.frameView [ 
                prop.position.x.at 0
                prop.position.y.percent 74
                prop.width.percent 45
                prop.height.percent 25
                frameView.title "Log"
                frameView.children [ 
                    View.scrollView [ 
                        prop.position.x.at 0
                        prop.position.y.at 0
                        prop.color (Color.Green, Color.Black)
                        prop.width.filled
                        prop.height.filled
                        scrollView.contentSize (Size(140,120))
                        scrollView.children [
                            View.textView [ 
                                prop.position.x.at 0
                                prop.position.y.at 0
                                prop.width.fill 2
                                prop.height.fill 2
                                prop.color (Color.Green, Color.Black)
                                textView.readOnly true  
                                textView.text (model.currentLog |> String.concat "")
                            ]
                        ]
                    ]
                ]
            ] 

            //Right section
            //menu for new, save, load 
            View.frameView [
                prop.position.x.percent 45
                prop.position.y.at 0
                prop.width.percent 55
                prop.height.sized 3
                frameView.title "Menu"
                frameView.children [ 
                    View.button [
                        prop.position.x.percent 0
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme())
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ -> dispatch NewDocument)
                        button.text "New"
                    ]

                    View.button [
                        prop.position.x.at 8
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme ())
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ ->
                            match model.currentDocumentPath with
                            | Some _ -> dispatch SaveDocument
                            | None -> dispatch SaveDialog)
                        button.text (
                            match model.currentDocumentPath with
                            | Some _ -> "Save"
                            | None -> "Save As"
                        ) 
                    ]
                    
                    View.button [
                        prop.position.x.at 19
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme())
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ -> dispatch OpenDialog)
                        button.text "Open"
                    ]
                    
                    View.button [
                        prop.position.x.at 27
                        prop.position.y.at 0
                        prop.colorNormal (Color.Green, Color.Black)
                        prop.colorScheme (colorScheme())
                        prop.width.sized 1
                        prop.height.sized 1
                        button.onClick (fun _ -> dispatch Quit)
                        button.text "Quit ×"
                    ] 

                    View.label [
                        prop.position.x.at 37
                        prop.position.y.at 0
                        prop.width.percent 40
                        prop.height.sized 1
                        prop.color (Color.Green, Color.Black)
                        label.text (
                            match model.recordingState, model.errorMessage with
                            | _, Some err -> err
                            | Idle, _ -> " | Idle"
                            | Recording, _ -> $" | Rec ({model.RecordingLength()})"
                            | Transcribing, _ -> " | Transcribing")
                    ] 
                ]
            ]
            // document view
            View.frameView [
                prop.position.x.percent 45
                prop.position.y.at 3
                prop.width.percent 55
                prop.height.percent 90
                frameView.title "Document"
                frameView.children [ 
                    View.scrollView [ 
                        prop.position.x.at 0
                        prop.position.y.at 0
                        prop.color (Color.Green, Color.Black)
                        prop.width.filled
                        prop.height.filled
                        scrollView.contentSize (Size(140,120))
                        scrollView.children [
                            View.textView [
                                prop.position.x.at 0
                                prop.position.y.at 0
                                prop.width.fill 2
                                prop.height.fill 2
                                prop.color (Color.Green, Color.Black)
                                textView.wordWrap true
                                textView.text model.fullDocument
                            ]
                        ]
                    ]
                ]
            ]  
        ]
    ]