# Errors

## 2026-04-27 - xUnit assertion API mismatch

- **Context**: Added tests for default user data directory behavior.
- **Error**: `dotnet test` failed because xUnit in this repository does not provide `Assert.DoesNotStartWith`.
- **Cause**: I assumed an assertion helper existed without checking the installed xUnit API surface.
- **Correction**: Use `Assert.False(value.StartsWith(prefix, comparison))` for negative prefix checks.
- **Prevention**: Prefer broadly supported xUnit assertions unless the repository already uses a newer/specific assertion API.
