# Fast Track Optimization Harness

Workflow for finding, building, and validating performance candidates for the
Fast Track mod against the Aquatic-compatible baseline on this branch.

## The 5-stage loop

1. **Survey** — scan the decompiled game assembly (`harness/decompiled/`) and
   profiling data for hot paths worth optimizing.
2. **Design** — write a candidate file from `harness/candidates/_TEMPLATE.md`
   describing the target, the mechanism of the win, and the predicted impact.
3. **Build** — implement the patch (Harmony prefix/postfix/transpiler), add a
   unit test where the code is testable outside the game, and run the static
   gates (compile, IL verify, thread-safety, save-compat).
4. **Review** — a skeptic pass (or full council) on the candidate file.
   Verdict: ADVANCE, REVISE, or REJECT.
5. **Measure** — A/B the change in-game, capture Player.log excerpts, record
   numbers in the candidate file, and resolve to ACCEPTED or PARKED.

## Model split

- **Opus** does the Stage 4 review (the skeptic/council pass) — it is the one
  place a stronger model materially changes the outcome.
- **Sonnet** does everything else: survey, design, build, and measurement
  write-up.

## Layout

- `harness/candidates/` — one Markdown file per candidate, from `_TEMPLATE.md`.
- `harness/decompiled/` — ilspycmd decompile cache of the game assembly.
  Gitignored; regenerate locally, do not commit.
- `harness/baseline-build.log`, `*.bench.log` — local build/bench output,
  gitignored.

## Design spec

The full design rationale lives in a separate (out-of-tree, not committed) design
doc, `2026-06-30-fasttrack-optimization-harness-design.md`, kept alongside this
repo's parent project. Ask the maintainer if you need it.
