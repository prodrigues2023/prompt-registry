# Roadmap

Four milestones. Each ships something usable on its own.

Track these as GitHub Milestones.

---

## Milestone 1 — Design (docs only)

**Goal:** a reader is convinced prompts are code and understands the registry's model, before any
implementation.

| Issue | Deliverable |
| --- | --- |
| Write context document | Problem, users, scope, explicit non-goals |
| Prompts-are-code argument | The claim, the objections answered, the honest limits |
| Lifecycle diagram | Version, test, promote, reference |
| Rollback diagram | The one-operation recovery |
| ADR-0001 | Record architecture decisions in ADRs |
| ADR-0002 | A prompt is a versioned, immutable artifact |
| ADR-0003 | Promotion through environments |
| ADR-0004 | Regression testing a prompt change |
| ADR-0005 | Referencing and rollback |

**Exit criteria:** the version → test → promote → rollback lifecycle is shown end to end, and every
capability is traced to immutability.

---

## Milestone 2 — Contracts

**Goal:** the registry's formats are specified, so applications integrate consistently.

| Issue | Deliverable |
| --- | --- |
| Prompt artifact schema | Name, version, template, variables, metadata, test results |
| Reference format | How an application names a prompt and an environment alias |
| ADR-0006 | Prompt artifact and reference format |
| ADR-0007 | Access control and change approval |
| Test contract | How a golden set and its assertions attach to a prompt |

**Exit criteria:** an application and the registry could be built independently against the
contracts and interoperate.

---

## Milestone 3 — Registry

**Goal:** `make up` and a registry runs locally with a sample application resolving prompts from it.

| Issue | Deliverable |
| --- | --- |
| Prompt store | Immutable versions, aliases, metadata |
| Publish and promote | Create a version, run its test, move an alias |
| Client library | Resolve-by-alias, cache, refresh, bundled fallback |
| Sample integration | An app that resolves its prompts from the registry |
| Rollback | Re-point an alias, verified to propagate without a deploy |
| Local environment | One command, local store, stubbed model, no cloud account |

**Exit criteria:** a first-time reader publishes a prompt, promotes it, rolls it back, and sees the
sample app follow — all without redeploying the app.

---

## Milestone 4 — Validation

**Goal:** prove the testing and rollback the registry promises.

| Issue | Deliverable |
| --- | --- |
| Regression harness | Run a new version against a golden set, compare to the current version |
| A caught regression | A prompt change that degrades one slice; assert the gate blocks it |
| Rollback drill | Promote a bad version, roll it back, measure propagation time |
| Fleet-consistency test | Observe the brief cross-instance disagreement during a refresh |
| Fallback test | Registry unreachable at cold start; assert the bundled version serves |

**Exit criteria:** a regression is demonstrably caught before promotion, and a rollback is
demonstrably faster than a redeploy — both shown, not asserted.
