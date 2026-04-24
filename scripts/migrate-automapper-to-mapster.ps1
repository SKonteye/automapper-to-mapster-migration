<#
.SYNOPSIS
    Rewrites C# `using` directives from AutoMapper to Mapster across a solution.

.DESCRIPTION
    Mechanical codemod for migrating a .NET solution from AutoMapper to Mapster.
    It handles call sites and injection points (the easy 95%). It does NOT
    translate Profile bodies (CreateMap / ForMember / ReverseMap) — that is a
    manual step driven by the cheatsheet in
    docs/automapper-to-mapster-migration.md.

    At the end of the run it prints a report listing every file that still
    contains AutoMapper-specific syntax that needs human attention.

.PARAMETER Root
    The directory to scan. Defaults to the current directory.

.PARAMETER DryRun
    If set, no files are written — only the report is printed. Always run
    with -DryRun first to confirm the blast radius before committing to
    the change.

.EXAMPLE
    pwsh ./scripts/migrate-automapper-to-mapster.ps1 -DryRun
    pwsh ./scripts/migrate-automapper-to-mapster.ps1

.NOTES
    See docs/automapper-to-mapster-migration.md for the full migration guide.
#>

[CmdletBinding()]
param(
    [string]$Root = '.',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'
Set-Location $Root

$files = Get-ChildItem -Recurse -Include *.cs -File `
    | Where-Object { $_.FullName -notmatch '[/\\](bin|obj|\.git|TestResults|packages)[/\\]' }

$touched = @()
$needsManual = @()

foreach ($file in $files) {
    $original = Get-Content $file.FullName -Raw
    if ($null -eq $original) { continue }
    $content  = $original

    # 1. Namespace swap: AutoMapper -> MapsterMapper (where IMapper lives)
    $content = $content -replace '(?m)^using AutoMapper;\s*$', 'using MapsterMapper;'

    # 2. Any fully-qualified AutoMapper.IMapper references in code
    $content = $content -replace 'AutoMapper\.IMapper\b', 'MapsterMapper.IMapper'

    # 3. Mock<AutoMapper.IMapper> in tests
    $content = $content -replace 'Mock<AutoMapper\.IMapper>', 'Mock<MapsterMapper.IMapper>'

    # 4. Flag files that still contain AutoMapper-only syntax.
    #    These need the Step 4 manual rewrite from the migration guide.
    if ($content -match ': Profile\b' -or
        $content -match '\bCreateMap<'    -or
        $content -match '\bForMember\('   -or
        $content -match '\bReverseMap\('  -or
        $content -match '\bAddAutoMapper\(') {
        $needsManual += $file.FullName
    }

    if ($content -ne $original) {
        $touched += $file.FullName
        if (-not $DryRun) {
            # Write without BOM so git diffs stay clean
            [System.IO.File]::WriteAllText(
                $file.FullName,
                $content,
                [System.Text.UTF8Encoding]::new($false))
        }
    }
}

Write-Information ''
Write-Information '=== AutoMapper -> Mapster codemod report ==='
Write-Information "Files scanned   : $($files.Count)"
Write-Information "Files rewritten : $($touched.Count)"
Write-Information "Files needing manual rewrite (Profile / CreateMap / DI) : $($needsManual.Count)"

if ($needsManual.Count -gt 0) {
    Write-Information ''
    Write-Information 'Manual-rewrite candidates:'
    $needsManual | ForEach-Object { Write-Information "  $_" }
    Write-Information ''
    Write-Information 'Refer to docs/automapper-to-mapster-migration.md Step 4 for the cheatsheet.'
}

if ($DryRun) {
    Write-Information ''
    Write-Information 'DRY RUN — no files were written. Re-run without -DryRun to apply.'
}
