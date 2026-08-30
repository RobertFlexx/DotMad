namespace DotMad

open System
open System.Globalization

[<RequireQualifiedAccess>]
type Token =
    | LParen
    | RParen
    | NumLit of float
    | BoolLit of bool
    | StringLit of string
    | UnitLit
    | Symbol of string
    | Eof

    override this.ToString() =
        match this with
        | Token.LParen -> "LPAREN"
        | Token.RParen -> "RPAREN"
        | Token.NumLit x when Double.IsNaN x -> "NUMLIT(nan)"
        | Token.NumLit x -> "NUMLIT(" + x.ToString("F2") + ")"
        | Token.BoolLit b -> $"BOOLLIT({b})"
        | Token.StringLit s -> $"STRINGLIT(\"{s}\")"
        | Token.UnitLit -> "UNITLITERAL"
        | Token.Symbol s -> $"SYMBOL('{s}')"
        | Token.Eof -> "EOF"

module Tokenizer =

    let private isDelim (c: char) =
        c = '(' || c = ')' || c = ' ' || c = '\t' || c = '\n'

    let private hexDigitValue (c: char) : uint32 option =
        if c >= '0' && c <= '9' then
            Some(uint32 (int c - int '0'))
        elif c >= 'a' && c <= 'f' then
            Some(uint32 (int c - int 'a') + 10u)
        elif c >= 'A' && c <= 'F' then
            Some(uint32 (int c - int 'A') + 10u)
        else
            None

    let parseNomadFloat (text: string) : float option =
        let s = text.Replace("_", "")

        match Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture) with
        | true, n -> Some n
        | _ ->
            if s.Length < 2 then
                None
            else
                let negative = s[0] = '-'
                let body = if s[0] = '-' || s[0] = '+' then s.Substring(1) else s

                if not (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && body.Length > 2) then
                    None
                else
                    let rest = body.Substring(2)

                    let mantissaStr, expStr =
                        match rest.IndexOfAny([| 'p'; 'P' |]) with
                        | -1 -> (rest, "0")
                        | idx -> (rest.Substring(0, idx), rest.Substring(idx + 1))

                    let intPart, fracPart =
                        match mantissaStr.IndexOf('.') with
                        | -1 -> (mantissaStr, "")
                        | idx -> (mantissaStr.Substring(0, idx), mantissaStr.Substring(idx + 1))

                    if intPart.Length = 0 && fracPart.Length = 0 then
                        None
                    else
                        let mutable mantissa = 0.0
                        let mutable ok = true

                        for c in intPart do
                            if ok then
                                match hexDigitValue c with
                                | Some digit -> mantissa <- mantissa * 16.0 + float digit
                                | None -> ok <- false

                        let mutable scale = 1.0 / 16.0

                        for c in fracPart do
                            if ok then
                                match hexDigitValue c with
                                | Some digit ->
                                    mantissa <- mantissa + float digit * scale
                                    scale <- scale / 16.0
                                | None -> ok <- false

                        if not ok then
                            None
                        else
                            let exp =
                                match Int32.TryParse(expStr, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) with
                                | true, e -> e
                                | _ -> Int32.MinValue

                            if exp = Int32.MinValue then
                                None
                            else
                                let value = mantissa * (2.0 ** float exp)
                                Some(if negative then -value else value)

    let private scanNumber (chars: char array) (i: int) : NomadResult<Token * int> =
        let mutable i = i
        let start = i

        while i < chars.Length && not (isDelim chars[i]) do
            i <- i + 1

        let text = String(chars, start, i - start)

        match parseNomadFloat text with
        | Some n -> Ok(Token.NumLit n, i)
        | None -> Error(NomadError.Tokenize($"Could not parse {text} to a number"))

    let private scanString (chars: char array) (i: int) : NomadResult<Token * int> =
        let mutable i = i
        let out = System.Text.StringBuilder()
        let mutable running = true
        let mutable result = Error(NomadError.Tokenize("unreachable"))

        while running do
            if i >= chars.Length then
                result <- Error(NomadError.Tokenize($"String literal was never ended (Got \"{out})"))
                running <- false
            else
                match chars[i] with
                | '"' ->
                    result <- Ok(Token.StringLit(string out), i + 1)
                    running <- false
                | '\\' ->
                    if i + 1 >= chars.Length then
                        out.Append('\\') |> ignore
                        i <- i + 1
                    else
                        match chars[i + 1] with
                        | 'n' ->
                            out.Append('\n') |> ignore
                            i <- i + 2
                        | 't' ->
                            out.Append('\t') |> ignore
                            i <- i + 2
                        | 'r' ->
                            out.Append('\r') |> ignore
                            i <- i + 2
                        | 'b' ->
                            out.Append('\x08') |> ignore
                            i <- i + 2
                        | '"' ->
                            out.Append('"') |> ignore
                            i <- i + 2
                        | c ->
                            out.Append('\\').Append(c) |> ignore
                            i <- i + 2
                | c ->
                    out.Append(c) |> ignore
                    i <- i + 1

        result

    let private scanSymbol (chars: char array) (i: int) : NomadResult<Token * int> =
        let mutable i = i
        let start = i

        while i < chars.Length && not (isDelim chars[i]) do
            i <- i + 1

        let name = String(chars, start, i - start)
        Ok(Token.Symbol name, i)

    let private keywordAt (chars: char array) (i: int) (word: string) : bool =
        let w = word.ToCharArray()
        let ``end`` = i + w.Length

        if ``end`` > chars.Length then
            false
        else
            let mutable match_ = true
            let mutable j = 0

            while j < w.Length && match_ do
                if chars[i + j] <> w[j] then
                    match_ <- false

                j <- j + 1

            if not match_ then
                false
            else
                ``end`` = chars.Length || isDelim chars[``end``]

    let private countParens (tokens: Token array) =
        let mutable l = 0
        let mutable r = 0

        for t in tokens do
            match t with
            | Token.LParen -> l <- l + 1
            | Token.RParen -> r <- r + 1
            | _ -> ()

        (l, r)

    let tokenize (source: string) : NomadResult<Token list> =
        let chars = source.ToCharArray()
        let tokens = System.Collections.Generic.List<Token>()
        let mutable i = 0
        let mutable err = None

        while i < chars.Length && err.IsNone do
            let c = chars[i]

            match c with
            | ' '
            | '\t'
            | '\n'
            | '\r' -> i <- i + 1
            | '#' ->
                while i < chars.Length && chars[i] <> '\n' do
                    i <- i + 1
            | '(' ->
                tokens.Add(Token.LParen)
                i <- i + 1
            | ')' ->
                tokens.Add(Token.RParen)
                i <- i + 1
            | '"' ->
                match scanString chars (i + 1) with
                | Ok(tok, next) ->
                    tokens.Add(tok)
                    i <- next
                | Error e -> err <- Some e
            | '-' when i + 1 < chars.Length && Char.IsAsciiDigit(chars[i + 1]) ->
                match scanNumber chars i with
                | Ok(tok, next) ->
                    tokens.Add(tok)
                    i <- next
                | Error e -> err <- Some e
            | c when Char.IsAsciiDigit(c) ->
                match scanNumber chars i with
                | Ok(tok, next) ->
                    tokens.Add(tok)
                    i <- next
                | Error e -> err <- Some e
            | _ ->
                if keywordAt chars i "true" then
                    tokens.Add(Token.BoolLit true)
                    i <- i + 4
                elif keywordAt chars i "false" then
                    tokens.Add(Token.BoolLit false)
                    i <- i + 5
                elif keywordAt chars i "unit" then
                    tokens.Add(Token.UnitLit)
                    i <- i + 4
                else
                    match scanSymbol chars i with
                    | Ok(tok, next) ->
                        tokens.Add(tok)
                        i <- next
                    | Error e -> err <- Some e

        match err with
        | Some e -> Error e
        | None ->
            let tokenArr = tokens.ToArray()
            let wrapped = Array.zeroCreate<Token> (tokenArr.Length + 3)
            wrapped[0] <- Token.LParen
            Array.Copy(tokenArr, 0, wrapped, 1, tokenArr.Length)
            wrapped[tokenArr.Length + 1] <- Token.RParen
            wrapped[tokenArr.Length + 2] <- Token.Eof

            let l, r = countParens wrapped

            if l = r then
                Ok(List.ofArray wrapped)
            elif l > r then
                Error(NomadError.Tokenize "Unbalanced parantheses: one or more unclosed left parantheses")
            else
                Error(NomadError.Tokenize "Unbalanced parantheses: one or more superfluous right parantheses")
