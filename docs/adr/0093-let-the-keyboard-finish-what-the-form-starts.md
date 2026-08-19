# 0093 — Let the keyboard finish what the form starts

- **Status:** Accepted
- **Date:** 2026-08-19

## Context

Nine screens of this front end collect fields and submit them with one button: sign-in,
registration, both halves of password recovery, the profile, the training form, and the three
dialogs. Every one of them could only be submitted by clicking. Pressing Enter after the last field
— which is what a person does — did nothing at all, on the sign-in page as much as in the contact
dialog.

**Why it did nothing, precisely.** `MudForm` renders a real `form` element, and suppresses the
browser's implicit submission on purpose: `SuppressImplicitSubmission` defaults to `true`, which
renders a hidden **disabled** submit button as the form's first child — and a form whose default
button is disabled is a form Enter does not submit, by the HTML specification. The component's own
documentation says this is also what keeps a parent dialog from closing when Enter is pressed. In
place of the suppressed submission it offers one thing, `OnEnterPressed`, and the whole of its
implementation is this:

```csharp
private async Task OnKeyDownAsync(KeyboardEventArgs args)
{
    if (args.Key is "Enter" or "NumpadEnter")
    {
        await OnEnterPressed.InvokeAsync();
    }
}
```

A `keydown` handler on the form element, checking nothing but the key. The callback is a bare
`EventCallback` and `KeyboardEventArgs` names no target, so neither the component nor the handler
can tell which field Enter was pressed in — a single-line username or the fourth paragraph of a
description. The event bubbles up from a textarea like from anything else.

**The constraint that decides the design.** Six fields on four of the nine screens are multi-line —
the training form's description, prerequisites and skills, the profile's bio, the contact dialog's
message, the reason dialog's only field. In a textarea, Enter's job is the paragraph break, and a
paragraph break must never save a form. `OnEnterPressed` cannot honor that distinction, because
nothing tells it where the key was pressed.

**The prerequisite that makes it safe elsewhere.** A `keydown` fires before a non-immediate
binding has flushed, which is the recorded defect of every key-listener recipe: the field being
typed in reaches the handler stale. Every single-line field of this front end already carries
`Immediate="true"`, so the bound values follow each keystroke and are current when Enter arrives.

## Decision

**A form with no multi-line field answers Enter through `OnEnterPressed`; a form with one
deliberately does not.**

- **Five screens wire the callback** — sign-in, registration, forgot-password, reset-password, and
  the erase-account dialog. Every field on them is a single line, so "Enter pressed on any child
  input" and "the visitor finished the form" are the same event. The callback names **the same
  method the button's `OnClick` names** — `OnEnterPressed="HandleLogin"` beside
  `OnClick="HandleLogin"` — so the keyboard and the mouse are two gestures into one handler, and
  the handler still validates first, exactly as it always did.
- **Four screens stay without it** — the training form, the profile, the contact dialog, the
  reason dialog. On them `MudForm`'s default suppression keeps Enter inert everywhere, which is
  the correct half of the behavior: a paragraph break inserts a line and saves nothing, and the
  button remains the one way to submit.
- **Nothing is written around the component.** No `form` element of the page's own, no button
  declaring `ButtonType.Submit`: the first would nest a form inside the form `MudForm` already
  renders, and the second would re-enable the implicit submission the component suppresses and
  trigger a real submission nothing handles.
- **Every dialog states whether Escape closes it.** `CloseOnEscapeKey` is nullable on the provider
  and unset by default, so a dialog that says nothing inherits whatever the library decided this
  release. The four `DialogOptions` this front end builds all say `true`.

Two rules defend it. `EveryFormAScreenSubmits_AnswersToTheKeyboard` holds the dichotomy — a screen
with no multi-line field declares `OnEnterPressed`, one with a multi-line field must not — plus the
guards: the callback names a method some button clicks, no screen writes its own `form` element,
and no button declares `ButtonType.Submit`. `EveryDialog_SaysWhetherEscapeCloses` holds the second
half.

## Consequences

- **Enter finishes the five forms a visitor types straight through** — the sign-in above all,
  where the reflex is strongest. On the four screens with a textarea the button remains the only
  submit, and this record trades that away explicitly rather than leaving it as an oversight: the
  alternative was a paragraph break that saves.
