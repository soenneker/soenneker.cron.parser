# Cronos issue and pull-request review

Reviewed September 6, 2026 (UTC). Inspected the issue/PR inventory through #101,
then read the relevant reports and the proposed changes in PRs #95, #97, and
#100. Those PRs were open and unmerged at review time. Reports are treated as
reproduction leads; Cronos 0.13.0 is not an oracle for its known defects.

## Implemented

| Source | Decision and verification |
|---|---|
| [PR #95](https://github.com/HangfireIO/Cronos/pull/95), [issue #99](https://github.com/HangfireIO/Cronos/issues/99) | Fix Sunday-origin stepped ranges. `7-1/2` now includes Sunday; `7-3/2` matches `0-3/2`. Exhaustive alias tests cover endpoints 0–6 and steps 1–7. Ordinary wrapping ranges and `7-7` retain their behavior. Removed tests that required agreement with Cronos's defect. |
| [Issue #22](https://github.com/HangfireIO/Cronos/issues/22) | Accept wildcard and stepped-wildcard list members as unions, in any position: `*/15,7` equals `0,7,15,30,45`. No additional stored state or allocation is required. Malformed lists and unsupported compound special-day lists still fail. |
| [Issue #93](https://github.com/HangfireIO/Cronos/issues/93), [PR #97](https://github.com/HangfireIO/Cronos/pull/97) | Upper-bound checks were already safe; added fixed-zone coverage at offsets −14, −5, 0, +5, and +14 hours. The investigation found and fixed a separate lower-bound bug: when conversion clamps a local time before year 1, the first representable local midnight must remain eligible. |
| [Issue #99 / PR #100](https://github.com/HangfireIO/Cronos/pull/100) | Our list parser is already iterative. Added a 100,001-member regression and malformed-tail check. This input is deliberately not passed to Cronos, whose reported stack overflow can terminate the test process. |
| [Issue #91](https://github.com/HangfireIO/Cronos/issues/91) | Added a synthetic time zone with deterministic half-hour gaps and overlaps. Existing real-zone differential tests remain useful, while the new assertions do not depend on changing host time-zone databases. |
| [Issue #70](https://github.com/HangfireIO/Cronos/issues/70), [issue #73](https://github.com/HangfireIO/Cronos/issues/73) | Added explicit empty/whitespace TryParse and fractional-instant regression coverage. These paths were already correct; no production change was needed. |

The Sunday-range, wildcard-list, and lower-bound regressions were run before
the production fixes and failed. All seven new regression tests pass afterward.

## Deliberately deferred or excluded

- Reverse-search fixes in [PR #100](https://github.com/HangfireIO/Cronos/pull/100),
  [PR #101](https://github.com/HangfireIO/Cronos/pull/101), and
  [PR #94](https://github.com/HangfireIO/Cronos/pull/94) do not apply to this
  forward-search API. Reverse search should be a separate feature with its own
  correctness and performance work.
- [Issue #71](https://github.com/HangfireIO/Cronos/issues/71) requests repeating a
  fixed-time occurrence during a fall-back overlap. That conflicts with our
  documented once-only behavior. Interval schedules already repeat correctly.
- Timer interval limits ([issue #42](https://github.com/HangfireIO/Cronos/issues/42)),
  active-period policies ([issue #69](https://github.com/HangfireIO/Cronos/issues/69)),
  and multi-expression job orchestration belong to consumers or schedulers.
- Compound special-day lists such as `15,L` ([issue #52](https://github.com/HangfireIO/Cronos/issues/52))
  need a deliberate representation and grammar design; this pass does not bolt
  them onto the existing singular day modifiers.
- Year fields, alternate calendars, NodaTime integration, and framework-target
  requests are not needed for the current .NET 10 Gregorian scheduling contract.
- Formatting/ToString and iterator validation PRs target APIs we do not expose.
  Our span-based bulk API is synchronous.

Cronos remains confined to tests and benchmarks. None of these changes adds a
production dependency or copies an upstream implementation.
