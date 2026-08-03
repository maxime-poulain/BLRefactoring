# 0020 — Declare every rule this codebase already satisfies

- **Status:** Accepted
- **Date:** 2026-08-03
- **Amends:** [0019](0019-enforce-the-ruleset-this-repository-already-declared.md)

## Context

[ADR 0019](0019-enforce-the-ruleset-this-repository-already-declared.md) turned on the seventy-three
rules `.editorconfig` had been declaring and not enforcing. It closed the gap it set out to close.
It also left three things behind, and they are why this record exists.

**The first is that 0019 no longer describes the repository.** It wrote:

> Two rules are demoted, both in `.editorconfig`, both with the argument written beside them
> […] `IDE0051` […] `CS1591` […]

and, among the alternatives it rejected:

> **Document all nine hundred and eleven public members.** Consistent with how much this repository
> writes down, and a different piece of work entirely.

Both demotions were removed in the change that followed, and the rejected alternative is exactly
what was then done — nine hundred and thirty-eight members documented, `CS1591` deleted from the
config, `IDE0051` promoted back to `warning` with five justified suppressions at the sites. That
change was merged without a record. A repository whose argument is that decisions are written down
reversed two written decisions in silence, and the record that stated them has been wrong ever
since. This one says so; 0019 is left as it was, per the convention in
[`docs/adr/README.md`](README.md).

**The second is that "no third category" was never true.** 0019's principle is:

> Every rule is enforced or demoted with its reason. There is no third category.

It named two rules in the second category and missed two that were already in the third.
`CA1725` and `IDE0055` sat at `suggestion` with nothing above them but their own titles. The second
matters more than its severity suggests: **`IDE0055` is the diagnostic through which every
formatting decision in that file is reported** — Allman braces, the newline settings, `System`
directives sorted first, four-space indentation. While it read `suggestion`, none of them was
checked by anything. The file described a house style and enforced none of it.

**The third is that nobody had asked what the rules outside the list would say.** The forty-nine
`CA` rules and twenty-four `IDE` rules in `.editorconfig` are a curated list, and nothing recorded
where the curation came from. The .NET analyzers ship roughly four hundred and fifty `CA` rules and
a hundred `IDE` ones.

### What the census found

The same instrument as 0019: a temporary workflow, `--no-incremental`, a file logger, warnings
deduplicated by position and rule. It is deleted. Unlike 0019's, this one was made to **enumerate**
rather than count, and that changed three decisions — see *Alternatives considered*.

| Configuration | Warnings | Distinct rules |
|---|---:|---:|
| The solution as it stood | **0** | **0** |
| Every `CA` rule (`AnalysisMode=All`) | 1030 | 33 |
| Every `IDE` rule `.editorconfig` does not name, plus naming | 1166 | 21 |

**The baseline is the answer to "are there any warnings left".** There are none — not one, and not
in the families this ruleset exempts on purpose either. No NuGet audit finding, no MSBuild engine
warning, nothing from the one project held outside the rules. That had never been checked.

**Two rules are two-thirds of the `CA` total.** `CA1707`, identifiers should not contain
underscores, fires five hundred and forty-seven times against test names this repository writes as
sentences on purpose. `CA2007`, `ConfigureAwait`, fires one hundred and forty-four times in an
application that has no synchronization context to return to. Neither is a finding about this code.

**Two rules are ninety-two per cent of the `IDE` total.** `IDE0058` (861) is mostly a test asserting
through a fluent call whose return value *is* the assertion. `IDE0022` and its family (216) ask this
codebase to pick a side between block bodies and expression bodies that it has deliberately not
picked.

**And the interesting number is the one that is not in either table.** Sixty `IDE` rules and three
whole `CA` categories reported **nothing at all**. The tree satisfies them and always had — held
there by habit, by review, by whoever wrote the line, and by nothing that would notice it stopping.

## Decision

**Declare the sixty rules that cost nothing.** A rule this codebase obeys by habit is a rule the
next commit may break in silence. Among them, four make a preference that was already written down
finally real: `dotnet_style_qualification_for_field`/`_property`, `dotnet_style_predefined_type_*`,
`csharp_style_var_*` and `csharp_style_throw_expression` are stated at the top of `.editorconfig`
with a trailing `:suggestion`, which is to say they were opinions no build acted on. `IDE0003`/
`IDE0009`, `IDE0049`, `IDE0007` and `IDE0016` are the diagnostics that report them, and all four
cost nothing. `IDE0130` — a namespace must match its folder — is in this group too, which is the
compiler saying what the architecture tests say, about the one thing they do not cover.

**Promote the two rules that had been demoted without an argument.** `CA1725` cost nothing and had
been the cheapest thing in the ruleset to enforce for as long as it had been declared. `IDE0055`
cost seven.

**Three `CA` categories on, one paid for.** `Security`, `Documentation` and `Interoperability`
report nothing — `AnalysisModeSecurity` is the whole `CA2100`/`CA3xxx`/`CA5xxx` family, in a
codebase holding a JWT issuer, a reverse proxy that attaches tokens server-side and EF Core, and
that it says nothing today is the reason to keep it able to. `Globalization` costs four, all fixed
here, and is kept because `CA1305` has been enforced all along: a ruleset that demands an
`IFormatProvider` on formatting and says nothing about `StringComparison` on comparison is enforcing
half a rule.

**Not `AnalysisMode=All`.** Two thirds of what it produces is two rules this repository contradicts
by design, and adopting it would mean twenty-odd demotions written to buy four hundred rules the
code already satisfies — which the sixty declarations above buy honestly, by name.