- **Two gestures, one handler, held by rule.** The drift this shape invites — someone changes the
  button's handler and not the callback's — is exactly what the rule's same-name guard refuses.
- **The busy state stopped being cosmetic.** A key held down repeats where a button is pressed
  once, and a second submission means two accounts racing for one username, a reset token spent
  twice, or an edit stating a version the first submission has already superseded. What refuses it
  is the `disabled` the spinner announces; six facts hold the label and the attribute together,
  one per screen that submits to the API.
- **An Enter that confirms an IME composition submits.** The component checks only the key's name,
  not `IsComposing`. That is the library's trade, and it ships with the callback.
- **A future screen fails the build until it takes a side.** A new `MudForm` with single-line
  fields must declare the callback; one that gains a textarea must drop it. Either way the
  decision is made in the markup, not discovered by a visitor.

## Alternatives considered

**Wrapping the fields and the button in a `form` element of the page's own — this record's first
draft, withdrawn.** The draft wrapped `MudForm` and a `ButtonType.Submit` button in a hand-written
`form` carrying `@onsubmit`, on the belief that `MudForm` renders a `div`. It renders a `form`,
and that undoes the whole design: built through the DOM, the forms nest, the fields belong to the
nearest ancestor — the component's inner form — and its hidden disabled default button blocks
implicit submission, so Enter reached nothing in a real browser. The unit suite stayed green the
entire time, because it dispatched the submit event on the outer element directly. The draft was
withdrawn when the component's source was read instead of probed.

**`IKeyInterceptor`, the community recipe.** A key listener with the same two defects the
component's own callback has — no target, and a race with non-immediate binding that its own
discussion resolves with *use KeyUp instead* — plus a JavaScript service this page otherwise never
needs.

**`OnEnterPressed` on all nine screens.** Uniform, and wrong on four of them: Enter in a
description would insert the paragraph break *and* submit the half-edited form.

**Migrating the nine forms to `EditForm`.** What MudBlazor's own documentation recommends for real
submit semantics. It replaces the validation model on nine screens — `MudForm`'s per-field
validation for data annotations on a bound model — to gain a gesture one attribute provides where
it is safe at all. Out of proportion, and unavailable anyway on the four screens where the gesture
itself is the problem.

**`SuppressImplicitSubmission="false"` with a submit button associated to the component's form.**
This would ride the browser's real algorithm — textareas exempt by specification — but it needs a
submit listener splatted onto the component's form element through unmatched attributes and a
button placed or associated against the component's own guidance. It glues the page to the
component's rendering internals; the callback is the supported surface for the same gesture.

**Enter in the catalog's search field.** Attempted and abandoned: that field has no button and no
form — it searches as you type. Its debounce updates `Text` only after the interval,
`OnInternalInputChanged` is a bare `EventCallback` carrying nothing, and reading `Text` off the
component is refused by MudBlazor's own MUD0012 analyzer, which is a build error here. What
remained was replacing the component's debounce with one the page owns — a change to the
most-tested page in the application to save four hundred milliseconds.

## Verification

The rule was seen red in both directions before the markup settled. Against the withdrawn draft it
named every screen:

> `Login.razor` writes a form element of its own around or beside a MudForm. MudForm is a form
> already, the fields of nested forms belong to the inner one, and its hidden disabled submit
> button makes Enter reach nothing — silently

and with the callback planted on a screen that has a textarea:

> `CreateTraining.razor` has a multi-line field and a MudForm declaring OnEnterPressed. The
> callback is raised by a form-level keydown that cannot tell which field Enter was pressed in, so
> it would submit from a textarea

The suites then prove the gesture where it exists: the five screens' submit-flow facts dispatch
Enter on the form element — where `MudForm` listens — and assert the same outcomes clicking always
produced; the erase dialog holds both gestures side by side; the four multi-line screens' facts
click, because clicking is all their forms answer.

**A browser pass covers what bUnit cannot**: bUnit dispatches the `keydown` on the form element,
while a browser bubbles it up from the focused field, and no test here can press Escape at all. So:
Enter in the sign-in page's password field signs in; Enter in a training's description inserts a
line and saves nothing; Escape closes the contact dialog.
