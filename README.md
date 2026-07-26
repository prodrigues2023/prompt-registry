# Prompt Registry

> Prompts are code that ships to production — so version them, test them, promote them, and roll
> them back like code. A registry that treats a prompt as a first-class artifact, not a string in
> a source file. Documented first, implemented in the open.

[![Phase](https://img.shields.io/badge/phase-1%20design-blue)](./ROADMAP.md)
[![ADRs](https://img.shields.io/badge/ADRs-5-green)](./docs/adr)
[![License](https://img.shields.io/badge/license-MIT-lightgrey)](./LICENSE)

A prompt is the single most behaviour-changing line in an AI application, and it is usually the
least engineered. It lives as a string literal, edited in place, deployed with the code, with no
version anyone can name, no test that catches a regression, and no way to roll back the one that
broke production without a redeploy.

Meanwhile everyone treats the *code* around the prompt with full rigour — reviewed, versioned,
tested, promoted through environments. The prompt, which changes behaviour more than any of that
code, gets none of it. This repository closes that gap: a prompt is a versioned artifact with an
identity, a test suite, a promotion path, and a rollback — the same discipline the rest of the
application already has.

**Português:** [README.pt-BR.md](./README.pt-BR.md)

---

## What is here today

| Area | Status | Link |
| --- | --- | --- |
| Context & scope | Done | [docs/context.md](./docs/context.md) |
| Lifecycle diagrams | Done | [docs/diagrams](./docs/diagrams) |
| UI prototype (design mockup) | Done | [▶ live demo](https://prodrigues2023.github.io/prompt-registry/prototype/) · [source](./docs/prototype) |
| Architecture Decision Records | 5 published | [docs/adr](./docs/adr) |
| Why prompts are code | Done | [docs/prompts-are-code.md](./docs/prompts-are-code.md) |
| Registry (API, client, consumer) | Done — Phase 3 | [Run it locally](#run-it-locally) · [src](./src) |
| Regression harness (golden set → gate) | Done — Phase 4 | [The gate](#testing-a-prompt-change--the-gate) · [src](./src/PromptRegistry.Harness) |

## The idea

**A prompt in production has a version, and the application references it by name, not by
literal.** The moment a prompt has a stable identity — a name and a version — everything code
already does becomes possible: test that version before it ships, promote it from staging to
production, and roll back to the previous version in seconds without touching the application.
The registry is the thing that gives a prompt that identity.

Everything in [the ADRs](./docs/adr) follows from treating a prompt as a versioned artifact
rather than a string.

## Run it locally

One command brings up the registry and Postgres; migrations apply at startup.

```bash
make up         # build + start the registry on http://localhost:8080
make demo       # publish, test, promote, block a regression, roll back — end to end
make regression # run the golden-set harness: a caught regression blocks a promotion
make drills     # fleet-consistency + fallback drills (self-contained, no server)
make app        # run the example consumer that resolves prompt://checkout-summary@production live
make down       # stop everything and drop the volume
```

`make demo` walks the whole lifecycle against the running registry and prints each step: a
version is published and tested, promoted staging → production, a v2 whose golden-set test
**fails is blocked at the gate** (HTTP 409), and a forced-then-bad promotion is **rolled back in
one operation**. What the pieces are:

| Project | Role |
| --- | --- |
| [`PromptRegistry.Core`](./src/PromptRegistry.Core) | The domain: immutable version, `prompt://name@env` reference, content hash |
| [`PromptRegistry.Api`](./src/PromptRegistry.Api) | Append-only store, promote/rollback as an alias move, resolve endpoint |
| [`PromptRegistry.Client`](./src/PromptRegistry.Client) | Resolve-by-alias with TTL cache, stale-serve, and bundled cold-start fallback |
| [`PromptRegistry.Harness`](./src/PromptRegistry.Harness) | `promptcheck`: runs the golden set, compares to the baseline per slice, writes the gate |
| [`PromptRegistry.Drills`](./src/PromptRegistry.Drills) | `drills`: the self-asserting validation drills — rollback timing, fleet consistency, fallback |
| [`CheckoutSummarizer`](./samples/CheckoutSummarizer) | An example consumer that knows only the reference — never a version literal |

The store is **append-only**: a published version is never mutated, so a rollback is a pointer
move rather than a redeploy, and a test result stays attached to the exact bytes it graded.

## Testing a prompt change — the gate

`make regression` runs the harness that decides a promotion so a human spot-check does not have to.
It embodies [ADR-0004](./docs/adr/0004-regression-testing.md): the test is **comparative** (is the
candidate at least as good as the version it would replace?), scored by **properties** not exact
output, evaluated **per slice** so a change that degrades any one class of inputs fails, and run
several times per case because the model is non-deterministic.

```bash
promptcheck --prompt checkout-summary --candidate 2 \
            --golden samples/golden/checkout-summary.golden.json --gate
```

A "reads better, quietly drops the order number and total" rewrite is exactly the change a
spot-check waves through. The harness catches it:

```
slice              candidate    baseline     delta   verdict
completeness            0.0%      100.0%   -100.0%   REGRESSED
edge                  100.0%      100.0%     +0.0%   ok
typical               100.0%      100.0%     +0.0%   ok
FAIL: Regression on slice(s) completeness ...
```

With `--gate` the verdict is written back to the version, and a failing gate **blocks the
promotion** — the regression never reaches production. The evaluation runs against a **local stub
model** (no cloud account, per the laptop constraint); the methodology, not the stub, is the point.
*How* to score one version better than another is owned by the
[rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit) — this registry
**runs** that judgement as a gate.

## Validation drills

Three drills prove the promises the registry makes about failure and recovery, each self-asserting
so they double as tests — *shown, not asserted*:

| Drill | What it proves | Run |
| --- | --- | --- |
| **Rollback** | A rollback reaches a running consumer within the cache TTL — seconds, no redeploy | `make rollback-drill` (needs `make up`) |
| **Fleet consistency** | Two instances briefly disagree during a refresh, then converge — consistency is eventual, bounded by the TTL | `make fleet-drill` |
| **Fallback** | A registry outage degrades to the bundled version (cold start) or last-known-good (warm), never a hard failure | `make fallback-drill` |

The fleet and fallback drills are self-contained (an in-process fake registry, no server); the
rollback drill measures real propagation against a running registry. Sample output:

```
rollback issued -> consumer served v1 again after 1011 ms
bounded by the 1s consumer cache TTL — no application redeploy, no restart.
```

## Why documented first

The expensive decisions are the ones that become contracts. How a prompt version is identified,
how an application references it, what "the prompt changed" means for the version — each is
load-bearing the moment an application depends on the registry, and changing one afterward breaks
every application that references a prompt. And the hardest question — how do you test a change to
a non-deterministic component — is a methodology decision that shapes the whole registry, and it
is far cheaper to reason through on paper than to retrofit.

## Roadmap

Four phases, tracked as GitHub milestones. See [ROADMAP.md](./ROADMAP.md).

1. **Design** — context, lifecycle diagrams, ADRs, the prompts-are-code argument
2. **Contracts** — the prompt artifact, the reference format, the test contract
3. **Registry** — the store, the promotion flow, a sample integration
4. **Validation** — regression testing against a golden set, rollback drills

## Related

- [enterprise-ai-framework](https://github.com/prodrigues2023/enterprise-ai-framework) — the framework whose planned prompt-versioning decision this registry is the deep dive on
- [rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit) — how a prompt change is judged better or worse, which is what gates a promotion
- [ai-solution-architecture-kit](https://github.com/prodrigues2023/ai-solution-architecture-kit) — where prompt change control fits into model certification and review

## Author

Paulo Roberto Franco Rodrigues — AI Solutions Architect.
Recently designed enterprise AI frameworks and served on an AI architecture committee defining
the engineering standards that bring software discipline to AI delivery.
[LinkedIn](https://linkedin.com/in/paulo-roberto-franco-rodrigues)

## License

MIT — see [LICENSE](./LICENSE).
