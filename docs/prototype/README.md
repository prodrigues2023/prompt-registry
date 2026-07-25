# UI prototype

A self-contained, static **design mockup** of the prompt-registry console — the interface the concept
implies, built as a docs-first design artifact, not the Milestone 3 application.

- **File:** [`index.html`](./index.html) — open it in a browser (no build, no dependencies).
- **What it shows:** the release lane of immutable versions (`v5`–`v8`), the environment aliases
  (`production → v7`, `staging → v8`), a **one-operation rollback** (`v8 → v7`, no deploy), the
  version history with its golden-set regression results, and how an application resolves an alias.
- **Design system:** the [shadcn/ui](https://ui.shadcn.com/) token system (zinc base), theme-aware
  (light/dark).
- **Data is synthetic** and illustrative. This is a prototype, not a live product.

It exists to make the repository's thesis legible at a glance: **a prompt is code** — versioned,
immutable, tested, promoted, and rolled back — and rollback is trivial precisely because the previous
version was never overwritten. See [prompts-are-code.md](../prompts-are-code.md) and
[the ADRs](../adr) for the reasoning.
