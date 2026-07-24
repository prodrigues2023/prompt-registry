# ADR-0004: Regression testing a prompt change

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

This is the decision that makes the whole "prompts are code" claim credible or hollow. Code gets
tests; if a prompt cannot be tested, the claim collapses and the registry is just a fancy string
store. But a prompt drives a non-deterministic component — the same version can produce different
outputs on the same input — so the unit-test model (assert exact output) does not apply.

The temptation is to conclude prompts cannot be tested and to promote on vibes: someone tries the
new version on a few inputs, it looks fine, it ships. That is exactly the untested change the
registry exists to prevent, dressed up as validation. The other temptation is to demand exact-match
tests, which fail on the first harmless rewording and get disabled within a week.

The real question is what a *useful* prompt test asserts, given non-determinism.

Options considered:

1. **No testing — promote on manual spot-check.** The status quo. A wording change that degrades
   one class of inputs ships because the spot-check did not include that class.
2. **Exact-output assertions.** Deterministic and wrong for the domain — flaky on rewording,
   disabled fast, and testing the model's phrasing rather than the prompt's behaviour.
3. **Property and score assertions against a golden set**, comparing the new version to the current
   one. Does the new version still refuse the unanswerable inputs, still produce valid JSON, still
   score at least as well on the golden set as the version it would replace?
4. **Human evaluation only.** Accurate and too slow to gate a promotion; useful as a sample, not as
   the mechanism.

## Decision

**A prompt version is tested against a golden set by asserting properties and comparing scores to
the version it would replace — using the evaluation methodology the
[rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit) owns.**

- **The test is comparative, not absolute.** The question is not "is version 8 good" but "is version
  8 at least as good as version 7 on the golden set". A promotion is an improvement or a no-regression,
  measured against the version it replaces.
- **Assertions are properties, not exact outputs.** Structural properties (valid JSON, required
  fields), behavioural properties (refuses the unanswerable slice, stays grounded), and score
  thresholds (faithfulness, relevance) — never "output equals this string". This is what makes the
  test survive harmless rewording while still catching real regressions.
- **The golden set is the contract.** A prompt has a golden set of representative inputs, including
  the hard and adversarial cases that a spot-check would miss. Testing without that set — or with a
  set that omits the inputs a change breaks — is theatre.
- **The test result is attached to the version** ([ADR-0002](./0002-prompt-as-artifact.md)) and
  gates promotion ([ADR-0003](./0003-promotion.md)). A version cannot be promoted to production
  without a passing comparison against the current production version.
- **Determinism is handled by running each input several times** and comparing distributions, not
  single samples, so a lucky or unlucky single run does not decide a promotion.

## Consequences

**Positive**

- The "prompts are code" claim holds, because a prompt change is genuinely tested — a regression is
  caught before promotion, not discovered in production.
- Comparative testing is robust where absolute testing is not: it survives rewording and still flags
  a version that degrades any class of inputs, which is the failure mode that matters.
- The golden set makes the hard cases first-class. A change that breaks the adversarial slice fails
  the gate, where a spot-check would have missed it entirely.

**Negative**

- **The test is only as good as the golden set, and building a good one is real work.** A golden set
  that omits the input class a change breaks passes a regression it should have caught — the test's
  green is only as trustworthy as the set's coverage, and coverage is expensive to build and
  maintain. This is the same dependency the eval toolkit documents, inherited here.
- Running each input multiple times to handle non-determinism multiplies the cost and latency of a
  test, and a large golden set run several times per promotion is a real bill and a real wait,
  creating pressure to shrink the set or the repetitions — both of which weaken the gate.
- Comparative testing anchors on the current version. If the current production version is itself
  bad, "no worse than current" is a low bar, and a series of no-regression promotions can hold a
  system at mediocre indefinitely without any single change failing. The gate prevents regression,
  not stagnation.
- A property-based test can pass while the output is subtly worse in a way no asserted property
  captures — tone, helpfulness, a nuance the golden set does not score. The test lowers the risk of
  a regression; it does not prove the new version is actually better in every way a human would
  notice, and treating a green test as "definitely improved" is the false confidence to avoid.
