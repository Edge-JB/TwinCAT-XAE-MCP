---
name: Feature request
about: A TE1000 capability you'd like wrapped as a tool/action
title: "[feat] "
labels: enhancement
---

## What capability

Describe the TwinCAT/TE1000 operation you want to automate.

## Proposed surface

Which existing tool would gain the action, or why it needs a new one. Prefer a new `action` on an
existing noun-grouped tool over a new top-level tool, and a `*_batch` form for multi-item work.

## Automation Interface reference

If known, the `ITc…` interface / method or infosys topic (see
[docs/automation-interface.md](../../docs/automation-interface.md)).

## Safety

Does it touch the live target, delete anything, or change licensing? If so it must be
confirmation-gated. It must never write toward the safety project.
