# 0079 — Build the development catalog with the domain

- **Status:** Accepted
- **Amends:** [0049](0049-measure-duplication-where-repetition-is-a-defect.md), [0065](0065-ship-every-host-as-an-image-and-build-them-in-the-pipeline.md)
- **Date:** 2026-08-14

## Context

A clone of this repository starts with one account. `AdministratorSeedExtensions` makes `admin` and
grants it the role (ADR 0051); nothing else exists. Every screen built over the last twenty records
is therefore correct and unexercised: the catalog pages one row, the search matches whatever was
just typed into it, the facets offer a single subject, a trainer's profile holds nothing, the
administration lists one line, and the newest-first order (ADR 0071) sorts a set of one. None of
that is a defect a test would catch, because each of those surfaces has tests that construct
exactly the data they assert on. What is missing is the state a person needs to *look* at the
product and see whether it works.

Two subjects had also grown apart from the code. `Topic` was a closed set of six broad categories —
Programming, Design, Marketing, Business, Personal Development, Leadership — chosen when the domain
was a sketch. They describe a training platform in general and this one not at all, and a corpus
written against them would file every backend course under `Programming` and leave the facets a
list of one useful entry. And nothing held the browser's copies of that set against the domain's:
`Blazor.Client.Models.Topic` lists the names the create-training picker offers, `CatalogTopics`
translates a name into a shelf hue, `app.css` declares the hue — three hand-written lists, none of
which references the domain, because the browser sees the generated client and nothing else.

## Decision

**The closed set of subjects grows from six to sixteen**, and every addition is a *subject* rather
than a product: Software Architecture, Cloud Computing, DevOps, Databases, Security, Web
Development, Data and Analytics, Testing and Quality, Project Management, Agile Practices. A closed
set that admits Kubernetes has to admit the next runtime, and a taxonomy that grows with the market
stops being a taxonomy — Kubernetes is a *title*, Cloud Computing is a shelf.

**The browser's copies of that set are held against it by a rule.** Each of the three fails
silently in its own way: a subject missing from the picker is one nobody can ever file a training
under, however willingly the API would accept it; a subject missing from the hue dictionary wears
the neutral tone, which is the right answer for a name from outside and the wrong one for a name
the domain admits; a property the dictionary names and the sheet does not declare paints nothing at
all, because a browser resolves an undefined custom property to nothing rather than complaining.
All three are checked in both directions.

**A written corpus fills a development database, and the domain builds it.** Twelve areas of
expertise, ninety-six trainings written to be read, name pools, and biographies composed from an
opening and a closing so that a hundred and seventy profiles do not repeat visibly. Every trainer
goes through `Trainer.Create`, every training through `Training.CreateAsync` with the same three
ports the application layer resolves, every portrait through `TrainerPhoto.Vet`, the sanitizer and
the object store in that order (ADR 0063), and every state change through the aggregate's own
method. One `TransactionScope` per trainer (ADR 0040), one scope per trainer so the change tracker
holds one person rather than a hundred and seventy.

**Exactly the number asked for, and between one and five each.** Sizes are drawn against weights
while more than five trainings remain to be placed, and the last trainer takes what is left — which
is therefore between one and five. The total is exact by construction rather than by trimming.
Five is deliberately below the domain's own limit of ten (`Training.MaximumPerTrainer`), so a
seeded trainer can still create a training by hand; a rule holds that inequality rather than
restating either number.

**Deterministic, and dated.** Everything derives from one `Random` seeded with a constant, so two
runs produce the same people, the same trainings and the same states — which is what lets a
screenshot keep matching the database and a test assert on a title. The creation instants are
spread over eighteen months backwards from the moment the seeder runs, with noise bounded at half
the spacing so the order can never be disturbed by it.

**Three gates, and the third is off by default.** `OpenApiDocumentGeneration.IsInProgress()`,
because emitting the document loads a host for real; `IHostEnvironment.IsDevelopment()`; and
`DevelopmentData:Enabled`, absent meaning off. Five hundred trainings is not something to discover
on a machine that was only supposed to start. `DevelopmentData:Trainings` tunes the size, so an
integration test can ask for forty.

**Idempotent, and destructive of nothing.** The dataset names its accounts deterministically —
`firstname.lastname`, folded to the characters Identity admits — so a trainer whose username
already exists is skipped whole. A completed run is a no-op, an interrupted one resumes, and
nothing already in the database is ever deleted or overwritten.

**The password is configuration, and `toto` is what the Development file ships.** It sits in
`DevelopmentData:Password` beside `Administrator:Password`, which the other seeder already read from
there — two seeders in one repository disagreeing about whether a credential is configuration was
the inconsistency, and a constant in an assembly was the wrong half. A host asked for the catalog
with no password configured reports the missing key and seeds nobody, rather than inventing one.
The configured policy asks for four characters and neither a digit nor an uppercase letter nor a
symbol (ADR 0051), so the word passes
as written, through the production hasher. It is a fixture rather than a secret, on the same
argument the administrator's seeder makes: a well-known credential committed in plain sight, whose
whole safety is the environment gate above it.

**It lives in the infrastructure, not beside the administrator's seeder.** `AdministratorSeedExtensions`
sits in `Shared.Api` because it touches Identity alone; this one names aggregates and repositories,
and `TheSharedApiLayer_NamesNoDomainType` refuses that — correctly, since seeding a database is not
an HTTP concern. The corpus and the placement live in `Shared.Infrastructure/Development/`, the
host-facing extension method in `Shared.Infrastructure/Extensions/`, which is also what keeps it
inside `OnlyTheCompositionRoot_RegistersServices`.

## Consequences

