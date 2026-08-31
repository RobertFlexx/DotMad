namespace DotMad

open System
open System.Collections.Generic

module Eval =

    let private coreForms = Dictionary<string, NativeImpl>()

    let mutable private evalFn: Expr -> Env -> NomadResult<Value> =
        fun _ _ -> Error(NomadError.Eval "eval not initialized")

    let eval (expression: Expr) (env: Env) : NomadResult<Value> = evalFn expression env
    
    let evalOrThrow (expression: Expr) (env: Env) : Value =
        match eval expression env with
        | Ok v -> v
        | Error e -> NomadError.throwNomadError e
    
    let getNumber (expr: Expr) (env: Env) : NomadResult<float> =
        match eval expr env with
        | Ok(Num x) -> Ok x
        | Ok other -> Error(NativesUtil.typeErr "number" expr other)
        | Error e -> Error e
        
    let getNumberOrThrow (expr: Expr) (env: Env) : float =
        match eval expr env with
        | Ok(Num x) -> x
        | Error e -> NomadError.throwNomadError e
        | _ -> NomadError.throwNomadError (NomadError.TypeAssertion "Number Expected")

    let getString (expr: Expr) (env: Env) : NomadResult<string> =
        match eval expr env with
        | Ok(Str s) -> Ok s
        | Ok other -> Error(NativesUtil.typeErr "string" expr other)
        | Error e -> Error e
        
    let getStringOrThrow (expr: Expr) (env: Env) : string =
        match eval expr env with
        | Ok(Str s) -> s
        | Error e -> NomadError.throwNomadError e
        | _ -> NomadError.throwNomadError (NomadError.TypeAssertion "String Expected")

    let getBool (expr: Expr) (env: Env) : NomadResult<bool> =
        match eval expr env with
        | Ok(Bool b) -> Ok b
        | Ok other -> Error(NativesUtil.typeErr "bool" expr other)
        | Error e -> Error e
        
    let getBoolOrThrow (expr: Expr) (env: Env) : bool =
        match eval expr env with
        | Ok(Bool b) -> b
        | Error e -> NomadError.throwNomadError e
        | _ -> NomadError.throwNomadError (NomadError.TypeAssertion "Bool Expected")

    let getList (expr: Expr) (env: Env) : NomadResult<NomadList> =
        match eval expr env with
        | Ok(VList l) -> Ok l
        | Ok other -> Error(NativesUtil.typeErr "list" expr other)
        | Error e -> Error e
        
    let getListOrThrow (expr: Expr) (env: Env) : NomadList =
        match eval expr env with
        | Ok(VList l) -> l
        | Error e -> NomadError.throwNomadError e
        | _ -> NomadError.throwNomadError (NomadError.TypeAssertion "List Expected")

    let getRecord (expr: Expr) (env: Env) : NomadResult<Record> =
        match eval expr env with
        | Ok(RecordVal r) -> Ok r
        | Ok other -> Error(NativesUtil.typeErr "record" expr other)
        | Error e -> Error e
        
    let getRecordOrThrow (expr: Expr) (env: Env) : Record =
        match eval expr env with
        | Ok(RecordVal r) -> r
        | Error e -> NomadError.throwNomadError e
        | _ -> NomadError.throwNomadError (NomadError.TypeAssertion "Record Expected")

    let predicate (params_: Expr array) (env: Env) (name: string) (f: Value -> bool) : NomadResult<Value> =
        if params_.Length <> 1 then
            Error(NativesUtil.arity name 1 params_.Length)
        else
            match eval params_[0] env with
            | Ok v -> Ok(Bool(f v))
            | Error(NomadError.Eval _) -> Ok(Bool false)
            | Error e -> Error e

    let rec substitute (expr: Expr) (table: Dictionary<string, Expr>) : Expr =
        match expr with
        | Expr.Symbol name ->
            match table.TryGetValue(name) with
            | true, replacement -> replacement
            | false, _ -> expr
        | Expr.List(items, _) ->
            let values = Array.map (fun e -> substitute e table) items
            Expr.List(values, if values.Length = 0 then [||] else values[1..])
        | _ -> expr

    let evalSeq (expressions: Expr array) (env: Env) : NomadResult<Value> =
        let mutable last = Value.Unit
        let mutable i = 0
        let mutable err = None

        while i < expressions.Length && err.IsNone do
            match eval expressions[i] env with
            | Ok v -> last <- v
            | Error e -> err <- Some e

            i <- i + 1

        match err with
        | Some e -> Error e
        | None -> Ok last

    let private coreHandle (name: string) (native: NativeImpl) : NativeImpl =
        coreForms[name] <- native
        native

    let private isCoreForm (name: string) (native: NativeImpl) =
        match coreForms.TryGetValue(name) with
        | true, expected -> Object.ReferenceEquals(expected, native)
        | false, _ -> false

    let coreIfImpl: NativeImpl =
        coreHandle "if" (fun params_ env ->
            if params_.Length <> 3 then
                Error(NativesUtil.arity "if" 3 params_.Length)
            else
                match eval params_[0] env with
                | Ok(Bool true) -> eval params_[1] env
                | Ok(Bool false) -> eval params_[2] env
                | Ok other -> Error(NomadError.Eval $"Condition of if-construct does not evaluate to a bool: {other}")
                | Error e -> Error e)

    let coreDoImpl: NativeImpl =
        coreHandle "do" (fun params_ env -> evalSeq params_ env)

    let coreSwitchImpl: NativeImpl =
        coreHandle "switch" (fun params_ env ->
            if params_.Length < 2 then
                Error(NativesUtil.arity "switch" 2 params_.Length)
            else
                match eval params_[0] env with
                | Error e -> Error e
                | Ok scrutinee ->
                    let mutable taken = None
                    let mutable malformed = false
                    let mutable resultError = None
                    let mutable i = 1

                    while i < params_.Length && taken.IsNone && not malformed do
                        match params_[i] with
                        | Expr.List(items, _) when items.Length = 2 ->
                            let matcher, onMatch = items[0], items[1]

                            if matcher.IsWildcard then
                                taken <- Some onMatch
                            else
                                match eval matcher env with
                                | Ok v when Value.equals v scrutinee -> taken <- Some onMatch
                                | Ok _ -> ()
                                | Error e ->
                                    malformed <- true
                                    resultError <- Some e
                        | _ -> malformed <- true

                        i <- i + 1

                    if resultError.IsSome then
                        Error resultError.Value
                    elif malformed then
                        Error(NomadError.Eval "Malformed switch-arm syntax")
                    else
                        match taken with
                        | Some onMatch -> eval onMatch env
                        | None -> Ok Unit)

    let coreScopedImpl: NativeImpl =
        coreHandle "scoped" (fun params_ env ->
            match params_ with
            | [| Expr.List(bindingPairs, _); body |] ->
                let thisEnv = Env.New(env, bindingPairs.Length)
                let mutable err = None
                let mutable i = 0

                while i < bindingPairs.Length && err.IsNone do
                    match bindingPairs[i] with
                    | Expr.List(pairItems, _) when pairItems.Length = 2 ->
                        match pairItems[0] with
                        | Expr.Symbol name ->
                            match eval pairItems[1] env with
                            | Ok v ->
                                match thisEnv.Set(name, v) with
                                | Error e -> err <- Some e
                                | Ok() -> ()
                            | Error e -> err <- Some e
                        | _ ->
                            err <-
                                Some(
                                    NomadError.Eval
                                        "Bad Syntax! The binding list is in the wrong form! (Expected '(name value)')"
                                )
                    | _ ->
                        err <-
                            Some(
                                NomadError.Eval
                                    "Bad Syntax! The binding list is in the wrong form! (Expected '(name value)')"
                            )

                    i <- i + 1

                match err with
                | Some e -> Error e
                | None -> eval body thisEnv
            | _ -> Error(NativesUtil.arity "scoped" 2 params_.Length))

    let coreTryImpl: NativeImpl =
        coreHandle "try" (fun params_ env ->
            if params_.Length <> 2 then
                Error(NativesUtil.arity "try" 2 params_.Length)
            else
                match eval params_[0] env with
                | Ok value -> Ok value
                | Error(NomadError.Eval _) -> eval params_[1] env
                | Error e -> Error e)

    let init () =
        evalFn <-
            fun expression env ->
                let mutable cursor = expression
                let mutable scope = env
                let handlers = ResizeArray<Expr * Env>(4)
                let mutable finalResult = Error(NomadError.Eval "unreachable")
                let mutable running = true

                let bail (e: NomadError) : bool =
                    match e with
                    | NomadError.Eval _ when handlers.Count > 0 ->
                        let hExpr, hEnv = handlers[handlers.Count - 1]
                        handlers.RemoveAt(handlers.Count - 1)
                        cursor <- hExpr
                        scope <- hEnv
                        true
                    | _ ->
                        finalResult <- Error e
                        running <- false
                        false

                while running do
                    match cursor with
                    | Expr.NumLit x ->
                        finalResult <- Ok(Num x)
                        running <- false
                    | Expr.StringLit s ->
                        finalResult <- Ok(Str s)
                        running <- false
                    | Expr.BoolLit b ->
                        finalResult <- Ok(Bool b)
                        running <- false
                    | Expr.Unit ->
                        finalResult <- Ok Unit
                        running <- false

                    | Expr.Symbol name ->
                        match scope.Get(name) with
                        | Ok v ->
                            finalResult <- Ok v
                            running <- false
                        | Error e -> bail e |> ignore

                    | Expr.Lambda(ps, body) ->
                        finalResult <- Ok(Lambda(ps, body, scope))
                        running <- false

                    | Expr.List(items, argExprs) ->
                        if items.Length = 0 then
                            finalResult <- Ok Value.Nil
                            running <- false
                        else
                            let fnExpr = items[0]
                            let argCount = items.Length - 1

                            match eval fnExpr scope with
                            | Error e -> bail e |> ignore
                            | Ok func ->
                                match func with
                                | NativeFun callback ->
                                    let coreName =
                                        match fnExpr with
                                        | Expr.Symbol s when isCoreForm s callback -> Some s
                                        | _ -> None

                                    if coreName.IsSome then
                                            let sym =
                                                coreName.Value

                                            match sym with
                                            | "if" ->
                                                if argCount <> 3 then
                                                    bail (NativesUtil.arity "if" 3 argCount) |> ignore
                                                else
                                                    match eval items[1] scope with
                                                    | Error e -> bail e |> ignore
                                                    | Ok(Bool true) -> cursor <- items[2]
                                                    | Ok(Bool false) -> cursor <- items[3]
                                                    | Ok other ->
                                                        bail (
                                                            NomadError.Eval
                                                                $"Condition of if-construct does not evaluate to a bool: {other}"
                                                        )
                                                        |> ignore

                                            | "do" ->
                                                if argCount = 0 then
                                                    finalResult <- Ok Unit
                                                    running <- false
                                                else
                                                    let mutable doErr = false
                                                    let mutable i = 0

                                                    while i < argCount - 1 && not doErr do
                                                        match eval items[i + 1] scope with
                                                        | Ok _ -> i <- i + 1
                                                        | Error e ->
                                                            doErr <- true
                                                            bail e |> ignore

                                                    if not doErr then
                                                        cursor <- items[argCount]

                                            | "switch" ->
                                                if argCount < 2 then
                                                    bail (NativesUtil.arity "switch" 2 argCount) |> ignore
                                                else
                                                    match eval items[1] scope with
                                                    | Error e -> bail e |> ignore
                                                    | Ok scrutinee ->
                                                        let mutable taken = None
                                                        let mutable si = 1
                                                        let mutable switchError = false

                                                        while si < argCount && taken.IsNone && not switchError do
                                                            match items[si + 1] with
                                                            | Expr.List(caseItems, _) when caseItems.Length = 2 ->
                                                                if caseItems[0].IsWildcard then
                                                                    taken <- Some caseItems[1]
                                                                else
                                                                    match eval caseItems[0] scope with
                                                                    | Ok v when Value.equals v scrutinee ->
                                                                        taken <- Some caseItems[1]
                                                                    | Ok _ -> ()
                                                                    | Error e ->
                                                                        switchError <- true
                                                                        bail e |> ignore
                                                            | _ ->
                                                                switchError <- true
                                                                bail (NomadError.Eval "Malformed switch-arm syntax")
                                                                |> ignore

                                                            si <- si + 1

                                                        if not switchError then
                                                            match taken with
                                                            | Some m -> cursor <- m
                                                            | None ->
                                                                finalResult <- Ok Unit
                                                                running <- false

                                            | "scoped" ->
                                                if argCount = 2 then
                                                    match items[1] with
                                                    | Expr.List(bindingPairs, _) ->
                                                        let body = items[2]
                                                        let thisEnv = Env.New(scope, bindingPairs.Length)
                                                        let mutable scErr = false
                                                        let mutable bi = 0

                                                        while bi < bindingPairs.Length && not scErr do
                                                            match bindingPairs[bi] with
                                                            | Expr.List(pairItems, _) when pairItems.Length = 2 ->
                                                                match pairItems[0] with
                                                                | Expr.Symbol bname ->
                                                                    match eval pairItems[1] scope with
                                                                    | Ok v ->
                                                                        match thisEnv.Set(bname, v) with
                                                                        | Error e ->
                                                                            scErr <- true
                                                                            bail e |> ignore
                                                                        | Ok() -> bi <- bi + 1
                                                                    | Error e ->
                                                                        scErr <- true
                                                                        bail e |> ignore
                                                                | _ ->
                                                                    scErr <- true
                                                                    bail (
                                                                        NomadError.Eval
                                                                            "Bad Syntax! The binding list is in the wrong form! (Expected '(name value)')"
                                                                    ) |> ignore
                                                            | _ ->
                                                                scErr <- true
                                                                bail (
                                                                    NomadError.Eval
                                                                        "Bad Syntax! The binding list is in the wrong form! (Expected '(name value)')"
                                                                ) |> ignore

                                                        if not scErr then
                                                            cursor <- body
                                                            scope <- thisEnv
                                                    | _ -> bail (NativesUtil.arity "scoped" 2 argCount) |> ignore
                                                else
                                                    bail (NativesUtil.arity "scoped" 2 argCount) |> ignore

                                            | "try" ->
                                                if argCount <> 2 then
                                                    bail (NativesUtil.arity "try" 2 argCount) |> ignore
                                                else
                                                    handlers.Add(items[2], scope)
                                                    cursor <- items[1]

                                            | _ -> ()
                                        else
                                            match callback argExprs scope with
                                            | Ok v ->
                                                finalResult <- Ok v
                                                running <- false
                                            | Error e -> bail e |> ignore
                                | Lambda(ps, body, closure) ->
                                    if ps.Length <> argCount then
                                        bail (
                                            NomadError.Eval
                                                $"Attempted to invoke lambda with wrong amount of params. Expected: {ps.Length} got: {argCount}"
                                        )
                                        |> ignore
                                    else
                                        let values = Array.zeroCreate<Value> ps.Length
                                        let mutable lamErr = false
                                        let mutable ai = 0

                                        while ai < ps.Length && not lamErr do
                                            match eval items[ai + 1] scope with
                                            | Ok v ->
                                                let mutable duplicate = false
                                                let mutable j = 0
                                                while j < ai && not duplicate do
                                                    duplicate <- ps[j] = ps[ai]
                                                    j <- j + 1

                                                if duplicate then
                                                    lamErr <- true
                                                    bail (NomadError.Eval $"Cannot bind {ps[ai]}: Already exists in this scope")
                                                    |> ignore
                                                else
                                                    values[ai] <- v
                                                    ai <- ai + 1
                                            | Error e ->
                                                lamErr <- true
                                                bail e |> ignore

                                        if not lamErr then
                                            cursor <- body
                                            scope <- Env.NewBound(closure, ps, values)

                                | Macro(ps, body) ->
                                    if ps.Length <> argCount then
                                        bail (
                                            NomadError.Eval
                                                $"Attempted to invoke macro with wrong amount of params. Expected: {ps.Length} got: {argCount}"
                                        )
                                        |> ignore
                                    else
                                        let table = Dictionary<string, Expr>()

                                        for i in 0 .. ps.Length - 1 do
                                            table[ps[i]] <- items[i + 1]

                                        cursor <- substitute (Expr.List(body, if body.Length = 0 then [||] else body[1..])) table

                                | other ->
                                    bail (
                                        NomadError.Eval $"Attempt to invoke non-function/non-macro: {fnExpr} ({other})"
                                    )
                                    |> ignore

                finalResult
