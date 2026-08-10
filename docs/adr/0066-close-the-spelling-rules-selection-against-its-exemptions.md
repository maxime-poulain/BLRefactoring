# 0066 — Close the spelling rule's selection against its exemptions

- **Status:** Accepted
- **Amends:** [0064](0064-write-this-repository-in-american-english.md)
- **Date:** 2026-08-10

## Context

ADR 0064 said the rule it introduced *"reads every file this repository writes"*. It did not.

It selected files by extension, from a list of eighteen. A file whose extension was not on the list
was not checked, and nothing said so: the rule reported itself green over the files it had chosen to
look at. Five files were governed by nothing —

- **three Dockerfiles**, because `Path.GetExtension` answers an empty string for a file called
  `Dockerfile`;
- **`.gitattributes`**, because `.gitignore`, `.editorconfig` and `.dockerignore` were on the list
  and it was not;
- **`scripts/report-test-failures.py`**, because Python was not a language this repository wrote in
  on the day the list was made.

None of them held a British spelling, which is the least interesting part. Two of the Dockerfiles
were written *after* ADR 0064 was accepted, and their author — knowing the convention and knowing
the rule existed — had to check them by hand, because the rule that was supposed to answer that
question was not reading them. A convention enforced by a test somebody has to remember to
supplement is a convention enforced by nobody.

The shape is the defect. **An allow-list produces a blind spot by construction**: it is a promise
about what is checked and says nothing about what is skipped, so the set of unchecked things grows
silently, one new kind of file at a time. ADR 0064 already named the cost of that shape, about its
dictionary — *"a word this repository has not met yet is not governed"* — and accepted it there for
a reason that does not carry over. A word the list has not met is one word. A **file type** the list
has not met is every word in every file of that type, forever.

## Decision

**The two sets are closed against each other, and a file in neither fails the build.**

- **`EveryFileThisRepositoryHolds_IsEitherReadOrDeclaredUnread`** walks the same tree the spelling
  rule reads and refuses any file that is neither. There is no third outcome and, in particular, no
  silent one.
- **What is read gained the four kinds that were missing**, and gained a second axis with them:
  a file is read when its **extension** is listed, or when its **name** is. `Dockerfile` has no
  extension, and no amount of extending the first list would ever have reached it.
- **What is unread is declared with a reason, one per entry**, in the spirit of
  `EveryDemotedRule_SaysWhyItWasDemoted`: an exemption without an argument is indistinguishable from
  an oversight. There are four. The MIT license, whose words belong to whoever wrote it. A
  developer's `appsettings.Local.json` (ADR 0035) and their `.pfx` (ADR 0065), which belong to a
  machine rather than to a commit — and the second of which is not text at all. And the rolling
  `.log` files a running host writes into the working tree (ADR 0026).
- **The rule's own file stays excluded, and it is the one exemption not in that list.** It cannot be:
  the list would have to name a file whose entire purpose is to hold the words nobody may write.
  ADR 0064 resolved that self-reference and this record does not reopen it.
- **This amends ADR 0064's exemptions, which is why it is a record and not a fix.** That record said
  three things were exempt and only those three. Four are now, the license having joined them — and
  the mechanism that decides is different, which is the part worth writing down.

## Consequences

- **The rule found three of its own four exemptions.** The license was predicted; the private
  overrides, the private key and the log files were not — the log files least of all, because they
  are written by *running the application*, so they exist on a machine that has run a host and on no
  other. Every one of them was reported by the rule on its first run under the new selection. That is
  the argument for the shape, made by the shape itself within minutes of it existing.
- **A new kind of file now costs a decision, in the commit that introduces it.** Adding a `.toml`
  breaks the build until somebody says whether this repository writes it or merely holds it. That is
  the intended cost, and it is small in exactly the place where the old shape was free and wrong.
- **The tree is read, not the index.** The rule walks the working directory, so a file that exists
  only on one machine — a log, a private key, a scratch file — is judged the same as a versioned one.
  For the first two that is what the exemptions are for. For the third it means a developer's stray
  `notes.txt` fails their build, which reads as an inconvenience and is a fair one: this suite has
  never asked what git thinks, and teaching it to would put the definition of *the repository* in a
  second place.
- **`.log` is exempt as a kind, not as a path.** A file this repository ever wrote *as* prose with
  that extension would be skipped. Nothing does, and the alternative — pinning the log directory —
  would break the moment somebody points the sink elsewhere, which is a configuration value.
- **The three Dockerfiles are governed from now on, and were clean when they arrived.** Their author
  checked them by hand and said so, which is what the absence of this rule cost: a paragraph in a
  commit message where a test should have been.

## Alternatives considered

**Add the missing extensions and move on.** Two lines — `.py`, `.gitattributes` — and a name check
for `Dockerfile`. It fixes today exactly and leaves the shape that produced today intact: the next
kind of file arrives, nobody thinks about the list, and the rule goes quiet about it in the same way.
The whole of what was wrong is that being unchecked looked identical to being checked.

**Read every file and exempt by pattern.** Simpler to state — govern everything, subtract a few
globs — and it is nearly what this is. It differs in one place that matters: a file matching no
pattern would be *read* rather than *refused*, so a new binary format would be scanned as text and
its noise searched for words. The failure mode of the two shapes is the difference between a
question and a wrong answer.

**Ask git which files are versioned.** It would end the working-tree question outright: no logs, no
keys, no scratch files, no exemptions for any of them. It also puts a second definition of *this
repository* inside a suite that has only ever had one — `SourceTree`, which walks a directory — and
it needs git to be present and the tree to be a checkout, which is one more thing that can be absent
in a container. Three exemptions with reasons are cheaper than a dependency.

**Leave it, since nothing was misspelled.** True and beside the point. The rule's value is that
nobody has to check, and for five files somebody did.

## Verification

- **Both rules watched failing on what they defend, then restored.** A British spelling put into one
  of the newly-read Dockerfiles was reported at its line — proving the file is read now, which no
  amount of it passing could show — and a file with an unlisted extension was reported as
  unclassified. Neither survived the commit.
- **The new rule was red before any exemption existed**, naming five rolling log files nobody had
  thought about. The exemptions were written from what it found rather than from what was predicted.
- **Clean Release build from deleted `bin/` and `obj/`, zero warnings**, and every suite that runs
  without Docker is green.
- **The two integration suites need Docker and did not run here.**
