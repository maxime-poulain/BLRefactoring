# 0044 — Let the domain speak entirely in its own terms

- **Status:** Accepted
- **Date:** 2026-08-06

## Context

Three records already say what the domain's language should be. ADR 0015 gives each aggregate the
error codes it raises, prefixed with its own name, and leaves the kernel's codes to the kernel.
ADR 0009 flattens typed identifiers so that a `TrainerId` cannot be passed where a `TrainingId` is
expected. ADR 0032 decides how a value object reaches its table. The rules that defend them are
real, and each one stops just short of a case the model actually contains:

- **`Name.Create` raises `ErrorCodes.Unspecified`** — a kernel code — for two refusals that plainly
  belong to `Trainer`. It is the only place under `src/` that does so. `ErrorVocabularyRules` has
  five rules and none of them see it: they judge what a code's *value* looks like and where it is
  declared, never who raises it. A caller who sends a one-letter firstname gets a code that means
  "something went wrong", from a model whose whole argument is that failures are named.
- **`TrainerPhoto.PhotoId` is a bare `Guid`**, in the aggregate of a repository that made typed
  identifiers a recorded decision. It is also the identifier the object key is built from —
  `trainers/{trainerId}/{photoId}` — so the one place two identifiers sit side by side is the one
  place neither is typed.
- **Trimming is decided per value object, by nobody.** `TrainingTitle`, `TrainingDescription`,
  `TrainingPrerequisites`, `AcquiredSkills` and `TrainerPhoto` trim. `Bio` and `Name` do not. There
  is no record and no comment; the difference is when each was written.
- **`TrainingDto` is mutable** — seven `{ get; set; }` — where `TrainerDto` is `{ get; init; }`.
  A read model that can be changed after it is read is a read model that can lie about what was
  read.
- **`Name`'s message describes half its rule**: *"Firstname must be two characters long at least"*
  for a rule that is two to fifty.

None of these is a new idea. Each is an existing decision that was applied to the code written
after it and not to the code written before, which is the drift the last three lots have been
converting into rules.

## Decision

**The domain speaks entirely in its own terms: it raises its own codes, stores its own types, and
says exactly what it refused.**

- **No domain type raises a kernel code.** `ErrorCodes.Validation` belongs to the FluentValidation
  pipeline (ADR 0016), and the kernel's other codes belong to callers with no aggregate to name.
  Inside the domain there is always an owner. `Name` gains `Trainer.InvalidFirstname` and
  `Trainer.InvalidLastname`, and its messages state the whole rule — two to fifty characters — so
  that the message and the check cannot disagree.
- **A value a domain type stores is a domain type.** `PhotoId` becomes a typed identifier beside
  `TrainerId` and `TrainingId`, converted at the persistence boundary like its siblings. The column
  stays `uniqueidentifier` and no migration is needed: what changes is what the model may confuse
  with what.
- **A text value object trims what it stores.** Leading and trailing whitespace is presentation, not
  content, and a value object that keeps it makes `"Bob "` and `"Bob"` two different trainers. The
  rule is one line in every factory, and a rule holds it. `Email` is the deliberate exception and
  says so in its own remark: an address with spaces around it is refused rather than repaired,
  because the domain does not know whether the caller meant to send it.
- **An application read model is built once.** Every property of a `*Dto` is `init`. What a query
  answered is what the database said, and a caller that wants something else asks another question.

## Consequences

- **Two codes join the published vocabulary**, so the README's error-code table grows to
  twenty-three, and `TheErrorCodeTable_ListsEveryCode` (ADR 0038) proves it did.
  `Trainer.InvalidFirstname` and `Trainer.InvalidLastname` are what a client branches on where it
  previously received `Unspecified` — a strictly better contract, and a breaking one for anybody
  who was branching on the old code, which nothing in this repository was.
- **`TrainerPhoto`'s equality is unchanged** — a typed identifier compares by its value — and so is
  the object key, which reads the identifier's value as before.
- **Trimming changes what two existing refusals mean.** `Bio.Create("   ")` already refused blank
  input; with trimming it refuses for the reason it always claimed. `Name.Create(" Bo ")` now
  succeeds where it failed, storing `"Bo"`, which is the behaviour every other text value object
  has had since it was written.
- **Four rules, and they are cheap because the population is already there.** Each reads the
  assemblies the suite already loads.

## Alternatives considered

**Fix the five defects without a record.** They are conformance to ADR 0009, 0015 and 0032, not new
decisions, and a fix needs no record. Rejected on the trimming alone: nothing anywhere said whether
a value object trims, so choosing is a decision and ADR 0013 wants it written. Once one of the five
needs a record, splitting the other four across three older records' rules — and leaving a reader to
assemble the argument — costs more than one record that states the whole position.

**Give `Name` one code rather than two.** `Trainer.InvalidName` would be shorter. Rejected because
`Name.Create` deliberately reports both fields at once — its own remark says *"a caller sending
three bad fields learns about all three at once"* — and one code for two fields throws that away at
the moment the caller most needs it.

**Trim at the boundary instead.** A model binder or a converter could trim every incoming string.
Rejected: it would make the domain's guarantee depend on which caller reached it, and the domain's
own tests would still be able to construct a value object with untrimmed content.

## Verification

Four rules, each red first on the case above:
`NoDomainType_RaisesAKernelCode` on `Name.cs`; `NoValueObject_ExposesABareGuid` on
`TrainerPhoto.PhotoId`; `EveryTextValueObject_TrimsWhatItStores` on `Bio` and `Name`;
`EveryApplicationReadModel_IsBuiltOnce` on `TrainingDto`'s seven properties. Each was then broken on
purpose and watched to fail. The behavioural half is the domain suite, where the two new codes and
the trimming are asserted directly.
