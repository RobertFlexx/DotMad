namespace DotMad

open System

module private InterpreterData =
    let stdlibForms =
        lazy (Stdlib.stdlibSrc |> Array.map Parser.parseProgram)

type Interpreter(globalEnv: Env) =

    do Eval.init ()

    new() = Interpreter(ResizeArray<string>())

    new(args: string seq) =
        Eval.init ()
        let env = Env.Root()
        let argValues = args |> Seq.map (fun a -> Value.String a) |> Array.ofSeq
        env.Set("args", Value.List_(argValues)) |> ignore

        for name, f in Natives.coreNatives () do
            env.Set(name, NativeFun f) |> ignore

        for name, f in NativesOs.osNatives () do
            env.Set(name, NativeFun f) |> ignore

        env.Set("if", NativeFun Eval.coreIfImpl) |> ignore
        env.Set("do", NativeFun Eval.coreDoImpl) |> ignore
        env.Set("switch", NativeFun Eval.coreSwitchImpl) |> ignore
        env.Set("scoped", NativeFun Eval.coreScopedImpl) |> ignore
        env.Set("try", NativeFun Eval.coreTryImpl) |> ignore

        Interpreter.loadStdlib env
        Interpreter(env)

    member _.DoString(source: string) : NomadResult<Value> =
        match Parser.parseProgram source with
        | Error e -> Error e
        | Ok forms -> Eval.evalSeq forms globalEnv
        
    member i.DoStringOrThrow(source: string) : Value =
        match i.DoString source with
        | Ok v -> v
        | Error e -> NomadError.throwNomadError e

    member _.DoFile(path: string) : NomadResult<unit> =
        try
            let source = System.IO.File.ReadAllText(path)

            match Parser.parseProgram source with
            | Error e -> Error e
            | Ok forms ->
                let mutable last = Ok()
                let mutable i = 0

                while i < forms.Length do
                    match Eval.eval forms[i] globalEnv with
                    | Ok _ -> i <- i + 1
                    | Error e ->
                        last <- Error e
                        i <- forms.Length

                last
        with ex ->
            Error(NomadError.Io(ex.Message))
            
    member i.DoFileOrThrow(path: string) : unit =
        match i.DoFile path with
        | Ok _ -> ()
        | Error e -> NomadError.throwNomadError e
        

    member _.EvalExpr(expr: Expr) : NomadResult<Value> = Eval.eval expr globalEnv

    member _.GlobalEnv = globalEnv

    member _.GetGlobal(name: string) : NomadResult<Value> = globalEnv.Get(name)

    member _.RegisterNative(name: string, f: NativeImpl) : NomadResult<unit> = globalEnv.Set(name, NativeFun f)
    
    member _.RegisterNativeCS(name: string, f: System.Func<Expr array, Env, NomadResult<Value>>) : NomadResult<unit> =
        globalEnv.Set(name, NativeFun (FuncConvert.FromFunc(f)))

    static member private loadStdlib(env: Env) =
        for parsed in InterpreterData.stdlibForms.Value do
            match parsed with
            | Error e -> Console.Error.WriteLine($"stdlib failed to load: {e}")
            | Ok forms ->
                match Eval.evalSeq forms env with
                | Error e -> Console.Error.WriteLine($"stdlib failed to load: {e}")
                | Ok _ -> ()
