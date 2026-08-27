namespace DotMad

module Parser =

    type private ParserState =
        { mutable Tokens: Token list
          mutable Pos: int }

    let private peek (state: ParserState) : Token option =
        match state.Tokens with
        | t :: _ -> Some t
        | [] -> None

    let private bump (state: ParserState) : Token option =
        match state.Tokens with
        | t :: rest ->
            state.Tokens <- rest
            state.Pos <- state.Pos + 1
            Some t
        | [] -> None

    let rec private parseExpr (state: ParserState) : NomadResult<Expr> =
        match bump state with
        | Some(Token.NumLit n) -> Ok(Expr.NumLit n)
        | Some(Token.Symbol s) -> Ok(Expr.Symbol s)
        | Some(Token.StringLit s) -> Ok(Expr.StringLit s)
        | Some(Token.BoolLit b) -> Ok(Expr.BoolLit b)
        | Some Token.UnitLit -> Ok Expr.Unit
        | Some Token.LParen -> parseList state
        | Some other -> Error(NomadError.Parse $"Unexpected token: {other}")
        | None -> Error(NomadError.Parse "Cannot parse EOF")

    and private parseList (state: ParserState) : NomadResult<Expr> =
        let items = System.Collections.Generic.List<Expr>()
        let mutable running = true
        let mutable result = Error(NomadError.Parse "unreachable")

        while running do
            match peek state with
            | None
            | Some Token.Eof ->
                result <- Error(NomadError.Parse "Unexpected EOF")
                running <- false
            | Some Token.RParen ->
                bump state |> ignore
                let values = items.ToArray()
                let args = if values.Length = 0 then [||] else values[1..]
                result <- Ok(Expr.List(values, args))
                running <- false
            | _ ->
                match parseExpr state with
                | Ok expr -> items.Add(expr)
                | Error e ->
                    result <- Error e
                    running <- false

        result

    let parseProgram (source: string) : NomadResult<Expr array> =
        match Tokenizer.tokenize source with
        | Error e -> Error e
        | Ok tokens ->
            let state = { Tokens = tokens; Pos = 0 }

            match parseExpr state with
            | Ok(Expr.List(forms, _)) -> Ok forms
            | Ok _ -> Error(NomadError.Parse "Root Expression is not a list")
            | Error e -> Error e

    let parseOne (source: string) : NomadResult<Expr> =
        match Tokenizer.tokenize source with
        | Error e -> Error e
        | Ok tokens ->
            let state = { Tokens = tokens; Pos = 0 }
            parseExpr state
