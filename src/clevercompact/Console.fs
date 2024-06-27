namespace CleverCompact

open Printf

type Console () as this =

    let mutable lastOutWasMultiLine = false

    member _.Printf fmt = lock this (fun () ->
        let result = fprintf System.Console.Out fmt
        lastOutWasMultiLine <- false
        result)

    member _.Printfn fmt = lock this (fun () ->
        let result = fprintfn System.Console.Out fmt
        lastOutWasMultiLine <- false
        result)

    member _.Eprintf fmt = lock this (fun () ->
        let result = fprintf System.Console.Error fmt
        lastOutWasMultiLine <- false
        result)

    member _.Eprintfn fmt = lock this (fun () ->
        let result = fprintfn System.Console.Error fmt
        lastOutWasMultiLine <- false
        result)

    member this.UpdateLines (lines: string list) =
        lock this (fun () ->
            match lines with
            | [] -> ()
            | lines ->
                let lineCount = List.length lines

                match lastOutWasMultiLine with
                | false -> List.iter (fun line -> this.Printfn "%s" line) lines
                | true ->

                    let struct (_, y) = System.Console.GetCursorPosition ()
                    let newY = max (y - lineCount) 0
                    let cleanLine = Array.init System.Console.BufferWidth (fun _ -> ' ') |> System.String

                    System.Console.SetCursorPosition (0, newY)
                    for i in 0..(lineCount - 1) do
                        this.Printfn "%s" cleanLine
                    System.Console.SetCursorPosition (0, newY)
                    List.iter (fun line -> this.Printfn "%s" line) lines

                lastOutWasMultiLine <- true)

