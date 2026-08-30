namespace DotMad

type NomadError =
    | Parse of string
    | Tokenize of string
    | Eval of string
    | Io of string
    | Exit of int

    member this.Report =
        match this with
        | Parse e -> $"Error while parsing: {e}"
        | Tokenize e -> $"Error while tokenizing: {e}"
        | Eval e -> $"Error while evaluating: {e}"
        | Io e -> $"Error while reading file: {e}"
        | Exit code -> $"Error while evaluating: program requested exit with status {code}"

    override this.ToString() =
        match this with
        | Parse e -> $"parse error: {e}"
        | Tokenize e -> $"tokenize error: {e}"
        | Eval e -> $"evaluation error: {e}"
        | Io e -> $"io error: {e}"
        | Exit c -> $"exit({c})"

    static member eval(msg: string) = NomadError.Eval msg

type NomadResult<'T> = Result<'T, NomadError>
