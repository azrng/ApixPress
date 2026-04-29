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
