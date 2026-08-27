namespace DotMad

open System
open NativesUtil

module Natives =

    let inline private bind2 first second combine =
        match first () with
        | Error error -> Error error
        | Ok left ->
            match second () with
            | Error error -> Error error
            | Ok right -> combine left right

    let coreNatives () : (string * NativeImpl) list =
        [ "throw",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "throw" 1 params_.Length)
              else
                  match Eval.eval params_[0] env with
                  | Ok(Str s) -> Error(NomadError.Eval s)
                  | Ok other -> Error(NomadError.Eval $"Cannot throw non-string: {other}")
                  | Error e -> Error e

          "letmac",
          fun params_ env ->
              if params_.Length < 3 then
                  Error(arity "letmac" 3 params_.Length)
              else
                  match params_[0], params_[1] with
                  | Expr.Symbol name, Expr.List(macParams, _) ->
                      match symbolParams macParams true with
                      | Error e -> Error e
                      | Ok ps ->
                          let body = params_[2..]
                          env.Set(name, Macro(ps, body)) |> Result.map (fun () -> Unit)
                  | _ -> Error(NomadError.Eval "letmac: first argument must be a symbol, second must be a list")

          "let",
          fun params_ env ->
              match params_ with
              | [| Expr.Symbol bindingName; bindingExpr |] ->
                  match Eval.eval bindingExpr env with
                  | Ok evaluated -> env.Set(bindingName, evaluated) |> Result.map (fun () -> Unit)
                  | Error e -> Error e
              | _ -> Error(arity "let" 2 params_.Length)

          "letfun",
          fun params_ env ->
              match params_ with
              | [| Expr.Symbol name; Expr.List(funParams, _); body |] ->
                  match symbolParams funParams false with
                  | Error e -> Error e
                  | Ok paramList -> env.Set(name, Lambda(paramList, body, env)) |> Result.map (fun () -> Unit)
              | _ -> Error(arity "letfun" 3 params_.Length)

          "mut",
          fun params_ env ->
              match params_ with
              | [| Expr.Symbol bindingName; bindingValue |] ->
                  match Eval.eval bindingValue env with
                  | Ok evaluated -> env.Mutate(bindingName, evaluated) |> Result.map (fun () -> Unit)
                  | Error e -> Error e
              | _ -> Error(arity "mut" 2 params_.Length)

          "lambda",
          fun params_ env ->
              match params_ with
              | [| Expr.List(funParams, _); body |] ->
                  match symbolParams funParams false with
                  | Error e -> Error e
                  | Ok paramList -> Ok(Lambda(paramList, body, env))
              | _ -> Error(arity "lambda" 2 params_.Length)

          "record",
          fun params_ env ->
              let record = Record(params_.Length)
              let mutable err = None
              let mutable i = 0

              while i < params_.Length && err.IsNone do
                  match params_[i] with
                  | Expr.List(field, _) when field.Length = 2 ->
                      match field[0], field[1] with
                      | Expr.Symbol fieldName, fieldExpr ->
                          match Eval.eval fieldExpr env with
                          | Ok evaluated -> record.Fields[fieldName] <- evaluated
                          | Error e -> err <- Some e
                      | _ -> err <- Some(NomadError.Eval "Record field has bad syntax")
                  | _ -> err <- Some(NomadError.Eval "Record field has bad syntax")

                  i <- i + 1

              match err with
              | Some e -> Error e
              | None -> Ok(Value.RecordVal record)

          ".",
          fun params_ env ->
              match params_ with
              | [| recordExpr; Expr.Symbol fieldName |] ->
                  match Eval.eval recordExpr env with
                  | Ok(RecordVal record) ->
                      if record.Fields.ContainsKey(fieldName) then
                          Ok(record.Fields[fieldName])
                      else
                          Error(NomadError.Eval $"Attempt to access non-existant field of record: {fieldName}")
                  | Ok other -> Error(NomadError.Eval $"Attempt to access field of non-record expression: {other}")
                  | Error e -> Error e
              | _ -> Error(arity "." 2 params_.Length)

          "record_mut",
          fun params_ env ->
              match params_ with
              | [| recordExpr; Expr.Symbol fieldName; newExpr |] ->
                  match Eval.eval recordExpr env with
                  | Ok(RecordVal record) ->
                      if not (record.Fields.ContainsKey(fieldName)) then
                          Error(NomadError.Eval $"Cannot mutate non-existant field: {fieldName}")
                      else
                          match Eval.eval newExpr env with
                          | Ok evaluated ->
                              record.Fields[fieldName] <- evaluated
                              Ok Unit
                          | Error e -> Error e
                  | Ok other -> Error(NomadError.Eval $"Attempt to mutate field of non-record expression: {other}")
                  | Error e -> Error e
              | _ -> Error(arity "record_mut" 3 params_.Length)

          "+",
          fun params_ env ->
              match params_ with
              | [| lhs; rhs |] ->
                  bind2 (fun () -> Eval.eval lhs env) (fun () -> Eval.eval rhs env) (fun x y ->
                      match x, y with
                      | Num a, Num b -> Ok(Num(a + b))
                      | Str a, Str b -> Ok(Value.String(a + b))
                      | _ -> Error(NomadError.Eval $"Cannot add these expressions: {x} and {y}"))
              | _ -> Error(arity "+" 2 params_.Length)

          "-",
          fun params_ env ->
              match params_ with
              | [| lhs; rhs |] ->
                  bind2 (fun () -> Eval.getNumber lhs env) (fun () -> Eval.getNumber rhs env) (fun x y -> Ok(Num(x - y)))
              | _ -> Error(arity "-" 2 params_.Length)

          "*",
          fun params_ env ->
              match params_ with
              | [| lhs; rhs |] ->
                  bind2 (fun () -> Eval.eval lhs env) (fun () -> Eval.eval rhs env) (fun x y ->
                      match x, y with
                      | Num a, Num b -> Ok(Num(a * b))
                      | Num a, Str s
                      | Str s, Num a -> Ok(Value.String(mulString s a))
                      | _ -> Error(NomadError.Eval $"Cannot multiply these expressions: {x} and {y}"))
              | _ -> Error(arity "*" 2 params_.Length)

          "/",
          fun params_ env ->
              match params_ with
              | [| lhs; rhs |] ->
                  bind2 (fun () -> Eval.getNumber lhs env) (fun () -> Eval.getNumber rhs env) (fun x y ->
                      if y = 0.0 then
                          Error(NomadError.Eval "Attempt to divide by 0")
                      else
                          Ok(Num(x / y)))
              | _ -> Error(arity "/" 2 params_.Length)

          "mod",
          fun params_ env ->
              match params_ with
              | [| lhs; rhs |] ->
                  bind2 (fun () -> Eval.getNumber lhs env) (fun () -> Eval.getNumber rhs env) (fun x y -> Ok(Num(x % y)))
              | _ -> Error(arity "mod" 2 params_.Length)

          "=",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.eval a env) (fun () -> Eval.eval b env) (fun x y -> Ok(Bool(Value.equals x y)))
              | _ -> Error(arity "=" 2 params_.Length)

          ">",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.getNumber a env) (fun () -> Eval.getNumber b env) (fun x y -> Ok(Bool(x > y)))
              | _ -> Error(arity ">" 2 params_.Length)

          ">=",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.getNumber a env) (fun () -> Eval.getNumber b env) (fun x y -> Ok(Bool(x >= y)))
              | _ -> Error(arity ">=" 2 params_.Length)

          "<",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.getNumber a env) (fun () -> Eval.getNumber b env) (fun x y -> Ok(Bool(x < y)))
              | _ -> Error(arity "<" 2 params_.Length)

          "<=",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.getNumber a env) (fun () -> Eval.getNumber b env) (fun x y -> Ok(Bool(x <= y)))
              | _ -> Error(arity "<=" 2 params_.Length)

          "or",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  match Eval.getBool a env with
                  | Ok true -> Ok(Bool true)
                  | Ok false -> Eval.getBool b env |> Result.map Bool
                  | Error e -> Error e
              | _ -> Error(arity "or" 2 params_.Length)

          "and",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  match Eval.getBool a env with
                  | Ok false -> Ok(Bool false)
                  | Ok true -> Eval.getBool b env |> Result.map Bool
                  | Error e -> Error e
              | _ -> Error(arity "and" 2 params_.Length)

          "list",
          fun params_ env ->
              let values = ResizeArray<Value>()
              let mutable err = None
              let mutable i = 0

              while i < params_.Length && err.IsNone do
                  match Eval.eval params_[i] env with
                  | Ok v -> values.Add(v)
                  | Error e -> err <- Some e

                  i <- i + 1

              match err with
              | Some e -> Error e
              | None -> Ok(Value.List_(values.ToArray()))

          "append",
          fun params_ env ->
              match params_ with
              | [| a; b |] ->
                  bind2 (fun () -> Eval.getList a env) (fun () -> Eval.getList b env) (fun x y ->
                      Ok(Value.VList(NomadList.Append(x, y))))
              | _ -> Error(arity "append" 2 params_.Length)

          "car",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "car" 1 params_.Length)
              else
                  match Eval.getList params_[0] env with
                  | Ok l -> Ok(Option.defaultValue Unit (NomadList.Head(l)))
                  | Error e -> Error e

          "cdr",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "cdr" 1 params_.Length)
              else
                  match Eval.getList params_[0] env with
                  | Ok l ->
                      match NomadList.Tail(l) with
                      | Some tail -> Ok(Value.VList tail)
                      | None -> Ok Unit
                  | Error e -> Error e

          "cons",
          fun params_ env ->
              match params_ with
              | [| e; l |] ->
                  bind2 (fun () -> Eval.getList l env) (fun () -> Eval.eval e env) (fun tailList head ->
                      Ok(Value.Cons(head, tailList)))
              | _ -> Error(arity "cons" 2 params_.Length)

          "sprint",
          fun params_ env ->
              let acc = System.Text.StringBuilder()
              let mutable err = None
              let mutable i = 0

              while i < params_.Length && err.IsNone do
                  match Eval.eval params_[i] env with
                  | Ok v -> acc.Append(string v) |> ignore
                  | Error e -> err <- Some e

                  i <- i + 1

              match err with
              | Some e -> Error e
              | None -> Ok(Value.String(string acc))

          "print",
          fun params_ env ->
              let out = System.Text.StringBuilder()
              let mutable err = None
              let mutable i = 0

              while i < params_.Length && err.IsNone do
                  match Eval.eval params_[i] env with
                  | Ok v -> out.Append(string v) |> ignore
                  | Error e -> err <- Some e

                  i <- i + 1

              match err with
              | Some e -> Error e
              | None ->
                  printStr (string out)
                  Ok Unit

          "println",
          fun params_ env ->
              let out = System.Text.StringBuilder()
              let mutable err = None
              let mutable i = 0

              while i < params_.Length && err.IsNone do
                  match Eval.eval params_[i] env with
                  | Ok v -> out.Append(string v) |> ignore
                  | Error e -> err <- Some e

                  i <- i + 1

              match err with
              | Some e -> Error e
              | None ->
                  printLine (string out)
                  Ok Unit

          "readln",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "readln" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok prompt ->
                      printStr prompt
                      let line = Console.ReadLine()

                      if isNull line then
                          Error(NomadError.Eval "readln: reached end of input")
                      else
                          Ok(Value.String(line.TrimEnd([| '\n'; '\r' |])))

          "chars",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "chars" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok s ->
                      Ok(Value.List_(s.EnumerateRunes() |> Seq.map (fun rune -> Value.String(rune.ToString())) |> Array.ofSeq))

          "lower",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "lower" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok s -> Ok(Value.String(lowercaseAscii s))

          "trim",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "trim" 1 params_.Length)
              else
                  match Eval.eval params_[0] env with
                  | Ok(Str s) -> Ok(Value.String(s.TrimStart(spaceChars).TrimEnd(spaceChars)))
                  | Ok other -> Error(NomadError.Eval $"Cannot apply trim-operation on non-string expression: {other}")
                  | Error e -> Error e

          "splitws",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "splitws" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok s ->
                      let parts =
                          s.Split(' ')
                          |> Array.filter (fun p -> not (String.IsNullOrEmpty(p.TrimStart(spaceChars))))
                          |> Array.map Value.String

                      Ok(Value.List_ parts)

          "to_string",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "to_string" 1 params_.Length)
              else
                  match Eval.eval params_[0] env with
                  | Ok v -> Ok(Value.String(string v))
                  | Error e -> Error e

          "string_to_num",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "string_to_num" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok s ->
                      match parseFloat s with
                      | Some n -> Ok(Num n)
                      | None -> Error(NomadError.Eval $"Cannot parse this string to a number: {s}")

          "isunit",
          fun params_ env ->
              Eval.predicate params_ env "isunit" (fun v ->
                  match v with
                  | Unit -> true
                  | _ -> false)
          "isstr",
          fun params_ env ->
              Eval.predicate params_ env "isstring" (fun v ->
                  match v with
                  | Str _ -> true
                  | _ -> false)
          "isnum",
          fun params_ env ->
              Eval.predicate params_ env "isnum" (fun v ->
                  match v with
                  | Num _ -> true
                  | _ -> false)
          "islist",
          fun params_ env ->
              Eval.predicate params_ env "islist" (fun v ->
                  match v with
                  | VList _ -> true
                  | _ -> false)
          "isfun",
          fun params_ env ->
              Eval.predicate params_ env "islambda" (fun v ->
                  match v with
                  | Lambda _ -> true
                  | _ -> false)
          "isnative",
          fun params_ env ->
              Eval.predicate params_ env "isnative" (fun v ->
                  match v with
                  | NativeFun _ -> true
                  | _ -> false)
          "ismac",
          fun params_ env ->
              Eval.predicate params_ env "ismac" (fun v ->
                  match v with
                  | Macro _ -> true
                  | _ -> false)
          "isbool",
          fun params_ env ->
              Eval.predicate params_ env "isbool" (fun v ->
                  match v with
                  | Bool _ -> true
                  | _ -> false)
          "isrecord",
          fun params_ env ->
              Eval.predicate params_ env "isrecord" (fun v ->
                  match v with
                  | RecordVal _ -> true
                  | _ -> false) ]
