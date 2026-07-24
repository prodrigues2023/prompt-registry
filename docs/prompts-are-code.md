# Why prompts are code

The whole registry rests on one claim: **a prompt is code, and the fact that it is written in
English does not exempt it from the discipline code gets.** This document argues it, because if you
do not accept the claim, the registry looks like overhead, and if you do, it looks obvious.

## The claim

A prompt determines what a program does. Change it and the behaviour changes — often more than
changing the surrounding code would. That is the definition of code: text that determines program
behaviour. The prompt is written in natural language rather than a formal one, but it is executed
(by a model), it has inputs and outputs, and it can have bugs.

Everything the industry learned about managing code therefore applies:

| Code has | A prompt needs | Because without it |
| --- | --- | --- |
| Version control | A named, immutable version | "Which prompt was live when this broke?" has no answer |
| Code review | A reviewable change | A behaviour change ships with no second pair of eyes |
| Tests | A regression test | A wording tweak silently degrades a class of inputs |
| Environments | A promotion path | The validated prompt is edited again before production |
| Rollback | A revert without redeploy | Undoing a bad prompt takes a code deploy while it is live |
| A build artifact | A published artifact | The thing running is not the thing that was tested |

Every row is a lesson the industry already paid for, in code. A prompt embedded as a string
literal opts out of all of them, for the highest-leverage text in the system.

## The objections, answered

**"A prompt is just configuration, not code."** Configuration that changes program behaviour *is*
code — the label does not change what it does. And even configuration, done well, is versioned,
reviewed, and promoted. The string-literal-in-a-function prompt is not even configuration; it is a
hard-coded value with none of config's discipline.

**"You can't test something non-deterministic."** You cannot assert an exact output, true. You can
assert *properties* against a golden set — does the new version still refuse the unanswerable
inputs, still produce valid JSON, still stay grounded — and compare its scores to the current
version's ([ADR-0004](./adr/0004-regression-testing.md)). This is how the
[rag-evaluation-toolkit](https://github.com/prodrigues2023/rag-evaluation-toolkit) already tests
model behaviour; a prompt change is exactly the kind of change it exists to catch.

**"A prompt changes too often for this ceremony."** Frequent change is the argument *for* the
discipline, not against it. Code that changes often is code you most want versioned, tested, and
revertible. A prompt that changes daily and cannot be rolled back is a daily production risk.

**"Prompts should live with the code, so version them with git."** Half right. Versioning the
prompt is exactly the goal. But embedding it in the code means every prompt change is a code
deploy, which is precisely what makes rollback slow and iteration by non-engineers impossible. The
registry keeps the prompt versioned *and* decouples its release from the application's, which git-
in-the-source-file cannot do.

## What this does not claim

The claim is that prompts deserve code's *discipline*, not that a prompt *is* a program in every
respect. Two honest limits:

- **A prompt is not a security control.** No amount of versioning makes a prompt injection-proof;
  "ignore previous instructions" overrides the best-written prompt. Prompt security is the
  [ai-guardrails-toolkit](https://github.com/prodrigues2023/ai-guardrails-toolkit)'s subject, not
  this registry's. Treating a prompt as code does not make it a wall.
- **A prompt's correctness is fuzzy in a way code's often is not.** A unit test can prove a
  function correct; a prompt test can only show a version is *no worse* than the last on a sample.
  The discipline is the same shape; the certainty it delivers is lower, and pretending otherwise
  would be dishonest.

## The consequence

Once you accept that a prompt is code, the registry is not overhead — it is the missing half of the
toolchain. Everything the team already does for the `.py` and `.cs` files around the prompt, the
prompt itself has been quietly exempt from. The registry ends the exemption.
