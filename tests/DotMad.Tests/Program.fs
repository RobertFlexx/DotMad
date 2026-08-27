open DotMad

let fail source message = failwith $"{source}: {message}"

let run source =
    match Interpreter().DoString(source) with
    | Ok value -> value
    | Error error -> fail source $"evaluation failed: {error}"

let expect source expected =
    let actual = string (run source)
    if actual <> expected then fail source $"expected {expected}, got {actual}"

let expectEvalError source =
    match Interpreter().DoString(source) with
    | Error(NomadError.Eval _) -> ()
    | Error error -> fail source $"wrong error category: {error}"
    | Ok value -> fail source $"unexpectedly returned {value}"

[<EntryPoint>]
let main _ =
    expect "(+ (* 10 5) (- 1000 250))" "800"
    expect "(let truest 55) truest" "55"
    expect "(let x 10) (let y 20) (+ x y)" "30"
    expect "(let add (lambda (x) (lambda (y) (+ x y)))) (let add10 (add 10)) (add10 20)" "30"
    expect "(letfun fact (n) (switch n (0 1) (_ (* n (fact (dec n)))))) (fact 10)" "3628800"
    expect "(do 1 2 3)" "3"
    expect "(try (throw \"boom\") \"caught\")" "caught"
    expect "(+ 0x1A 1_000)" "1026"
    expect "(= unit unit)" "true"
    expect "(chars \"héllo\")" "(h é l l o)"
    expect "(strlen \"héllo\")" "5"
    expect "(strlen \"a😀b\")" "3"
    expect "(lower \"ÉABC\")" "Éabc"
    expect "(letfun loop (n acc) (if (= n 0) acc (loop (dec n) (inc acc)))) (loop 200000 0)" "200000"
    expect "(scoped ((if do)) (if 1 2 3))" "3"
    expect "(let r (record (x 0))) (try (+ (throw \"x\") (record_mut r x 1)) unit) (. r x)" "0"
    expect "(let r (record (x 0))) (try (cons (record_mut r x 1) missing) unit) (. r x)" "0"
    expectEvalError "(throw \"first\") (+ 1 2)"
    expectEvalError "(switch 1 (missing 2) (_ 3))"
    expectEvalError "(switch 1)"

    match Interpreter().DoString("(try (exit 7) 0)") with
    | Error(NomadError.Exit 7) -> 0
    | _ -> fail "exit" "exit was swallowed"