**Naming, at last, and tree-wide.** Not one `dotnet_naming_rule` existed anywhere, so `IDE1006` had
nothing to enforce and every identifier in this codebase held by agreement alone — the same state
the whole ruleset was in before 0019, one level down. The rules as written cost nothing, and the
one place the convention genuinely splits is scoped: `members_are_pascal` applies under `src/` only,
because a test method here is prose (`Registering_a_trainer_answers_201_and_a_location`) and the
sentence is the point.

**Four rules adopted and paid for, thirty-two occurrences fixed.** The whole list is in the commit;
two are worth naming here. `CurrentUserService` raised `ApplicationException` — the type the
framework documents as *do not use* — from two property getters, which is the one place a getter
must not throw from. That single defect was `CA2201`, `CA1065` and `IDE0055` all reporting at once,
in a repository that has a record about owning its error vocabulary
([ADR 0015](0015-let-each-aggregate-own-the-errors-it-raises.md)). And `Title.GetEqualityComponents`
folded with `ToLowerInvariant`, where a handful of characters lowercase to the same letter without
uppercasing to the same one — so two distinct titles could compare equal.

**Sixteen rules declined, each with the number that decided it written beside it in
`.editorconfig`.** Declared rather than left unmentioned, so the file records a decision taken
instead of a rule nobody looked at. One of them is a contradiction made visible: `IDE0270` asks for
`x ?? throw …`, a throw expression, which line 42 of the same file forbids.

**Two architecture rules defend this record**, both guarding a failure that is green:

- `EveryDemotedRule_SaysWhyItWasDemoted` — a severity below `warning` carries an argument, not just
  the rule's own title. This is the rule `CA1725` and `IDE0055` needed.
- `NoSetting_CarriesATrailingComment` — EditorConfig recognises a comment only at the start of a
  line. Written after a value, the `#` becomes part of the value, the parser fails, and the setting
  silently reverts to its default. `csharp_prefer_braces` had been in that state since it was
  written, and read correctly only because the default happened to agree with it.

## Consequences

- `.editorconfig` declares **160 diagnostics** where it declared seventy-six, and every one of them
  is enforced or carries the argument for lowering it — now checked by a test rather than asserted
  in a README.
- **The house style is a rule for the first time.** Brace placement, newline placement and import
  order were preferences an editor might apply. A contributor's build now fails on them, which is a
  harsher first experience than a warning nobody read; the count says the whole tree cost seven.
- **The naming convention exists.** It was always there and was never written down, which is why the
  first attempt at these rules produced thirty violations that were all wrong — `private static
  readonly` and `private const` in PascalCase, which is what this codebase does and what C#
  convention says. Written correctly, they cost nothing.
- **Documentation generation is no longer a debt.** `GenerateDocumentationFile` was switched on for
  `IDE0005`, `CS1591` was silenced for it, and the follow-up wrote the nine hundred and thirty-eight
  comments instead. Nothing exempts `CS1591` now, and the OpenAPI document ASP.NET Core publishes
  carries the result.

Against that:

- **Three `CA` categories are on and report nothing, so their cost is unknown until code changes.**
  That is the trade in every one of the sixty free declarations too: they are free against this
  tree, on this day. The alternative is that they stay unenforced, which is the state this record
  exists to end.
- **Sixteen declined rules are sixteen judgements that could be revisited**, and the numbers beside
  them will go stale. They are written as of a measurement, not as a permanent verdict.
- **The floating SDK is still the way this breaks with nobody having pushed.** CI pins `10.0.x` and
  `AnalysisLevel` is explicit, which bounds it; a `global.json` closes it and is still not taken
  here, for the same reason 0019 gave — it is a decision about reproducibility rather than about
  enforcement.

## Alternatives considered

**Count, rather than enumerate.** This is the one that mattered, and 0019's census only counted.
Three decisions reversed the moment the sites were visible. `CA2000`'s five occurrences read like
five resource leaks; three of them hand an `HttpRequestMessage` to `SendAsync` and return the task
without awaiting it, so the mechanical remedy — a `using` — would dispose the request before the
send completes, and the rule would have introduced the bug it exists to prevent. `CA1508`'s four
read like dead code; all four are equality tests asserting at run time what the analyzer proves at
compile time, which is what an equality test is for, so the rule is declared under `src/` instead of
everywhere. And the naming rules' thirty violations read like a codebase with no convention; they
were a codebase with a convention the rules had described wrongly.

**`AnalysisMode=All`, with the noisy rules demoted.** Rejected above. The version of this that
nearly won is `AnalysisModeMaintainability=All`, which measured cheap on the assumption that its
content was `CA1508`. Its actual content here was `CA1515` sixty-three times — make an application's
public types internal, which would need `InternalsVisibleTo` plumbing to keep
`WebApplicationFactory<Program>`, MVC's controller discovery and the shared test kit compiling. A
category switch whose every consequence is turned down reads like enforcement and does nothing.

**Leave the sixty undeclared, since the code already satisfies them.** The argument against is the
whole argument of this repository, and of 0019 before it: the difference between a codebase that
happens to be right and one that cannot stop being right is whether anything fails when it changes.
Sixty rules is also the answer to *why these forty-nine* — a question the curated list could not
answer, and which now has one.

**Extend `members_are_pascal` to the tests as well.** It would rename roughly five hundred and
eighty-six test methods out of the sentences that make a failure legible. Measured — the naming
rules cost nothing in the tests once static and const are matched first, so the only thing scoping
gives up is this rule, deliberately.

**Adopt the collection expressions** (`IDE0028`, `IDE0300`, `IDE0301`, `IDE0305`, twenty-three
occurrences). Mechanical, and a real modernisation. Rejected because adopting a language feature is
a decision about how the code reads and belongs to whoever makes it, not to a ruleset changing the
subject in the middle of enforcing one.