**One write happens outside an aggregate, and it is the audit stamp.** `CreatedOn` is set by an
interceptor on insert and explicitly protected from update, so there is no path through the domain
or through change tracking that places a training in the past. The seeder therefore issues a bulk
update per training, which goes to the database directly and never reaches the interceptor. Without
it five hundred trainings share one second and the newest-first page is decided entirely by the
identifier tiebreaker. It runs before `app.Run()` starts the outbox worker, so the search index
copies the corrected value rather than the stamped one (ADR 0059).

**Around a hundred and seventy welcome emails are really sent.** Each creation commits its facts to
the outbox, and the worker delivers them once the host is running — one SMTP connection each. With
Mailpit in the compose file that is where they land; without an SMTP server they become poison
messages and light the readiness probe, which is ADR 0033 working rather than failing.

**The corpus is checked without a database.** Every title, description, prerequisite, acquired
skill, biography, name, address and topic name goes through the factory that owns its rule in a
unit test, and every property the seeder depends on — the exact count, the bounds, the uniqueness,
the determinism, the strictly increasing dates, the mutually exclusive states — is asserted on the
placed dataset. A corpus entry the domain would refuse is a red test rather than a seeder that
stops halfway through a developer's database.

**The generated portraits are drawn, not photographed.** No stock image can be committed here
without somebody owning its license, and a catalog of initials shows nothing about what the product
looks like with faces in it. They are abstract compositions in one hue per person, drawn with the
imaging library in the one project allowed to hold it (ADR 0063), and they carry no text because
the dependency-free Linux native assets ship no fontconfig — a portrait that renders on a
developer's machine and comes out blank in a container would be worse than one that never carried
letters. One trainer in five publishes none, so the initials fallback (ADR 0074) is visible on a
page of real results.

**Adding a subject now costs four edits and a rule says so.** The domain's set, the picker, the hue
dictionary and the sheet. It used to cost one, and check nothing.

**The seeder is proven without Docker, and one thing it does is deliberately not proven.**
`DevelopmentSeedExtensionsTests` runs the whole of it against SQLite in memory — the three gates
each refusing on their own, the exact count, the bounds per trainer, the committed password through
the real hasher, the backdating that the audit interceptor would otherwise overwrite, the three
minority states, the portraits reaching both the store and the aggregate, and a second run creating
nothing. The exception is the transaction: the SQLite provider implements no ambient transactions at
all, so that world runs the statements without the `TransactionScope` they run inside on SQL Server.
The suite says so where it suppresses the warning, and asserts nothing about atomicity — the
interrupted-run behavior above belongs to the integration suites, whose database implements it.

**The corpus is exempt from the duplication measure, and that widens ADR 0049 by one category.**
Ninety-six blueprints across twelve areas are ninety-six records of the same shape carrying
different prose, and a detector that anonymizes literals reads that as one block repeated a hundred
times: measured, this single file put the new-code duplication at twenty percent, and every other
file in the change contributed nothing. No arrangement inside C# helps — flattening the twelve areas
leaves the ninety-six identical `new(...)` shapes that produced the figure. So it joins the two
hosts under `sonar.cpd.exclusions`, which ADR 0049 had deliberately closed to anything else.

The widening is one category and not a hole. Corpora are admitted by name through a registry in
`TheDuplicationMeasure_ExcludesTheHostsWrittenTwiceAndNothingElse`, each entry carrying the argument
it was admitted on, in the mold ADR 0066 sets: nothing is exempt by being forgotten. A path in the
workflow that the registry does not name still fails, and so does a registry entry naming a file
that no longer exists — an exemption that outlives what it described is indistinguishable from a
wildcard. Both halves were seen red before being trusted.

**The constant seed is refused by two analyzers, and answering both takes two mechanisms.**
`.editorconfig` demotes CA5394 over the development folder, which settles the .NET analyzer the
normal build runs. It settles nothing for SonarQube, which reads a quality profile rather than this
repository's severities and raises S2245 on the same line — so `DevelopmentDataset.Build` carries a
`[SuppressMessage]` with the argument written out, in the idiom four identifier constructors here
already use. Both say the same thing: the seed is constant because determinism is the decision, and
a cryptographic generator would make that decision impossible to keep. Neither is a finding being
silenced; it is the case `CLAUDE.md` describes, where this repository's ruleset and somebody else's
quality profile disagree and the ruleset wins.

**That demotion also revealed that the images had never read `.editorconfig` at all.** It was the
first line in this repository's ruleset that *loosens* rather than tightens — and the first that the container build could not do without, because the three Dockerfiles
copied `Directory.Build.props` and `Directory.Packages.props` from the root and never `.editorconfig`.
Every image had therefore been compiling the same source under the analyzers' own severities, which
is invisible while a file only promotes rules. The copy is fixed and a rule now derives what the root
carries rather than listing it, so the next `Directory.Build.targets` is covered without anyone
remembering (ADR 0065).

## Alternatives considered

**Insert the rows in SQL.** Faster to write and faster to run, and it would delete the only thing
that makes this dataset worth having: proof that the domain accepts the data. A seeder that writes
rows directly produces data the product could not have produced, which passes every screen and
hides exactly the defects it exists to reveal.

**Drive it through HTTP.** It would exercise the boundary too, and it would require signing in
under a hundred and seventy identities, running against a listening host, and turning the seeder
into a client of the API it ships with. The transaction per trainer would also be gone.

**Leave `CreatedOn` alone.** Rejected above: without the backdating the one sort a visitor actually
uses is untestable by hand.

**Reuse the test builders.** They are in `tests/`, and the reference runs from `tests/` to `src/`.
Nothing in `src/` can reach them, and inverting that to seed a database would be a worse trade than
writing the corpus.
