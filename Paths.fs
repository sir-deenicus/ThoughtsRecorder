module Paths

open System
open System.IO

type PathProvider() =
    let baseDir = 
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThoughtsRecorder")
    
    let autoSaveDir = Path.Combine(baseDir, "autosave")
     
    member this.SaveContent content = 
        if not(File.Exists(autoSaveDir)) then
            Directory.CreateDirectory(autoSaveDir) |> ignore 

        File.WriteAllText(this.AutoSavePath, content) 
    member _.AutoSavePath = autoSaveDir 
        
 

