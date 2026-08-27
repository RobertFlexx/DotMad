# DotMad

A .NET implementation of [nomad-lisp](https://github.com/Moritisimor/nomad-lisp), a small, dynamically typed Lisp.
 
DotMad is a faithful port of the original interpreter: the same syntax, standard library, evaluation order, and familiar errors. It runs on .NET 10 and includes a REPL with line editing and history. Its evaluator is stack-safe, so tail-recursive Nomad programs can run for hundreds of thousands of calls without growing the .NET stack.

## Building

```bash
make
```

The compiled binary lands in `src/DotMad/bin/Release/net10.0/DotMad.dll`.

Run the cross-implementation conformance tests with `make test`. `make clean` removes all generated `bin` and `obj` directories.

## Usage

```bash
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll                       # REPL
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll --repl                # REPL
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll -e '(+ 1 2)'          # evaluate expression
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll examples/fib.nomad    # run script
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll --help                # help
```

Or use `dotnet run`:

```bash
dotnet run --project src/DotMad -- -e '(+ 1 2)'
```

## Quick tour

Everything is an expression, everything returns a value. There is no `void`. When there is nothing meaningful to return, you get `unit`.

```lisp
# arithmetic
(+ 1 2)          # => 3
(* 6 7)          # => 42
(+ (* 10 5) (- 1000 250))  # => 800

# variables
(let x 10)
(let y 20)
(+ x y)          # => 30

# functions
(letfun greet (name) (println "Hello, " name))
(greet "World")  # prints: Hello, World

# conditionals
(if true "yes" "no")  # => "yes"

# pattern matching
(switch 2
  (1 "one")
  (2 "two")
  (_ "other"))    # => "two"

# closures and currying
(let add (lambda (x) (lambda (y) (+ x y))))
(let add10 (add 10))
(add10 20)        # => 30

# recursion
(letfun fact (n)
  (switch n
    (0 1)
    (_ (* n (fact (- n 1))))))
(fact 10)         # => 3628800
```

## Language overview

### Variables

```lisp
(let x 10)        # bind x in current scope
(mut x 42)        # mutate an existing binding

(scoped ((a 1) (b 2))
  (+ a b))         # => 3, a and b are local to this block
```

You can shadow outer bindings, but you cannot rebind within the same scope. Use `mut` to change an existing value.

### Functions

```lisp
# letfun is the normal way to define named functions
(letfun add (a b) (+ a b))

# lambda creates anonymous functions
(let double (lambda (x) (* x 2)))

# functions are values, pass them around
(map (lambda (x) (* x x)) (list 1 2 3))
# => (1 4 9)
```

Arity is strict. Calling a function with the wrong number of arguments is an error.

### Macros

Macros do textual substitution before evaluation. They receive unevaluated expressions.

```lisp
(letmac unless (cond yes no)
  if cond no yes)

(unless false
  (println "this runs")
  (println "this does not"))
```

The standard library includes `when`, `unless`, and `!=` as macros.

### Lists

Lists are persistent (immutable) linked lists. `cons` adds to the front, `car` gets the first element, `cdr` gets the rest.

```lisp
(let numbers (list 1 2 3 4 5))
(car numbers)       # => 1
(cdr numbers)       # => (2 3 4 5)
(cons 0 numbers)    # => (0 1 2 3 4 5)
(append (list 1 2) (list 3 4))  # => (1 2 3 4)
(len numbers)       # => 5
```

### Records

Records are mutable dictionaries with named fields.

```lisp
(let person (record (name "Alice") (age 30)))
(. person name)         # => "Alice"
(record_mut person age 31)
(. person age)          # => 31
```

### Error handling

```lisp
(try
  (throw "something went wrong")
  (println "caught it"))
# prints: caught it

(try
  (/ 1 0)
  (println "division by zero"))
# prints: division by zero
```

### Type checking

```lisp
(isstr "hello")     # => true
(isnum 42)          # => true
(isbool true)       # => true
(islist (list 1 2)) # => true
(isfun (lambda () unit)) # => true
(isunit unit)       # => true
(typeof 42)         # => "number"
```

### Standard library

The standard library is loaded automatically and includes:

| Function | Description |
|----------|-------------|
| `not` | Boolean negation |
| `inc` / `dec` | Increment / decrement by 1 |
| `when` / `unless` | Conditional macros |
| `map` / `filter` / `foldl` | List operations |
| `rev` | Reverse a list |
| `len` | List length |
| `strlen` | String length |
| `chars` | String to character list |
| `foreach` / `foreachi` | Iterate over list |
| `nth` / `nth_unit` | Get element at index |
| `range` | Slice a list by index range |
| `list_init` | Create list from index function |
| `begins_with` / `ends_with` | List prefix/suffix check |
| `has_prefix` / `has_suffix` | String prefix/suffix check |

### I/O

```lisp
(println "hello" "world")     # prints: helloworld
(print "no newline")          # prints without newline
(sprint "a" "b" "c")          # returns "abc" without printing
(readln "Enter name: ")       # read line with prompt
```

### File and OS operations

```lisp
(read_file "file.txt")
(write_file "file.txt" "content")
(read_dir ".")
(mkdir "new_dir")
(remove_file "file.txt")
(remove_dir "old_dir")
(cwd)
(chdir "/tmp")
(get_env "HOME")
(exec "ls -la")
(exit 0)
```

## Examples

See the `examples/` directory for working programs:

- **calc.nomad** - interactive calculator
- **counter.nomad** - simple counting loop
- **fib.nomad** - fibonacci sequence
- **map.nomad** - list mapping
- **people.nomad** - records and iteration
- **morse.nomad** - text to morse code converter

Run any example with:

```bash
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll examples/fib.nomad
```

## Embedding guide

DotMad can be used as a library inside your own .NET projects. You add a project reference, create an interpreter, evaluate nomad expressions, and read the results back as .NET values.

### Adding the reference

In your project file, add a reference to DotMad:

**C# (.csproj)**

```xml
<ItemGroup>
  <ProjectReference Include="../DotMad/src/DotMad/DotMad.fsproj" />
</ItemGroup>
```

**F# (.fsproj)**

```xml
<ItemGroup>
  <ProjectReference Include="../DotMad/src/DotMad/DotMad.fsproj" />
</ItemGroup>
```

**VB.NET (.vbproj)**

```xml
<ItemGroup>
  <ProjectReference Include="..\DotMad\src\DotMad\DotMad.fsproj" />
</ItemGroup>
```

Adjust the path to wherever you cloned DotMad.

---

### C# guide

```csharp
using DotMad;

// create an interpreter with the standard library loaded
var interp = new Interpreter();

// evaluate an expression, get a Result<Value, NomadError> back
var result = interp.DoString("(+ 1 2)");

// pattern match on the result
if (result is Ok<Value> ok)
    Console.WriteLine(ok.Value);  // 3
else if (result is Err<NomadError> err)
    Console.WriteLine(err.Error.Report);
```

**Working with values**

The `Value` type is a discriminated union. You pattern match to get the .NET type out:

```csharp
var result = interp.DoString("(list 1 2 3)");

if (result is Ok<Value> ok)
{
    switch (ok.Value)
    {
        case Num n:
            Console.WriteLine($"Got a number: {n.Item}");
            break;
        case Str s:
            Console.WriteLine($"Got a string: {s.Item}");
            break;
        case Bool b:
            Console.WriteLine($"Got a bool: {b.Item}");
            break;
        case VList list:
            Console.WriteLine("Got a list");
            break;
        case Unit:
            Console.WriteLine("Got unit");
            break;
    }
}
```

**Running nomad scripts**

Load and execute a `.nomad` file:

```csharp
var interp = new Interpreter();
var err = interp.DoFile("scripts/my_tool.nomad");
if (err is Err<NomadError> e)
    Console.WriteLine($"Script failed: {e.Error.Report}");
```

**Multi-line programs**

`DoString` handles multiple top-level forms. It evaluates them in order and returns the last value:

```csharp
var result = interp.DoString("""
    (let x 10)
    (let y 20)
    (+ x y)
    """);

if (result is Ok<Value> ok && ok.Value is Num n)
    Console.WriteLine(n.Item);  // 30
```

**Setting and getting variables**

You can push .NET values into the interpreter and pull nomad values out:

```csharp
var interp = new Interpreter();

// set a variable from C#
interp.GlobalEnv.Set("name", Value.String("Alice"));
interp.GlobalEnv.Set("age", Value.Num_(30));

// use it in nomad
var result = interp.DoString("(println name \" is \" age \" years old\")");

// read a variable set by nomad
interp.DoString("(let answer 42)");
var answer = interp.GetGlobal("answer");
if (answer is Ok<Value> ok && ok.Value is Num n)
    Console.WriteLine(n.Item);  // 42
```

**Passing script arguments**

The constructor can take arguments that become the `args` list inside nomad:

```csharp
var interp = new Interpreter(new[] { "myapp", "input.csv", "--verbose" });
interp.DoString("(println (car args))");  // prints: myapp
```

**Error handling**

All errors come back as `NomadError` with five cases:

```csharp
var result = interp.DoString("(+ 1 \"two\")");
if (result is Err<NomadError> err)
{
    // quick human-readable message
    Console.WriteLine(err.Error.Report);

    // or match specific error types
    switch (err.Error)
    {
        case NomadError.Parse p:
            Console.WriteLine($"Syntax error: {p.Item}");
            break;
        case NomadError.Eval e:
            Console.WriteLine($"Runtime error: {e.Item}");
            break;
        case NomadError.Tokenize t:
            Console.WriteLine($"Token error: {t.Item}");
            break;
        case NomadError.Io io:
            Console.WriteLine($"IO error: {io.Item}");
            break;
    }
}
```

**Registering custom functions**

You can add new functions to the interpreter that call back into your C# code:

```csharp
interp.RegisterNative("greet", (Expr[] args, Env env) =>
{
    // evaluate the first argument
    var result = Eval.eval(args[0], env);
    if (result is Ok<Value> ok && ok.Value is Str name)
        return new Ok<Value>(Value.String($"Hello, {name.Item}!"));
    return new Err<NomadError>(NomadError.eval("greet expects a string"));
});

var r = interp.DoString("(greet \"World\")");
// r => Ok(Str "Hello, World!")
```

**Full C# example**

```csharp
using DotMad;

var interp = new Interpreter();

// push some config in
interp.GlobalEnv.Set("api_url", Value.String("https://api.example.com"));
interp.GlobalEnv.Set("timeout", Value.Num_(30));

// run a nomad config script
var err = interp.DoFile("config.nomad");
if (err is Err<NomadError> e)
{
    Console.WriteLine($"Config failed: {e.Error.Report}");
    return 1;
}

// pull results out
var result = interp.DoString("(get_config)");
if (result is Ok<Value> ok)
    Console.WriteLine($"Got: {ok.Value}");

return 0;
```

---

### F# guide

```fsharp
open DotMad

let interp = Interpreter()

// evaluate an expression
let result = interp.DoString "(+ 1 2)"
printfn "%A" result  // Ok (Num 3.0)
```

**Working with values**

F# pattern matching makes this natural:

```fsharp
let printValue value =
    match value with
    | Num n -> printfn "Number: %g" n
    | Str s -> printfn "String: %s" s
    | Bool b -> printfn "Bool: %b" b
    | VList _ -> printfn "List"
    | Unit -> printfn "Unit"
    | _ -> printfn "Other"

match interp.DoString "(list 1 2 3)" with
| Ok v -> printValue v
| Error e -> printfn "Error: %s" e.Report
```

**Running nomad scripts**

```fsharp
match interp.DoFile "scripts/my_tool.nomad" with
| Ok () -> printfn "Script completed"
| Error e -> printfn "Script failed: %s" e.Report
```

**Multi-line programs**

```fsharp
let result = interp.DoString """
    (let x 10)
    (let y 20)
    (+ x y)
    """

match result with
| Ok (Num n) -> printfn "Result: %g" n  // Result: 30
| Error e -> printfn "Error: %s" e.Report
| _ -> ()
```

**Setting and getting variables**

```fsharp
// push values from F#
interp.GlobalEnv.Set("name", Value.String "Alice") |> ignore
interp.GlobalEnv.Set("age", Value.Num_ 30) |> ignore

interp.DoString "(println name)" |> ignore

// pull values out
interp.DoString "(let answer 42)" |> ignore
match interp.GetGlobal "answer" with
| Ok (Num n) -> printfn "Answer: %g" n  // Answer: 42
| _ -> ()
```

**Passing script arguments**

```fsharp
let interp = Interpreter([| "myapp"; "input.csv"; "--verbose" |])
interp.DoString "(println args)" |> ignore  // ("myapp" "input.csv" "--verbose")
```

**Error handling**

```fsharp
match interp.DoString "(+ 1 \"two\")" with
| Ok value -> printfn "Got: %A" value
| Error (NomadError.Parse msg) -> printfn "Syntax error: %s" msg
| Error (NomadError.Eval msg) -> printfn "Runtime error: %s" msg
| Error (NomadError.Io msg) -> printfn "IO error: %s" msg
| Error other -> printfn "Error: %A" other
```

**Registering custom functions**

```fsharp
interp.RegisterNative("double", fun args env ->
    match Eval.eval args[0] env with
    | Ok (Num n) -> Ok (Value.Num_(n * 2.0))
    | Ok _ -> Error (NomadError.eval "double expects a number")
    | Error e -> Error e
) |> ignore

interp.DoString "(double 21)"  // Ok (Num 42.0)
```

**Full F# example**

```fsharp
open DotMad

let interp = Interpreter()

interp.GlobalEnv.Set("config_path", Value.String "settings.json") |> ignore

match interp.DoFile "app.nomad" with
| Ok () ->
    match interp.DoString "(get_setting \"theme\")" with
    | Ok (Str theme) -> printfn "Theme: %s" theme
    | Ok v -> printfn "Unexpected: %A" v
    | Error e -> printfn "Error: %s" e.Report
| Error e ->
    eprintfn "Failed to load app: %s" e.Report
    1
```

---

### VB.NET guide

```vb
Imports DotMad

Module Program
    Dim interp As New Interpreter()

    Sub Main()
        ' evaluate an expression
        Dim result = interp.DoString("(+ 1 2)")

        ' check the result
        If TypeOf result Is Ok(Of Value) Then
            Dim ok = DirectCast(result, Ok(Of Value))
            Console.WriteLine(ok.Value)  ' 3
        ElseIf TypeOf result Is Err(Of NomadError) Then
            Dim err = DirectCast(result, Err(Of NomadError))
            Console.WriteLine(err.Error.Report)
        End If
    End Sub
End Module
```

**Working with values**

```vb
Dim result = interp.DoString("(list 1 2 3)")

If TypeOf result Is Ok(Of Value) Then
    Dim ok = DirectCast(result, Ok(Of Value))
    Select Case ok.Value
        Case TypeOf ok.Value Is Num
            Dim n = DirectCast(ok.Value, Num)
            Console.WriteLine("Number: " & n.Item.ToString())
        Case TypeOf ok.Value Is Str
            Dim s = DirectCast(ok.Value, Str)
            Console.WriteLine("String: " & s.Item)
        Case TypeOf ok.Value Is Bool
            Dim b = DirectCast(ok.Value, Bool)
            Console.WriteLine("Bool: " & b.Item.ToString())
        Case TypeOf ok.Value Is Unit
            Console.WriteLine("Unit")
    End Select
End If
```

**Running nomad scripts**

```vb
Dim err = interp.DoFile("scripts/my_tool.nomad")
If TypeOf err Is Err(Of NomadError) Then
    Dim e = DirectCast(err, Err(Of NomadError))
    Console.WriteLine("Script failed: " & e.Error.Report)
End If
```

**Multi-line programs**

```vb
Dim code = "
    (let x 10)
    (let y 20)
    (+ x y)
"

Dim result = interp.DoString(code)
If TypeOf result Is Ok(Of Value) Then
    Dim ok = DirectCast(result, Ok(Of Value))
    If TypeOf ok.Value Is Num Then
        Dim n = DirectCast(ok.Value, Num)
        Console.WriteLine("Result: " & n.Item.ToString())  ' Result: 30
    End If
End If
```

**Setting and getting variables**

```vb
' push values from VB
interp.GlobalEnv.Set("name", Value.String("Alice"))
interp.GlobalEnv.Set("age", Value.Num_(30))

interp.DoString("(println name)")

' pull values out
interp.DoString("(let answer 42)")
Dim answer = interp.GetGlobal("answer")
If TypeOf answer Is Ok(Of Value) Then
    Dim ok = DirectCast(answer, Ok(Of Value))
    If TypeOf ok.Value Is Num Then
        Dim n = DirectCast(ok.Value, Num)
        Console.WriteLine("Answer: " & n.Item.ToString())  ' 42
    End If
End If
```

**Passing script arguments**

```vb
Dim interp = New Interpreter({"myapp", "input.csv", "--verbose"})
interp.DoString("(println (car args))")  ' myapp
```

**Error handling**

```vb
Dim result = interp.DoString("(+ 1 ""two"")")
If TypeOf result Is Err(Of NomadError) Then
    Dim err = DirectCast(result, Err(Of NomadError))
    Console.WriteLine(err.Error.Report)

    If TypeOf err.Error Is NomadError.Eval Then
        Dim e = DirectCast(err.Error, NomadError.Eval)
        Console.WriteLine("Runtime error: " & e.Item)
    ElseIf TypeOf err.Error Is NomadError.Parse Then
        Dim p = DirectCast(err.Error, NomadError.Parse)
        Console.WriteLine("Syntax error: " & p.Item)
    End If
End If
```

**Registering custom functions**

```vb
interp.RegisterNative("add_ten", Function(args As Expr(), env As Env)
    Dim result = Eval.eval(args(0), env)
    If TypeOf result Is Ok(Of Value) Then
        Dim ok = DirectCast(result, Ok(Of Value))
        If TypeOf ok.Value Is Num Then
            Dim n = DirectCast(ok.Value, Num)
            Return New Ok(Of Value)(Value.Num_(n.Item + 10))
        End If
    End If
    Return New Err(Of NomadError)(NomadError.eval("add_ten expects a number"))
End Function)
```

**Full VB.NET example**

```vb
Imports DotMad

Module Program
    Sub Main()
        Dim interp = New Interpreter()

        interp.GlobalEnv.Set("app_name", Value.String("MyApp"))
        interp.GlobalEnv.Set("version", Value.String("1.0"))

        Dim err = interp.DoFile("app.nomad")
        If TypeOf err Is Err(Of NomadError) Then
            Dim e = DirectCast(err, Err(Of NomadError))
            Console.WriteLine("Failed: " & e.Error.Report)
        End If
    End Sub
End Module
```

---

### Value type reference

These are the nomad value types you can pattern match on when reading results back:

| Nomad type | .NET type | How to get the value |
|---|---|---|
| number | `Num` | `.Item` gives `float` |
| string | `Str` | `.Item` gives `string` |
| boolean | `Bool` | `.Item` gives `bool` |
| list | `VList` | contains a `NomadList` (use `NomadList.Head`, `NomadList.Tail`, `NomadList.ToVec`) |
| record | `RecordVal` | contains a `Record` with a `.Fields` dictionary |
| function | `Lambda` | contains params, body, and captured environment |
| unit | `Unit` | no payload |

To create values from your code:

| Value | C# | F# | VB.NET |
|---|---|---|---|
| number | `Value.Num_(42)` | `Value.Num_ 42` | `Value.Num_(42)` |
| string | `Value.String("hi")` | `Value.String "hi"` | `Value.String("hi")` |
| bool | `Value.Bool_(true)` | `Value.Bool_ true` | `Value.Bool_(True)` |
| empty list | `Value.Nil` | `Value.Nil` | `Value.Nil` |
| list | `Value.List_(new[] { a, b })` | `Value.List_ [| a; b |]` | `Value.List_({a, b})` |

## Project structure

```
src/DotMad/
  Error.fs          error types (Parse, Tokenize, Eval, Io, Exit)
  Expr.fs           AST nodes
  Token.fs          tokenizer
  Parser.fs         recursive descent parser
  Value.fs          runtime values, environments, nomad lists
  NativesUtil.fs    helper functions for native implementations
  Eval.fs           evaluator with tail call optimization
  Natives.fs        built-in functions (arithmetic, lists, strings, etc.)
  NativesOs.fs      OS functions (file I/O, exec, env vars)
  Stdlib.fs         embedded standard library source
  Interpreter.fs    interpreter class, stdlib loading
  LineEditor.fs     REPL with arrow keys, history, line editing
  Program.fs        entry point (REPL, -e, script execution)
```

## How it works

DotMad uses a trampoline evaluator. Instead of recursing into `eval` for every sub-expression, the evaluator runs a loop. When it encounters a tail call, it just updates the current expression and environment and loops again. This means deeply recursive tail-recursive functions run in constant stack space.

Core forms like `if`, `do`, `switch`, `scoped`, and `try` are handled inline in the trampoline loop. They set the next expression to evaluate instead of calling `eval` recursively.

Lambda calls create a new environment, bind the parameters, and set the body as the next expression to evaluate. No call stack growth.

Macro expansion substitutes symbols in the macro body with the caller's unevaluated arguments, then evaluates the result.

The REPL uses `Console.ReadKey` for raw input and implements cursor movement, history navigation, and common keyboard shortcuts (Ctrl+A, Ctrl+E, Ctrl+U, Ctrl+K).

## Testing

Run the built-in examples:

```bash
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll -e '(+ 1 2)'           # => 3
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll -e '(letfun fact (n) (switch n (0 1) (_ (* n (fact (- n 1)))))) (fact 10)'
# => 3628800
```

Or test interactively in the REPL:

```bash
dotnet src/DotMad/bin/Release/net10.0/DotMad.dll
DotMad lambda (let x 10)
DotMad lambda (let y 20)
DotMad lambda (+ x y)
Evaluates to: 30
```

## License

MIT, same as nomad-lisp.
