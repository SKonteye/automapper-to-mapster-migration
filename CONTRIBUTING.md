# Contributing

Thanks for wanting to help! This repo is documentation-and-tooling, not a library, so the most valuable contributions are **real-world gotchas** and **edge cases the script doesn't handle** — not feature requests for the script itself.

## What makes a great contribution

### 🥇 Gotchas you hit on your own migration
If you did this migration on your codebase and something bit you that isn't in [the six gotchas](docs/automapper-to-mapster-migration.md#the-six-gotchas), please open an issue using the [Gotcha report template](.github/ISSUE_TEMPLATE/gotcha_report.md). Include:
- The AutoMapper code that worked
- The naive Mapster translation that broke
- What the failure mode was (silent / exception / wrong result)
- The fix

### 🥈 Cheatsheet improvements
Found a cleaner Mapster idiom for something in the [cheatsheet](docs/cheatsheet.md)? Open a PR. Keep entries short — the whole point of the cheatsheet is that it fits on one screen.

### 🥉 Script improvements
The PowerShell codemod is deliberately minimal. It handles the 95% case (using directives + DI). PRs that expand scope (e.g. Roslyn-based profile rewriting) will likely be declined — there are already tools for that. PRs that make the existing script more robust (handling edge cases in file encoding, solution layouts, etc.) are very welcome.

## How to submit changes

1. **Open an issue first** for anything non-trivial. This saves you writing a PR that gets declined.
2. **Fork** the repo and create a topic branch: `git checkout -b gotcha/datetime-kind-unspecified`.
3. **Keep PRs focused** — one gotcha per PR, one script fix per PR.
4. **Update `CHANGELOG.md`** under `[Unreleased]`.
5. **Open a PR** using the [PR template](.github/PULL_REQUEST_TEMPLATE.md).

## Style guide

### Documentation
- American English.
- Sentence case for headings (`## Step 1 — swap the packages`, not `## Step 1 — Swap The Packages`).
- Wrap prose at ~100 columns. Not a hard rule.
- Code examples should be real, minimal, and runnable.

### PowerShell
- PowerShell 5.1-compatible syntax where possible (no ternary `? :`, no null-coalescing `??`). The script is expected to run on locked-down Windows dev boxes.
- Use full cmdlet names, not aliases (`Where-Object`, not `?`).
- Pass `PSScriptAnalyzer` with no warnings — CI enforces this.

### Commit messages
[Conventional Commits](https://www.conventionalcommits.org/) style, lightly enforced:
- `docs: add gotcha for DateTime.Kind drift`
- `fix(script): handle UTF-8 BOM on Windows-authored files`
- `chore: bump workflow actions to v4`

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Be kind. Assume good faith. If something feels off, email the maintainer (contact in `README.md`) rather than escalating in public.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE) of this project.
