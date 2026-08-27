namespace DotMad

open System
open System.IO

module NativesUtil =

    let arity (name: string) (expected: int) (got: int) : NomadError =
        NomadError.Eval
            $"Native function {name} was given bad syntax. Perhaps it was given the wrong amount of args? Args expected: {expected}. Got: {got}"

    let typeErr (expected: string) (expr: Expr) (got: Value) : NomadError =
        NomadError.Eval $"This expression was expected to evaluate to a {expected}, but it didn't: {expr} ({got})"

    let flushStdout () = Console.Out.Flush()

    let printStr (s: string) =
        Console.Write(s)
        flushStdout ()

    let printLine (s: string) =
        Console.WriteLine(s)
        flushStdout ()

    let mulString (s: string) (factor: float) : string =
        let n =
            if Double.IsNaN factor then
                0
            elif not (Double.IsFinite factor) then
                1000000
            else
                try
                    int factor
                with _ ->
                    0

        if n < 1 then
            ""
        else
            s |> String.replicate (Math.Clamp(n, 0, 1000000))

    let isOcamlSpace (c: char) : bool =
        c = ' ' || c = '\t' || c = '\n' || c = '\r' || c = '\x0B' || c = '\x0C'

    let spaceChars = [| ' '; '\t'; '\n'; '\r'; '\x0B'; '\x0C' |]

    let lowercaseAscii (s: string) : string =
        s
        |> String.map (fun c ->
            if c >= 'A' && c <= 'Z' then char (int c + 32) else c)

    let parseFloat (text: string) : float option = Tokenizer.parseNomadFloat text

    let floatToI32Code (x: float) : int =
        if Double.IsNaN x then 0
        elif x >= float Int32.MaxValue then Int32.MaxValue
        elif x <= float Int32.MinValue then Int32.MinValue
        else int x

    let createDir0755 (path: string) : Result<unit, exn> =
        try
            ignore (Directory.CreateDirectory(path))
            Ok()
        with ex ->
            Error ex

    let symbolParams (params_: Expr array) (lowercaseError: bool) : NomadResult<string array> =
        let out = ResizeArray<string>()
        let mutable err = None
        let mutable i = 0

        while i < params_.Length && err.IsNone do
            match params_[i] with
            | Expr.Symbol s -> out.Add(s)
            | _ ->
                err <-
                    Some(
                        NomadError.Eval(
                            if lowercaseError then
                                "Non-symbol in parameter list"
                            else
                                "Non-Symbol in parameter list"
                        )
                    )

            i <- i + 1

        match err with
        | Some e -> Error e
        | None -> Ok(out.ToArray())
