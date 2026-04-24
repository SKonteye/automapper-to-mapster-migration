# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — 2026-04-24

### Added
- Initial release.
- `scripts/migrate-automapper-to-mapster.ps1`: PowerShell codemod that rewrites
  `using AutoMapper;` → `using MapsterMapper;` across a .NET solution and
  prints a report of files that still need manual attention.
- `docs/automapper-to-mapster-migration.md`: full step-by-step migration guide,
  including audit greps, package swap, DI rewrite, `Profile` → `IRegister`
  cheatsheet, six silent-regression gotchas, five verify gates, and advanced
  scenarios for `ProjectTo`, `ITypeConverter`, `IValueResolver`, and
  inheritance mappings.
- `docs/cheatsheet.md`: standalone cheatsheet for the `Profile` → `IRegister`
  rewrite.
- `examples/before-after/`: minimal worked example showing an AutoMapper
  `Profile` alongside its Mapster `IRegister` equivalent.
- MIT license, Contributor Covenant code of conduct, issue/PR templates, and
  a GitHub Actions workflow running PSScriptAnalyzer on every push.

[Unreleased]: https://github.com/SKonteye/automapper-to-mapster-migration/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/SKonteye/automapper-to-mapster-migration/releases/tag/v0.1.0
