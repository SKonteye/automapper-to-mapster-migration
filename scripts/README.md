# Scripts

## `migrate-automapper-to-mapster.ps1`

PowerShell codemod that rewrites `using AutoMapper;` → `using MapsterMapper;` across a .NET solution and prints a report of files that still need manual attention.

### What it does

1. Walks every `*.cs` file under the current directory (skipping `bin`, `obj`, `.git`, `TestResults`, `packages`).
2. Rewrites `using AutoMapper;` to `using MapsterMapper;` — this is the one change that makes every call site of `_mapper.Map<T>(src)` keep compiling.
3. Flags any file that contains AutoMapper-specific syntax the script **cannot** safely translate: `: Profile`, `CreateMap<`, `ForMember(`, `ReverseMap(`, `AddAutoMapper(`, `ITypeConverter`, `IValueResolver`, `ProjectTo<`, `Include<`.
4. Prints a report at the end listing every file that needs a manual rewrite.

### What it does NOT do

- It does **not** translate `Profile` bodies. That's the job of your brain + the [cheatsheet](../docs/cheatsheet.md).
- It does **not** touch NuGet packages. Swap those first with `dotnet remove` / `dotnet add`.
- It does **not** rewrite DI registration (`AddAutoMapper` → `Scan` + `AddSingleton` + `AddScoped<IMapper, ServiceMapper>`). That's a one-time three-line change; see the [guide](../docs/automapper-to-mapster-migration.md#step-5--rewrite-di-registration).
- It is not Roslyn-based. For the 95% mechanical case, regex is sufficient and has zero setup cost.

### Usage

```powershell
# From your solution root. Dry-run first — always.
pwsh ./scripts/migrate-automapper-to-mapster.ps1 -DryRun

# When the report looks right, apply:
pwsh ./scripts/migrate-automapper-to-mapster.ps1

# Or point at a specific folder:
pwsh ./scripts/migrate-automapper-to-mapster.ps1 -Root ./src
```

### Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-Root` | `string` | `.` | Directory to scan. |
| `-DryRun` | `switch` | off | Print the report without writing any files. Run with this first. |

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. Files were rewritten (or would be, in `-DryRun` mode). |
| non-zero | A file I/O error occurred. Nothing is rolled back; check the report. |

### Requirements

- **PowerShell 5.1+** (Windows PowerShell) or **PowerShell 7+** (cross-platform).
- Read/write access to the target directory.
- Your changes should be committed (or stashed) before running without `-DryRun` — the script does not back up files. That's what Git is for.

### Typical output

```
[dry-run]  rewrote 47 file(s).

Files that need manual attention (AutoMapper-specific syntax detected):
  src/Application/Mappings/UserMappingProfile.cs
  src/Application/Mappings/OrderMappingProfile.cs
  src/Application/Mappings/ProductMappingProfile.cs

3 file(s) need manual rewrite. See docs/cheatsheet.md.
```

### Safety notes

- **Commit first.** The script modifies files in place. Your safety net is your version control.
- **Dry-run first.** Always. The report should match your expectations before you let it touch anything.
- **One solution at a time.** Don't try to run this across a monorepo with unrelated services — run it per-project.
