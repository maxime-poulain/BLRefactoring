# Architecture decision records

One file per decision that would be expensive to reverse, or that a reader would otherwise
reasonably assume was an accident.

The code says *what*, and the comments say *why this line*. What neither can hold is the shape of a
decision: the options that were open, what each would have cost, and why the one that lost was
rejected. Without that, the second reader either takes the design on trust or rediscovers the
argument — and sometimes reverses it, since the rejected option is usually the one that looks
simpler from the outside.

## Conventions

- One record per file, numbered in order: `NNNN-a-sentence-in-the-imperative.md`.
- Numbers are never reused, and a record is never rewritten once merged. A decision that changes
  gets a new record that supersedes the old one, and the old one is marked as such and left in
  place — the reasoning that was true at the time is what makes the change legible.
- Status is one of `Proposed`, `Accepted`, `Superseded by NNNN`.
- Record the alternatives and why they lost. A record without them documents an outcome, not a
  decision, and cannot be revisited.

## Index

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-paginate-on-the-query-side-over-a-total-order.md) | Paginate on the query side, over a total order | Accepted |
