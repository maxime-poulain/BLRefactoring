using System.Globalization;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How a form answers the keyboard, and by which gestures (ADR 0093).
/// </summary>
/// <remarks>
/// <c>MudForm</c> is a real <c>form</c> element whose implicit submission the component suppresses
/// with a hidden disabled submit button, and whose <c>OnEnterPressed</c> is raised by a form-level
/// <c>keydown</c> that checks nothing but the key. That shape decides everything here: the gesture
/// is answered by the component's own callback, never by markup around it — a second <c>form</c>
/// element or a submitting button would fight the form the component already renders — and the
/// callback may only exist where no multi-line field does, because a <c>keydown</c> without a
/// target cannot tell a paragraph break from a submission.
/// </remarks>
public sealed partial class FormRules
{
    /// <summary>The front end, which is the only place this repository writes a screen.</summary>
    private const string WebRoot = "src/Web/";

    /// <summary>
    /// Every form a screen submits, answers to the keyboard.
    /// </summary>
    /// <remarks>
    /// One dichotomy and three guards, over one population — every <c>.razor</c> declaring a
    /// <c>MudForm</c>. A screen with no multi-line field declares <c>OnEnterPressed</c>, and one
    /// with a multi-line field must not, because the callback fires on Enter pressed anywhere in
    /// the form — a textarea included — and a paragraph break must never save a form. The guards
    /// hold what the callback names and what the markup must not do: the handler is the same
    /// method some button clicks, so the keyboard and the mouse cannot drift apart; no screen
    /// writes a <c>form</c> element of its own, because <c>MudForm</c> is one already and the
    /// fields of nested forms belong to the inner one — whose hidden disabled button makes Enter
    /// reach nothing; and no button declares <c>ButtonType.Submit</c>, because a submit button
    /// inside the component's form re-enables the implicit submission it suppresses, and the real
    /// submission it triggers is handled by nobody.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0093",
        "a form with no multi-line field answers Enter through MudForm's own OnEnterPressed, one " +
        "with a multi-line field deliberately does not, and the callback names the handler the " +
        "button already clicks")]
    public void EveryFormAScreenSubmits_AnswersToTheKeyboard() =>
        Screens()
            .Selected("screen declaring a MudForm")
            .SelectMany(screen => Complaints(screen.Relative, screen.Text))
            .ShouldHold();

    /// <summary>
    /// Every dialog, says whether Escape closes it.
    /// </summary>
    /// <remarks>
    /// <c>CloseOnEscapeKey</c> is nullable on the provider and unset by default, so a dialog that
    /// says nothing inherits whatever the library decided this release. Escape is a gesture a
    /// visitor expects to be able to trust, and it is handled inside MudBlazor's own keyboard
    /// listener rather than by anything here — which means no test in this repository can press it.
    /// What can be held is that the intent is written down rather than inherited, and that is what
    /// this rule holds (ADR 0093).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0093",
        "every dialog states whether Escape closes it, rather than inheriting an answer no test here can press")]
    public void EveryDialog_SaysWhetherEscapeCloses() =>
        SourceTree.AllFiles
            .Select(path => (Absolute: path, Relative: SourceTree.Relative(path)))
            .Where(file => file.Relative.StartsWith(WebRoot, StringComparison.Ordinal)
                           && (file.Relative.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                               || file.Relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(file => Initializers(SourceTree.ReadText(file.Absolute))
                .Select(initializer => (file.Relative, Initializer: initializer)))
            .Selected("DialogOptions the front end builds")
            .Where(options => !options.Initializer.Contains("CloseOnEscapeKey", StringComparison.Ordinal))
            .Select(options =>
                $"{options.Relative} builds a DialogOptions that says nothing about " +
                "CloseOnEscapeKey. The provider's own is nullable and unset, so the dialog inherits " +
                "whatever the library decided this release — and Escape is the one gesture no test " +
                "in this repository can press (ADR 0093)")
            .ShouldHold();

    /// <summary>What one screen gets wrong, in the order the reader would fix it.</summary>
    private static IEnumerable<string> Complaints(string screen, string text)
    {
        if (Tags(text, FormElement()).Any())
        {
            yield return
                $"{screen} writes a form element of its own around or beside a MudForm. MudForm is " +
                "a form already, the fields of nested forms belong to the inner one, and its hidden " +
                "disabled submit button makes Enter reach nothing — silently (ADR 0093)";
        }

        if (Tags(text, ButtonElement()).Any(button => button.Contains("ButtonType.Submit", StringComparison.Ordinal)))
        {
            yield return
                $"{screen} declares a button with ButtonType.Submit. Inside MudForm's own form that " +
                "re-enables the implicit submission the component suppresses, and the real " +
                "submission it triggers is handled by nobody — the page reloads (ADR 0093)";
        }

        var declared = EnterHandler().Match(Tags(text, MudFormElement()).FirstOrDefault() ?? string.Empty);

        if (HasMultilineField(text))
        {
            if (declared.Success)
            {
                yield return
                    $"{screen} has a multi-line field and a MudForm declaring OnEnterPressed. The " +
                    "callback is raised by a form-level keydown that cannot tell which field Enter " +
                    "was pressed in, so it would submit from a textarea — and a paragraph break " +
                    "must never save a form (ADR 0093)";
            }

            yield break;
        }

        if (!declared.Success)
        {
            yield return
                $"{screen} declares a MudForm and no OnEnterPressed. Every field here is a single " +
                "line, so Enter is the gesture a visitor will try first, and MudForm suppresses the " +
                "browser's own submission — the callback is the one way the keyboard finishes the " +
                "form (ADR 0093)";

            yield break;
        }

        var handler = declared.Groups[1].Value;

        if (!Tags(text, ButtonElement()).Any(button =>
                button.Contains($"OnClick=\"{handler}\"", StringComparison.Ordinal)))
        {
            yield return
                $"{screen} answers Enter with '{handler}' and clicks no button through it. The " +
                "keyboard and the mouse must name one handler, or one of the two paths is the one " +
                "somebody tested and the other is the one that drifts (ADR 0093)";
        }
    }

    /// <summary>Whether any field of the screen renders as a textarea.</summary>
    /// <remarks>
    /// <c>Lines</c> above one and <c>AutoGrow</c> are the two ways a MudBlazor input becomes one.
    /// Nobody writes <c>Lines="1"</c>, but the guard reads the number rather than trusting that.
    /// </remarks>
    private static bool HasMultilineField(string text) =>
        LinesAttribute().Matches(text)
            .Any(lines => int.Parse(lines.Groups[1].Value, CultureInfo.InvariantCulture) > 1)
        || text.Contains("AutoGrow", StringComparison.Ordinal);

    /// <summary>The screens this rule speaks about: every razor file declaring a MudForm.</summary>
    private static IEnumerable<(string Relative, string Text)> Screens() =>
        SourceTree.AllFiles
            .Select(path => (Absolute: path, Relative: SourceTree.Relative(path)))
            .Where(file => file.Relative.StartsWith(WebRoot, StringComparison.Ordinal)
                           && file.Relative.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Select(file => (file.Relative, Text: SourceTree.ReadText(file.Absolute)))
            .Where(file => file.Text.Contains("<MudForm", StringComparison.Ordinal))
            .OrderBy(file => file.Relative, StringComparer.Ordinal);

    /// <summary>
    /// Every opening tag of one element, from its name to the angle bracket that closes it.
    /// </summary>
    /// <remarks>
    /// Hand-walked rather than matched, because a razor attribute holds C#: the obvious
    /// <c>&lt;MudButton[^&gt;]*&gt;</c> stops in the middle of
    /// <c>OnClick="@(() =&gt; Navigate())"</c>, on the arrow of the lambda. Quotes are what the
    /// scanner tracks, so a bracket inside an attribute value is text like any other.
    /// </remarks>
    private static IEnumerable<string> Tags(string text, Regex element)
    {
        foreach (var opening in element.Matches(text).Cast<Match>())
        {
            var index = opening.Index + opening.Length;
            var quote = '\0';

            while (index < text.Length)
            {
                var character = text[index];

                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (character is '"' or '\'')
                {
                    quote = character;
                }
                else if (character == '>')
                {
                    break;
                }

                index++;
            }

            yield return text[opening.Index..Math.Min(index + 1, text.Length)];
        }
    }

    /// <summary>Every <c>new DialogOptions</c> with the initializer that follows it.</summary>
    private static IEnumerable<string> Initializers(string text)
    {
        foreach (var options in DialogOptions().Matches(text).Cast<Match>())
        {
            var opening = text.IndexOf('{', options.Index);

            if (opening < 0)
            {
                continue;
            }

            var closing = text.IndexOf('}', opening);

            yield return closing < 0 ? text[opening..] : text[opening..(closing + 1)];
        }
    }

    [GeneratedRegex(@"<form(?=[\s>/])")]
    private static partial Regex FormElement();

    [GeneratedRegex(@"<MudForm(?=[\s>/])")]
    private static partial Regex MudFormElement();

    [GeneratedRegex(@"<MudButton(?=[\s>/])")]
    private static partial Regex ButtonElement();

    /// <summary>The callback and the method it names — a name, never a lambda, so the OnClick guard can compare.</summary>
    [GeneratedRegex("OnEnterPressed=\"([A-Za-z_][A-Za-z0-9_]*)\"")]
    private static partial Regex EnterHandler();

    [GeneratedRegex("Lines=\"(\\d+)\"")]
    private static partial Regex LinesAttribute();

    [GeneratedRegex(@"new DialogOptions")]
    private static partial Regex DialogOptions();
}
