---
name: clean-code-principles-csharp
description: Apply maintainable C# boundaries, readable methods, and behavior-focused design to MVZ-MP code. Adapted from the ScheduleOne workspace skill.
---

# Clean Code Principles for C#

Use for C# architecture, review, and refactoring. Keep the mod entry point focused on lifecycle wiring; isolate Steam transport, game hooks, protocol, and save safety in separate small types. Prefer explicit dependencies and host-owned invariants over global mutable state. Avoid builders and abstractions until they simplify a real use case.

The `rules/` folder and `references/rust-inspired-csharp.md` were copied from `ScheduleOne/.agents/skills/clean-code-principles-csharp` on 2026-09-23. Read only the rule needed for the current decision. S1API examples in those files are examples, not dependencies or design requirements for MyVoiceZoo.

