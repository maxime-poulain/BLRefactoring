# 0057 — The trainer's own surface says where they stand

- **Status:** Accepted
- **Amends:** [0053](0053-a-suspended-trainer-reads-and-does-not-write.md)
- **Date:** 2026-08-08

**What this changes in 0053.** One bullet of that record's *Verification* section asks for the
write controls to be *"absent rather than present-and-failing"*. They are present and disabled
instead, and the second half of this record argues why. Nothing else in 0053 moves: its decision
table, its boundary refusal and its amendment of ADR 0050 all stand, and this record is built in the
same commit that accepts it.

## Context

Two states in this product are reasoned states: a trainer is `Suspended` **with a reason**
(ADR 0050, ADR 0052), and a training is `Withheld` **with a reason** (ADR 0052). The domain states
the pairing in both directions, and `EveryReasonedState_IsWrittenWithItsReason` holds it there.

Neither reason reaches the person the state is about. `TrainerHttpResponse` publishes no standing at
all; `TrainingHttpResponse` publishes the word `Withheld` and not the reason for it. Both omissions
were deliberate and both were recorded as costs rather than as designs:

> Because the withholding reason lives here and not on `TrainingHttpResponse`, a trainer whose
> training was withheld still cannot read why through their own listing — the consequence ADR 0052
> named and this record leaves open. **It closes when the trainer's own surface learns to show it.**

The only channel left was the email of ADR 0056, and an outbox is not a store: ADR 0033 sweeps
delivered messages on a retention period, so a fact that has been delivered and swept cannot answer
*why is my training unavailable*. A product whose account of its own decision expires is a product
that sanctions people in secret.

ADR 0053 closes the writes. This record is what makes that closure legible from the inside.

## Decision

**A trainer's own surface reports every state it can be in, with the reason for it — and shows the
controls a state forbids, disabled and explained, rather than removing them.**

- **`TrainerHttpResponse` carries `Status` and `SuspensionReason`; `TrainingHttpResponse` carries
  `WithholdingReason`.** Both are scoped to the caller: `/Trainer/me` answers the trainer it
  describes, and a training answers its owner. **This is not ADR 0055's separation weakening.** That
  record kept the administration's rows off the trainer's contract because the two have different
  audiences — every trainer's name and address served to anybody was the read it withdrew. Nothing
  here reads another person's row, and neither response gains a search, a page or a second subject.
  Separation by audience never meant that the subject of a decision may not read it.
- **The reason is the administrator's own words, published unedited**, exactly as the notice carries
  them. Two texts describing one sanction is how a product ends up arguing with itself.
- **The write controls stay, disabled, with the reason above them.** A control that is gone teaches
  nothing: it is indistinguishable from a defect, from a permission the trainer never had, and from
  a product that simply does not do that. A control that is present and greyed out says *this is
  yours, and it is suspended* — which is the same argument ADR 0053 makes when it refuses to block
  the sign-in, that a product which sanctions somebody should be able to tell them so itself.
  Removing them would also put the explanation only at the top of the page, and never on the thing
  the trainer actually reached for.
- **Disabling is courtesy and never the enforcement.** `ActiveTrainerPolicy` refuses the request
  whatever the browser renders, and the interface is expected never to meet that `403` in normal
  use. When it does — a suspension decided mid-session — the front end re-reads the standing and
  the banner appears, which is why a bodiless `403` is sufficient here: the answer to *why* is one
  read away, on a surface the sanction deliberately leaves open.
- **The fields of a form stay editable while its submit button does not.** Greying out the content
  itself would make the trainer's own text unreadable, and a suspended trainer keeps every read.

## Consequences

- **ADR 0052's open consequence is discharged, and ADR 0055's with it.** Neither is amended: both
  predicted this and said what would close them. A record that fulfils a prediction is not a record
  that contradicts one, and declaring an amendment would send a reader looking for a disagreement
  that never happened.
- **Every page of the trainer's space now depends on one read.** `/profile` already made it; the
  catalogue and the editor did not. A scoped source makes it once and shares it.
- **`TrainingHttpResponse` grows a member that is null on almost every row.** The price of saying
  the third state out loud, and it is smaller than the alternative — a listing that renders a
  withheld training as merely withdrawn, and offers its owner a publish button that cannot work.
- **The banner shows text somebody typed into a dialog.** The 500-character bound on a reason stops
  being only a validation rule and becomes a layout constraint.
- **A trainer with no standing to report — an account that is nobody's trainer — reads nothing
  here.** The administration surface is not this surface, and an administrator has no standing.

## Alternatives considered

**Keep ADR 0053's clause: remove the controls.** The reading this record amends. Rejected on the
grounds above, and on one mechanical ground the record could not have weighed: hiding a control is
a second, unchecked implementation of an authorization rule. This front end has made that mistake
once and recorded it — a listing that fetched every training and hid the buttons on other people's
had already handed the data to the browser.

**Carry the reason in the email alone.** Already the state of the world, and the reason this record
exists: the outbox is swept (ADR 0033), so the account of the decision expires while the decision
does not.

**A third response type for the trainer's own standing.** A fifth shape for two fields, and it would
put the trainer's own state behind a second read on a page that already reads them.

**A `standing` claim in the token.** The cheapest read, and wrong: the token is minted once at
sign-in, there is no refresh token, so a suspension decided while the trainer is signed in would
leave a claim saying `Active` until they signed out — precisely the window in which the sanction has
to hold.

## Verification

- `EveryStateATrainerCanRead_CarriesItsReason` — a published response that names a state names the
  reason for it, and one that names a reason names the state it belongs to. Both directions, so
  neither a mute state nor an orphan reason survives.
- Shared facts in `tests/TrainingHub.Api.TestKit/`, so both hosts answer them: a suspended trainer
  reads their standing and its reason at `/Trainer/me`, and a withheld training tells its owner why.
- `bUnit` facts that each write control is **present and disabled** while the suspension lasts —
  asserted together, because presence alone is what this record adds to 0053.
