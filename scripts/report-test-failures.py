#!/usr/bin/env python3
"""Turn the TRX reports of a failed test run into GitHub Actions annotations.

The TRX files are already published as an artifact, and that artifact is the full record. But
reading it means downloading it, which needs a browser session and a network path to Azure blob
storage — and neither is available to everything that has to diagnose a red run. What the run's
own page showed instead was `Process completed with exit code 1`, which names no test, no
assertion and no suite.

An annotation costs nothing and lands on the page. This does not replace the artifact; it makes
the first question answerable without it.

Usage: report-test-failures.py <results-directory>
"""

import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

# GitHub keeps ten annotations per step and drops the rest silently. Ten failures is already more
# than anybody reads one by one, and the count below says how many were left out.
MAX_ANNOTATIONS = 10


def named(element, name):
    """Yield the descendants of `element` whose local name is `name`, namespace ignored.

    TRX elements sit in a namespace whose identifier is a plain-scheme URI. That URI is a name
    and not an address — nothing ever fetches it, and it cannot be moved to a secure scheme
    because it has to match, character for character, what the test runner writes. Matching on the
    local name drops the need to quote it at all: one fewer constant to keep in step with a schema
    somebody else owns, and one fewer string that reads like an insecure endpoint to anything
    scanning this file.
    """
    for candidate in element.iter():
        if candidate.tag.rpartition("}")[2] == name:
            yield candidate


def escape(text):
    """Encode a message for a workflow command, which is newline-delimited."""
    return (
        text.replace("%", "%25")
        .replace("\r", "")
        .replace("\n", "%0A")
        .replace(":", "%3A")
    )


def failures(trx_path):
    """Yield (test name, message, stack trace) for every failed result in one TRX file."""
    try:
        root = ElementTree.parse(trx_path).getroot()
    except ElementTree.ParseError as error:
        print(f"::warning::Could not parse {trx_path.name}: {error}")
        return

    for result in named(root, "UnitTestResult"):
        if result.get("outcome") != "Failed":
            continue

        message = next(named(result, "Message"), None)
        stack = next(named(result, "StackTrace"), None)

        yield (
            result.get("testName") or "<unnamed test>",
            (message.text or "").strip() if message is not None else "",
            (stack.text or "").strip() if stack is not None else "",
        )


def main():
    if len(sys.argv) != 2:
        print("::error::report-test-failures.py takes exactly one argument: the results directory")
        return 1

    directory = Path(sys.argv[1])

    if not directory.is_dir():
        print(f"::warning::No test results at {directory} — the run failed before any test ran")
        return 0

    found = [failure for trx in sorted(directory.glob("*.trx")) for failure in failures(trx)]

    if not found:
        print(
            "::warning::The step failed and no TRX records a failed test. The run most likely "
            "died before or between the suites — a container that never became ready, or a host "
            "that threw on start-up."
        )
        return 0

    for name, message, stack in found[:MAX_ANNOTATIONS]:
        # The first stack frame is nearly always the assertion helper; the second is the test. Two
        # lines is enough to place the failure without burying the message under a trace.
        head = "\n".join(stack.splitlines()[:2])
        body = message if not head else f"{message}\n{head}"
        print(f"::error title={escape(name)}::{escape(body)}")

    if len(found) > MAX_ANNOTATIONS:
        print(
            f"::warning::{len(found)} tests failed; the {MAX_ANNOTATIONS} above are all GitHub "
            "will show. The published artifact has the rest."
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
