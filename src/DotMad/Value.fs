namespace DotMad

open System
open System.Collections.Generic

type Env(parent: Env option, capacity: int, slotNames: string array, slotValues: Value array) =
    let bindings = Dictionary<string, Value>(capacity)

    member _.Parent = parent

    member private _.TryGetLocal(key: string) =
        let mutable i = 0
        let mutable found = None

        while i < slotNames.Length && found.IsNone do
            if slotNames[i] = key then found <- Some slotValues[i]
            i <- i + 1

        match found with
        | Some value -> ValueSome value
        | None ->
            match bindings.TryGetValue(key) with
            | true, value -> ValueSome value
            | false, _ -> ValueNone

    member private _.TryMutateLocal(key: string, value: Value) =
        let mutable i = 0
        let mutable found = false

        while i < slotNames.Length && not found do
            if slotNames[i] = key then
                slotValues[i] <- value
                found <- true
            i <- i + 1

        if not found && bindings.ContainsKey(key) then
            bindings[key] <- value
            found <- true

        found

    member this.Get(key: string) : NomadResult<Value> =
        let mutable cur = Some this
        let mutable result = ValueNone

        while cur.IsSome && result.IsNone do
            let env = cur.Value
            result <- env.TryGetLocal(key)
            cur <- env.Parent

        match result with
        | ValueSome value -> Ok value
        | ValueNone -> Error(NomadError.Eval("No such variable: " + key))

    member _.Set(key: string, value: Value) : NomadResult<unit> =
        let mutable duplicate = bindings.ContainsKey(key)
        let mutable i = 0
        while i < slotNames.Length && not duplicate do
            duplicate <- slotNames[i] = key
            i <- i + 1

        if duplicate then
            Error(NomadError.Eval("Cannot bind " + key + ": Already exists in this scope"))
        else
            bindings[key] <- value
            Ok()

    member this.Mutate(key: string, value: Value) : NomadResult<unit> =
        let mutable cur = Some this
        let mutable found = false

        while cur.IsSome && not found do
            let env = cur.Value
            found <- env.TryMutateLocal(key, value)
            cur <- env.Parent

        if found then
            Ok()
        else
            Error(NomadError.Eval("Cannot mutate non-existant binding: " + key))

    member _.IterLocal() : (string * Value) list =
        let items = ResizeArray<string * Value>(slotNames.Length + bindings.Count)
        for i = 0 to slotNames.Length - 1 do items.Add(slotNames[i], slotValues[i])
        for kv in bindings do items.Add(kv.Key, kv.Value)
        List.ofSeq items

    member _.ParentOption = parent

    static member Root() = Env(None, 64, [||], [||])
    static member New(parent: Env) = Env(Some parent, 4, [||], [||])
    static member New(parent: Env, capacity: int) = Env(Some parent, capacity, [||], [||])
    static member NewBound(parent: Env, names: string array, values: Value array) =
        Env(Some parent, 0, names, values)

and [<RequireQualifiedAccess>] NomadList =
    | Nil
    | Cons of Value * NomadList

    static member FromVec(values: Value array) : NomadList =
        let mutable list = NomadList.Nil

        for i in [ values.Length - 1 .. -1 .. 0 ] do
            list <- NomadList.Cons(values[i], list)

        list

    static member Append(left: NomadList, right: NomadList) : NomadList =
        let elems = NomadList.ToVec(left)
        let mutable acc = right

        for i in [ elems.Length - 1 .. -1 .. 0 ] do
            acc <- NomadList.Cons(elems[i], acc)

        acc

    static member ToVec(l: NomadList) : Value array =
        let acc = ResizeArray<Value>()
        let mutable cur = l
        let mutable running = true

        while running do
            match cur with
            | NomadList.Nil -> running <- false
            | NomadList.Cons(h, t) ->
                acc.Add(h)
                cur <- t

        acc.ToArray()

    static member Head(l: NomadList) : Value option =
        match l with
        | NomadList.Cons(h, _) -> Some h
        | NomadList.Nil -> None

    static member Tail(l: NomadList) : NomadList option =
        match l with
        | NomadList.Cons(_, t) -> Some t
        | NomadList.Nil -> None

    static member Len(l: NomadList) : int =
        let mutable count = 0
        let mutable cur = l
        let mutable running = true

        while running do
            match cur with
            | NomadList.Nil -> running <- false
            | NomadList.Cons(_, t) ->
                count <- count + 1
                cur <- t

        count

    static member Get(l: NomadList, index: int) : Value option =
        let mutable cur = l
        let mutable i = index

        while i > 0 do
            match cur with
            | NomadList.Nil -> i <- -1
            | NomadList.Cons(_, t) ->
                cur <- t
                i <- i - 1

        match cur with
        | NomadList.Cons(h, _) -> Some h
        | NomadList.Nil -> None

    static member listEquals(a: NomadList, b: NomadList) : bool =
        let mutable ca = a
        let mutable cb = b
        let mutable running = true
        let mutable result = true

        while running do
            match ca, cb with
            | NomadList.Nil, NomadList.Nil -> running <- false
            | NomadList.Cons(ha, ta), NomadList.Cons(hb, tb) ->
                if not (Value.equals ha hb) then
                    result <- false
                    running <- false
                else
                    ca <- ta
                    cb <- tb
            | _ ->
                result <- false
                running <- false

        result

    override this.ToString() =
        let sb = System.Text.StringBuilder()
        sb.Append('(') |> ignore
        let mutable cur = this
        let mutable first = true
        let mutable running = true

        while running do
            match cur with
            | NomadList.Nil -> running <- false
            | NomadList.Cons(head, tail) ->
                if not first then
                    sb.Append(' ') |> ignore

                sb.Append(string head) |> ignore
                first <- false
                cur <- tail

        sb.Append(')') |> ignore
        sb.ToString()

