# Candidate NNNN: <short title>

- Status: DESIGN | BUILT | REVIEWED | QUEUED | ACCEPTED | PARKED
- Target: <Type.Method> — <file:line>
- Risk class: Prefix | Postfix | Transpiler/IL | static/threading
- Gating flag: <FastTrackOptions flag, or "none">
- Collision with Peter's rewrites: yes/no — <note>

## What and why
<the optimization and the mechanism of the win>

## Predicted impact
<qualitative + rough magnitude, tied to a hot-path source>

## Patch
<diff or file/line summary of the change>

## Unit test
<name + path, or "not unit-testable: game-coupled">

## Static gates (Stage 3)
- Compiles: <y/n>
- Unit test: <pass/fail/na>
- IL verify: <pass/fail/na>
- Thread-safety check: <note/na>
- Save-compat: <note/na>

## Review (Stage 4)
<single skeptic verdict, or full council transcript> -> ADVANCE | REVISE | REJECT

## Measurement (Stage 5)
<A/B numbers + raw Player.log capture excerpts>

## Outcome
ACCEPTED (merged) | PARKED (reason)
