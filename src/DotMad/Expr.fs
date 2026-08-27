namespace DotMad

open System

[<RequireQualifiedAccess>]
type Expr =
    | Lambda of string array * Expr
    | Symbol of string
    | NumLit of float
    | StringLit of string
    | BoolLit of bool
    | List of Expr array * Expr array
    | Unit

    member this.IsWildcard =
        match this with
        | Expr.Symbol s -> s = "_"
        | _ -> false

    override this.ToString() =
        match this with
        | Expr.Lambda _ -> "<LAMBDA>"
        | Expr.Symbol s -> $"Symbol('{s}')"
        | Expr.NumLit n -> "Number(" + n.ToString("F6", Globalization.CultureInfo.InvariantCulture) + ")"
        | Expr.StringLit s -> $"String(\"{s}\")"
        | Expr.BoolLit b -> $"Bool({b})"
        | Expr.Unit -> "<UNIT>"
        | Expr.List(items, _) ->
            let inner = items |> Array.map string |> String.concat " "
            $"List({inner})"
