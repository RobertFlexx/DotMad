namespace DotMad

open System

module Program =

    let helpText =
        @"  \\
   \\
  //\\
 //  \\

The Magnificent DotMad Interpretation System

Omit all arguments to enter REPL Mode.
Use the -e | --eval flag to evaluate an expression which is passed as an argument.
	Example: dotmad -e '(+ 1 2)' # => 3

You can also pass a file to be run as a script.
	Example: dotmad my_script.nomad
For More information, visit:
https://github.com/Moritisimor/nomad-lisp"

    let outLine (s: string) =
        Console.WriteLine(s)
        Console.Out.Flush()

    let repl () =
        let argv = Environment.GetCommandLineArgs() |> Array.skip 1
        let interpreter = Interpreter(argv)
        let mutable running = true

        while running do
            match LineEditor.readLine "DotMad \u03bb " with
            | LineEditor.Submitted input ->
                match interpreter.DoString(input) with
                | Ok value -> outLine $"Evaluates to: {value}"
                | Error(NomadError.Exit code) -> Environment.Exit(code)
                | Error e -> outLine e.Report
            | LineEditor.Cancelled -> ()
            | LineEditor.Eof -> running <- false

    [<EntryPoint>]
    let main argv =
        match argv with
        | [| "--help" |]
        | [| "-h" |] ->
            outLine helpText
            0
        | [| "-e"; expr |]
        | [| "--eval"; expr |] ->
            let interpreter = Interpreter(argv |> Array.toSeq)

            match interpreter.DoString(expr) with
            | Ok value ->
                outLine (string value)
                0
            | Error(NomadError.Exit code) -> code
            | Error e ->
                outLine e.Report
                1
        | [| "--repl" |]
        | [| "-r" |] ->
            repl ()
            0
        | [| file |] ->
            let interpreter = Interpreter(argv |> Array.toSeq)

            match interpreter.DoFile(file) with
            | Ok() -> 0
            | Error(NomadError.Exit code) -> code
            | Error e ->
                outLine e.Report
                1
        | [||] ->
            repl ()
            0
        | _ ->
            let interpreter = Interpreter(argv |> Array.toSeq)
            let file = argv[0]

            match interpreter.DoFile(file) with
            | Ok() -> 0
            | Error(NomadError.Exit code) -> code
            | Error e ->
                outLine e.Report
                1
