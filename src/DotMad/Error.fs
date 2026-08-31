namespace DotMad

exception NomadException of string
exception NomadParseException of string
exception NomadTokenizeException of string
exception NomadExit of int
exception NomadIoException of string
exception NomadEvalException of string
exception NomadTypeAssertionException of string

type NomadError =
    | Parse of string
    | Tokenize of string
    | Eval of string
    | Io of string
    | Exit of int
    | TypeAssertion of string

    member this.Report =
        match this with
        | Parse e -> $"Error while parsing: {e}"
        | Tokenize e -> $"Error while tokenizing: {e}"
        | Eval e -> $"Error while evaluating: {e}"
        | Io e -> $"Error while reading file: {e}"
        | Exit code -> $"Error while evaluating: program requested exit with status {code}"
        | TypeAssertion e -> $"Error while asserting type: {e}"

    override this.ToString() =
        match this with
        | Parse e -> $"parse error: {e}"
        | Tokenize e -> $"tokenize error: {e}"
        | Eval e -> $"evaluation error: {e}"
        | Io e -> $"io error: {e}"
        | Exit c -> $"exit({c})"
        | TypeAssertion e -> $"type assertion error: {e}"

    static member eval(msg: string) = NomadError.Eval msg
    static member throwNomadError(nomadError: NomadError) =
        match nomadError with
        | NomadError.Eval e -> raise (NomadEvalException e)
        | NomadError.Io e -> raise (NomadIoException e)
        | NomadError.Parse e -> raise (NomadParseException e)
        | NomadError.Tokenize e -> raise (NomadTokenizeException e)
        | NomadError.Exit e -> raise (NomadExit e)
        | NomadError.TypeAssertion e -> raise (NomadTypeAssertionException e)

type NomadResult<'T> = Result<'T, NomadError>
