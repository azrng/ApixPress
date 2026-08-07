# Errors

## 2026-04-27 - xUnit assertion API mismatch

- **Context**: Added tests for default user data directory behavior.
- **Error**: `dotnet test` failed because xUnit in this repository does not provide `Assert.DoesNotStartWith`.
- **Cause**: I assumed an assertion helper existed without checking the installed xUnit API surface.
- **Correction**: Use `Assert.False(value.StartsWith(prefix, comparison))` for negative prefix checks.
- **Prevention**: Prefer broadly supported xUnit assertions unless the repository already uses a newer/specific assertion API.

## 2026-04-28 - Parallel shell PATH variance

- **Context**: Ran parallel PowerShell searches while inspecting project settings code.
- **Error**: One `rg --files ... | Select-String ...` command failed with `rg` not recognized, while another same-round `rg -n ...` command succeeded.
- **Cause**: The effective command environment for parallel shells was inconsistent enough that assuming `rg` availability in every invocation was not reliable.
- **Correction**: Continue the task with PowerShell-native `Select-String` and `Get-ChildItem` for local searches.
- **Prevention**: If `rg` fails in this workspace, switch that turn's remaining local searches to PowerShell-native commands instead of repeatedly retrying `rg`.

## 2026-04-29 - Test assertion tied to fake BaseUrl

- **Context**: Added a regression test for opening an imported HTTP interface and then generating curl code.
- **Error**: The targeted `dotnet test` run failed because the assertion expected `https://pay.demo.local/endpoint-1`, while the test composition supplied a different active BaseUrl.
- **Cause**: The test asserted a fake environment detail that was not part of the behavior under test.
- **Correction**: Assert the stable behavior: dialog stays closed until command execution, curl is generated, method is correct, and the imported endpoint path is included.
- **Prevention**: For ViewModel tests using shared fake services, assert only the behavior being protected unless the fake dependency contract is explicitly part of the scenario.

## 2026-04-29 - PowerShell Set-Content introduced BOM risk

- **Context**: Reopened `TASK.md` by piping `Get-Content` into `Set-Content -Encoding UTF8`.
- **Error**: The diff showed a UTF-8 BOM added to the first line, violating the repository rule to avoid shell rewrite operations on Chinese files.
- **Cause**: I used a broad PowerShell rewrite instead of `apply_patch` for a small markdown status edit.
- **Correction**: Removed the BOM with `apply_patch` and continued using patches for tracked file edits.
- **Prevention**: For tracked source or markdown files, especially files with Chinese content, use `apply_patch` for small edits and avoid `Set-Content`, `Out-File`, redirection, or whole-file rewrites.

## 2026-05-14 - Parallel dotnet build/test file lock

- **Context**: Ran `dotnet build ApixPress.slnx` and a filtered `dotnet test` in parallel.
- **Error**: The test build failed with `AVLN9999` because `src\ApixPress.App\obj\Debug\net10.0\ApixPress.dll` was being used by another process.
- **Cause**: Parallel .NET commands shared the same build intermediate output path.
- **Correction**: Re-run build and test sequentially for this repository.
- **Prevention**: Do not parallelize `dotnet build` and `dotnet test` on the same solution/project unless each command uses isolated output/intermediate paths.

## 2026-08-07 - PowerShell wildcard path passed directly to rg

- **Context**: Reviewed Avalonia command bindings and attempted to search `*.axaml` paths with `rg` in PowerShell.
- **Error**: `rg` reported the wildcard-containing path as an invalid Windows file name.
- **Cause**: PowerShell did not expand the wildcard before it reached `rg`.
- **Correction**: Enumerate repository files with `rg --files` first, then filter the resulting paths or pass literal directories to `rg`.
- **Prevention**: Do not pass a Windows path containing `*` directly as an `rg` path argument in PowerShell.

## 2026-08-07 - No-match rg result interrupted parallel review batch

- **Context**: Included an optional search of the local Avalonia source directory in a parallel review batch.
- **Error**: The source search had no matches and `rg` exited with code 1, which caused the batch to fail.
- **Cause**: `rg` uses exit code 1 for a normal no-match result, but the orchestration treated every non-zero exit as an error.
- **Correction**: Treat optional no-match searches as an empty result, or run them separately from required reads.
- **Prevention**: Add an explicit no-match guard when an `rg` search is exploratory rather than an assertion.

## 2026-08-07 - Full test run exceeded review command timeout

- **Context**: Ran `dotnet test ApixPress.slnx --no-restore` as a read-only baseline for the HTTP invocation review.
- **Error**: The command produced no final result before the 64-second execution timeout and was terminated.
- **Cause**: Not yet determined; the full solution run did not provide sufficient diagnostic output within the command limit.
- **Correction**: Use the focused request-module test filter for the remaining baseline and report the full-suite run as unverified.
- **Prevention**: Run a focused test filter first during a read-only review, then broaden only when its execution time is established.

## 2026-08-07 - Optional documentation path did not exist

- **Context**: Searched for API-history credential documentation under an optional `docs` directory.
- **Error**: The directory did not exist, and `rg` returned a non-zero status that interrupted the batch.
- **Cause**: The optional search scope was not checked before adding it to a required evidence batch.
- **Correction**: Treat absent optional directories as an empty documentation result.
- **Prevention**: Check optional search roots with `Test-Path` before invoking `rg` in a parallel batch.

## 2026-08-07 - Brainstorm visual companion did not start from PowerShell

- **Context**: Started the bundled `start-server.sh` through PowerShell for a local UI prototype.
- **Error**: The command returned exit code 0 but produced no session directory or `.server-info` file.
- **Cause**: The shell-script entry point did not start a visible companion session in this PowerShell invocation.
- **Correction**: Inspect the script's supported invocation and use the compatible shell entry point before creating prototype files.
- **Prevention**: Verify `.server-info` immediately after starting a visual companion server rather than relying on the process exit code.

## 2026-08-07 - Visual companion script inspection was unavailable

- **Context**: Tried to inspect the bundled visual companion launch script after no session directory was created.
- **Error**: The inspection batch returned no script content and a non-zero result, while the available Bash executable was the Windows subsystem launcher.
- **Cause**: The companion shell workflow is not operational in the current PowerShell environment.
- **Correction**: Do not rely on the companion server for this task; validate the Avalonia implementation through its existing UI test and runtime paths.
- **Prevention**: Treat unavailable optional visualization tooling as non-blocking and return to the project-native verification workflow.

## 2026-08-07 - In-app browser runtime initialization conflicted with Node globals

- **Context**: Opened the HTTP upload interaction preview using the mandated in-app browser runtime.
- **Error**: Browser initialization failed with `Cannot redefine property: process`.
- **Cause**: The persistent Node REPL already exposed a non-configurable `process` global incompatible with the browser-client initialization path.
- **Correction**: Reset the Node REPL and retry the guarded browser bootstrap; the same error persisted, so use a local static server and the system browser for this preview.
- **Prevention**: Verify browser runtime initialization before claiming a local browser preview has opened.

## 2026-08-07 - External browser launch was blocked by execution policy

- **Context**: After the in-app browser failed, attempted to launch a local static preview and open it in the system browser at the user's request.
- **Error**: The shell command was rejected by execution policy before it ran.
- **Cause**: This session cannot create the external process required to open a browser window.
- **Correction**: Report the preview limitation instead of claiming the page was opened.
- **Prevention**: Do not promise an externally opened browser preview until the launch command is accepted.
