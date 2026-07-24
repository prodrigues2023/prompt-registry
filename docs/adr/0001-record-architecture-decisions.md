# ADR-0001: Record architecture decisions in ADRs

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

This repository is a registry with a point of view: prompts deserve the discipline of code. Its
value is the reasoning behind each choice — why a version is immutable, how an application
references it, how a change is tested — because those choices become contracts the moment an
application depends on the registry.

Registry decisions lose their reasoning in a way that then breaks integrations. How a prompt
version is identified looks like an implementation detail six months later — until someone changes
it, and every application that referenced a prompt by that identity can no longer resolve it. The
reasoning is what marks the identity scheme as a contract rather than a detail.

## Decision

Every architecturally significant decision is recorded as a numbered ADR in `docs/adr/`, using
Michael Nygard's format: context, decision, consequences.

A decision is architecturally significant if changing it would alter how a prompt is identified,
how an application references it, or what "the prompt changed" means for its version.

ADRs are immutable once accepted. A decision that changes is superseded by a new ADR, and the
original is marked `Superseded by ADR-XXXX`. (The registry applies the same immutability to prompt
versions — see [ADR-0002](./0002-prompt-as-artifact.md) — which is not a coincidence; both are
artifacts whose value depends on being stable references.)

## Consequences

**Positive**

- A reader can tell which parts of the registry are contracts and which are incidental, which is
  the difference between a safe change and a broken integration.
- A team adopting the registry can see what each decision was meant to enable before they build on
  it.
- Disagreement becomes reviewable: an issue can target a specific ADR.

**Negative**

- An ADR costs time, and some decisions will be documented after the fact — losing the
  alternatives, which were the valuable part.
- The evaluation methodology that gates promotion lives in the
  [rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit), not here, so
  the ADR set is not self-contained on the question that matters most — how a prompt change is
  judged. This split is deliberate but it means a reader must follow a link for the full picture.
