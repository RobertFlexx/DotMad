namespace DotMad

open NativesUtil

module NativesOs =

    let osNatives () : (string * NativeImpl) list =
        [ "exec",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "exec" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok cmd ->
                      try
                          let psi = System.Diagnostics.ProcessStartInfo("sh")
                          psi.ArgumentList.Add("-c")
                          psi.ArgumentList.Add(cmd)
                          psi.UseShellExecute <- false
                          let proc = System.Diagnostics.Process.Start(psi)
                          proc.WaitForExit()
                          let raw = proc.ExitCode * 256
                          Ok(Num(float raw))
                      with _ ->
                          Ok(Num 127.0)

          "exit",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "exit" 1 params_.Length)
              else
                  match Eval.getNumber params_[0] env with
                  | Ok code -> Error(NomadError.Exit(floatToI32Code code))
                  | Error e -> Error e

          "bye", fun _params _env -> Error(NomadError.Exit 0)

          "print_env",
          fun params_ env ->
              if params_.Length <> 0 then
                  Error(arity "print_env" 0 params_.Length)
              else
                  let mutable current = Some env
                  let mutable idx = 0

                  while current.IsSome do
                      let scope = current.Value
                      printLine $"Scope {idx}:"
                      let entries = scope.IterLocal() |> List.sortBy fst

                      for (k, v) in entries do
                          printLine $"\t{k}: {v}"

                      current <- scope.ParentOption
                      idx <- idx + 1

                  flushStdout ()
                  Ok Unit

          "include",
          fun params_ env ->
              match params_ with
              | [| Expr.Symbol path |] ->
                  try
                      let content = System.IO.File.ReadAllText(path)

                      match Parser.parseProgram content with
                      | Ok forms ->
                          match Eval.evalSeq forms env with
                          | Ok _ -> Ok Unit
                          | Error e -> Error e
                      | Error e -> Error e
                  with ex ->
                      Error(NomadError.Eval $"Error while including '{path}': {ex.Message}")
              | _ -> Error(arity "include" 1 params_.Length)

          "read_file",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "read_file" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      try
                          Ok(Value.String(System.IO.File.ReadAllText(path)))
                      with ex ->
                          Error(NomadError.Eval $"Error while reading '{path}': {ex.Message}")

          "write_file",
          fun params_ env ->
              match params_ with
              | [| pathExpr; contentExpr |] ->
                  match Eval.getString pathExpr env with
                  | Error e -> Error e
                  | Ok path ->
                      match Eval.getString contentExpr env with
                      | Error e -> Error e
                      | Ok content ->
                          try
                              System.IO.File.WriteAllText(path, content)
                              Ok Unit
                          with ex ->
                              Error(NomadError.Eval $"Couldn't write to '{path}': {ex.Message}")
              | _ -> Error(arity "write_file" 2 params_.Length)

          "remove_file",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "remove_file" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      try
                          if System.IO.File.Exists(path) then
                              System.IO.File.Delete(path)
                              Ok Unit
                          else
                              Error(NomadError.Eval $"Couldn't remove file '{path}': No such file or directory")
                      with ex ->
                          Error(NomadError.Eval $"Couldn't remove file '{path}': {ex.Message}")

          "read_dir",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "read_dir" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      try
                          let names =
                              System.IO.Directory.GetDirectories(path)
                              |> Array.append (System.IO.Directory.GetFiles(path))
                              |> Array.map (fun p -> System.IO.Path.GetFileName(p))
                              |> Array.sort
                              |> Array.map Value.String

                          Ok(Value.List_ names)
                      with ex ->
                          Error(NomadError.Eval $"Couldn't read directory '{path}': {ex.Message}")

          "mkdir",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "mkdir" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      match createDir0755 path with
                      | Ok() -> Ok Unit
                      | Error ex -> Error(NomadError.Eval $"Couldn't create directory '{path}': {ex.Message}")

          "remove_dir",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "remove_dir" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      try
                          System.IO.Directory.Delete(path)
                          Ok Unit
                      with ex ->
                          Error(NomadError.Eval $"Couldn't remove directory: '{path}': {ex.Message}")

          "chdir",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "chdir" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok path ->
                      try
                          System.Environment.CurrentDirectory <- path
                          Ok Unit
                      with ex ->
                          Error(NomadError.Eval $"Error while changing working directory to '{path}': {ex.Message}")

          "cwd",
          fun params_ _env ->
              if params_.Length <> 0 then
                  Error(arity "cwd" 0 params_.Length)
              else
                  try
                      Ok(Value.String(System.Environment.CurrentDirectory))
                  with ex ->
                      Error(NomadError.Eval $"Could not determine working directory: {ex.Message}")

          "get_env",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "get_env" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok var ->
                      match System.Environment.GetEnvironmentVariable(var) with
                      | null -> Error(NomadError.Eval $"Environment variable '{var}' not found")
                      | v -> Ok(Value.String v)

          "get_env_unit",
          fun params_ env ->
              if params_.Length <> 1 then
                  Error(arity "get_env" 1 params_.Length)
              else
                  match Eval.getString params_[0] env with
                  | Error e -> Error e
                  | Ok var ->
                      match System.Environment.GetEnvironmentVariable(var) with
                      | null -> Ok Unit
                      | v -> Ok(Value.String v) ]