and NativeImpl = Expr[] -> Env -> NomadResult<Value>

and [<Sealed>] Record(capacity: int) =
    let fields = Dictionary<string, Value>(capacity)
    member _.Fields = fields

    new() = Record(4)

and [<CustomEquality; NoComparison>] Value =
    | Num of float
    | Str of string
    | Bool of bool
    | VList of NomadList
    | RecordVal of Record
    | Lambda of string array * Expr * Env
    | NativeFun of NativeImpl
    | Macro of string array * Expr array
    | Unit

    override this.Equals(other) =
        match other with
        | :? Value as other -> Value.equals this other
        | _ -> false

    override this.GetHashCode() =
        match this with
        | Num x -> hash x
        | Str s -> hash s
        | Bool b -> hash b
        | Unit -> 0
        | VList _ -> 1
        | RecordVal _ -> 2
        | Lambda _ -> 3
        | NativeFun _ -> 4
        | Macro _ -> 5

    static member equals (a: Value) (b: Value) : bool =
        match a, b with
        | Num x, Num y -> x = y
        | Str x, Str y -> x = y
        | Bool x, Bool y -> x = y
        | Unit, Unit -> true
        | VList x, VList y -> NomadList.listEquals (x, y)
        | RecordVal x, RecordVal y -> Object.ReferenceEquals(x, y)
        | Lambda(pa, ba, ea), Lambda(pb, bb, eb) ->
            pa = pb && Object.ReferenceEquals(ba, bb) && Object.ReferenceEquals(ea, eb)
        | NativeFun x, NativeFun y -> System.Delegate.Equals(x, y)
        | Macro(pa, ba), Macro(pb, bb) -> pa = pb && Object.ReferenceEquals(ba, bb)
        | _ -> false

    static member String(s: string) = Value.Str s
    static member Bool_(b: bool) = Value.Bool b
    static member Num_(n: float) = Value.Num n

    static member List_(values: Value array) : Value = Value.VList(NomadList.FromVec values)

    static member Nil: Value = Value.VList NomadList.Nil

    static member Cons(head: Value, tail: NomadList) : Value = Value.VList(NomadList.Cons(head, tail))

    static member Record_() : Value = Value.RecordVal(Record())

    override this.ToString() =
        match this with
        | NativeFun _ -> "<NATIVEFUNCTION>"
        | Lambda _ -> "<FUNCTION>"
        | Macro _ -> "<MACRO>"
        | Num x -> Value.FormatNumber x
        | Str s -> s
        | Bool b -> if b then "true" else "false"
        | VList items -> string items
        | RecordVal _ -> "<RECORD>"
        | Unit -> "<UNIT>"

    static member FormatNumber(x: float) =
        if Double.IsNaN x then
            "nan"
        elif Double.IsPositiveInfinity x then
            "inf"
        elif Double.IsNegativeInfinity x then
            "-inf"
        elif Double.IsFinite(x) && x % 1.0 = 0.0 && abs x < 9223372036854775807.0 then
            string (int64 x)
        else
            x.ToString("F2", Globalization.CultureInfo.InvariantCulture)
