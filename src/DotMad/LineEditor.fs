namespace DotMad

open System
open System.Collections.Generic
open System.Text

module LineEditor =

    type LineReadResult =
        | Submitted of string
        | Cancelled
        | Eof

    let readLine (prompt: string) : LineReadResult =
        if Console.IsInputRedirected then
            Console.Write(prompt)
            Console.Out.Flush()
            let input = Console.ReadLine()
            if isNull input then Eof else Submitted input
        else

            let line = StringBuilder()
            let mutable cursorPos = 0
            let history = List<string>()
            let mutable historyIndex = -1
            let mutable savedInput = ""

            let writePrompt () =
                Console.Write(prompt)
                Console.Out.Flush()

            let redrawLine () =
                Console.Write("\r")
                Console.Write(prompt)
                Console.Write(line.ToString())
                Console.Write(" \b")
                Console.SetCursorPosition(prompt.Length + cursorPos, Console.CursorTop)

            let insertChar (c: char) =
                line.Insert(cursorPos, string c) |> ignore
                cursorPos <- cursorPos + 1

            let deleteCharAtCursor () =
                if cursorPos < line.Length then
                    line.Remove(cursorPos, 1) |> ignore

            let backspace () =
                if cursorPos > 0 then
                    line.Remove(cursorPos - 1, 1) |> ignore
                    cursorPos <- cursorPos - 1

            let clearFromCursorToEnd () =
                line.Remove(cursorPos, line.Length - cursorPos) |> ignore

            writePrompt ()

            let mutable running = true
            let mutable result = Eof

            while running do
                let key = Console.ReadKey(true)

                match key.Key with
                | ConsoleKey.Enter ->
                    let text = line.ToString()

                    if text.Length > 0 then
                        history.Add(text)

                    running <- false
                    result <- Submitted text
                    Console.WriteLine()

                | ConsoleKey.Backspace ->
                    backspace ()
                    redrawLine ()

                | ConsoleKey.Delete ->
                    deleteCharAtCursor ()
                    redrawLine ()

                | ConsoleKey.LeftArrow ->
                    if cursorPos > 0 then
                        cursorPos <- cursorPos - 1
                        Console.SetCursorPosition(prompt.Length + cursorPos, Console.CursorTop)

                | ConsoleKey.RightArrow ->
                    if cursorPos < line.Length then
                        cursorPos <- cursorPos + 1
                        Console.SetCursorPosition(prompt.Length + cursorPos, Console.CursorTop)

                | ConsoleKey.UpArrow ->
                    if history.Count > 0 then
                        if historyIndex = -1 then
                            savedInput <- line.ToString()
                            historyIndex <- history.Count - 1
                        elif historyIndex > 0 then
                            historyIndex <- historyIndex - 1

                        line.Clear() |> ignore
                        line.Append(history[historyIndex]) |> ignore
                        cursorPos <- line.Length
                        redrawLine ()

                | ConsoleKey.DownArrow ->
                    if historyIndex >= 0 then
                        if historyIndex < history.Count - 1 then
                            historyIndex <- historyIndex + 1
                            line.Clear() |> ignore
                            line.Append(history[historyIndex]) |> ignore
                        else
                            historyIndex <- -1
                            line.Clear() |> ignore
                            line.Append(savedInput) |> ignore

                        cursorPos <- line.Length
                        redrawLine ()

                | ConsoleKey.Home ->
                    cursorPos <- 0
                    Console.SetCursorPosition(prompt.Length, Console.CursorTop)

                | ConsoleKey.End ->
                    cursorPos <- line.Length
                    Console.SetCursorPosition(prompt.Length + cursorPos, Console.CursorTop)

                | ConsoleKey.U when key.Modifiers = ConsoleModifiers.Control ->
                    line.Clear() |> ignore
                    cursorPos <- 0
                    redrawLine ()

                | ConsoleKey.K when key.Modifiers = ConsoleModifiers.Control ->
                    clearFromCursorToEnd ()
                    redrawLine ()

                | ConsoleKey.A when key.Modifiers = ConsoleModifiers.Control ->
                    cursorPos <- 0
                    Console.SetCursorPosition(prompt.Length, Console.CursorTop)

                | ConsoleKey.E when key.Modifiers = ConsoleModifiers.Control ->
                    cursorPos <- line.Length
                    Console.SetCursorPosition(prompt.Length + cursorPos, Console.CursorTop)

                | ConsoleKey.C when key.Modifiers = ConsoleModifiers.Control ->
                    Console.WriteLine()
                    running <- false
                    result <- Cancelled

                | ConsoleKey.D when key.Modifiers = ConsoleModifiers.Control ->
                    if line.Length = 0 then
                        Console.WriteLine("Bye!")
                        running <- false
                        result <- Eof

                | _ when key.KeyChar <> '\u0000' ->
                    insertChar key.KeyChar
                    redrawLine ()

                | _ -> ()

            result
