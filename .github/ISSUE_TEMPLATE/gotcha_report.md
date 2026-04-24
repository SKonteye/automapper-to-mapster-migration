---
name: 🐛 Gotcha report
about: A real-world silent regression or surprising behavior you hit during the migration
title: "[gotcha] "
labels: ["gotcha", "triage"]
assignees: []
---

<!--
Gotchas are gold. The more specific, the more useful.
Please fill in every section — vague gotchas are hard to triage.
-->

## The AutoMapper code that worked

```csharp
// Paste the original, working AutoMapper mapping here.
```

## The Mapster translation that broke

```csharp
// Paste the naive Mapster translation that looked right but wasn't.
```

## What actually went wrong

<!-- Pick one: -->
- [ ] **Silent** — wrong output, no exception, no warning
- [ ] **Runtime exception** — please include the exception type and message
- [ ] **Compile-time error**
- [ ] **Other** — describe below

<details>
<summary>Stack trace or error output (if applicable)</summary>

```
Paste here.
```

</details>

## The fix

```csharp
// Paste the Mapster code that actually works.
```

## Why it happens

<!--
A sentence or two about the underlying reason. Example:
"Mapster's default null-propagation behavior is different from AutoMapper's:
nested member access on a null parent throws NullReferenceException rather than
short-circuiting to null."
-->

## Environment

- **.NET version:** <!-- e.g. .NET 8.0 -->
- **Mapster version:** <!-- e.g. 7.4.0 -->
- **Mapster.DependencyInjection version:**
- **OS:** <!-- Windows / macOS / Linux -->

## Anything else?

<!-- Optional: minimal repro repo, relevant docs, links, etc. -->
