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
