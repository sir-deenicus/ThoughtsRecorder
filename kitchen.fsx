open System.Text.RegularExpressions

let stripBrackets (input: string) =
    Regex.Replace(input, @"\[.*?\]", "").Trim()

stripBrackets "[NOISE] This is a test [MUSIC]"

stripBrackets "Hello [SILENCE]" 

stripBrackets "No brackets"