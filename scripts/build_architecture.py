"""Builds wwwroot/architecture.html and wwwroot/architecture.svg from the C# sources.

Code excerpts are cut from the real files by marker lines, so the page shows the code that runs and the
correct starting line numbers. Re-run after changing the sources:

    python scripts/build_architecture.py
"""
from html import escape
from pathlib import Path
import textwrap

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / "src" / "Lesson.C10"
WWW = APP / "wwwroot"


def excerpt(relative: str, start: str, end: str) -> tuple[str, int, str]:
    """Lines from the first containing `start` through the next containing `end` (inclusive)."""
    lines = (ROOT / relative).read_text(encoding="utf-8").splitlines()
    first = next(i for i, line in enumerate(lines) if start in line)
    last = next(i for i in range(first, len(lines)) if end in lines[i])
    return relative, first + 1, textwrap.dedent("\n".join(lines[first:last + 1]))


def code_block(relative: str, start: str, end: str, language: str = "clike") -> str:
    path, line, code = excerpt(relative, start, end)
    return (f'<p class="student-source">Source: {escape(path)} · starting at line {line}</p>'
            f'<pre><code class="language-{language}">{escape(code)}</code></pre>')


def step(title: str, intro: str, *blocks: str, note: str | None = None) -> str:
    note_html = f'<p class="student-note">{note}</p>' if note else ""
    return f'<article class="student-step"><h3>{title}</h3>{note_html}<p>{intro}</p>{"".join(blocks)}</article>'


# ---------------------------------------------------------------- UML sequence diagram

PARTICIPANTS = [
    ("Student", "#087fff"),
    ("Home.razor (circuit)", "#8838ff"),
    ("C10Lesson", "#00a36c"),
    ("Run journal", "#ff8900"),
    ("LiveQueue + TurnTracker", "#e5484d"),
    ("Managed Agents session", "#090d3b"),
]
STUDENT, PAGE, LESSON, JOURNAL, QUEUE, SESSION = range(len(PARTICIPANTS))

# (from, to, label) ; ("frame", label) opens a loop frame ; ("end",) closes it ; ("alt", label) adds a divider
MESSAGES = [
    (STUDENT, PAGE, "1. Select a scenario and edit inputs"),
    (PAGE, PAGE, "2. ExperimentGate: one experiment at a time"),
    (PAGE, LESSON, "3. Run(input) · validate queued messages"),
    (LESSON, PAGE, "4. Outcome, summary, steps, evidence"),
    (PAGE, JOURNAL, "5. Append to .lab-data/runs.jsonl"),
    (PAGE, STUDENT, "6. Render evidence over the SignalR circuit"),
    (STUDENT, PAGE, "7. Run queue live"),
    (PAGE, QUEUE, "8. Run(messages, competingWriter)"),
    (QUEUE, SESSION, "9. sessions.create · text-only, 10 USD budget"),
    (QUEUE, SESSION, "10. Open the event stream before any send"),
    ("frame", "loop · for each queued message"),
    (QUEUE, SESSION, "11. send user.message → accepted event ID"),
    (SESSION, QUEUE, "12. user.message · queued, then processed"),
    (SESSION, QUEUE, "13. agent.message · the turn's answer"),
    (SESSION, QUEUE, "14. status_idle · end_turn → advance"),
    ("alt", "foreign user.message ID → conflict, stop sending"),
    ("end",),
    (QUEUE, PAGE, "15. Explained timeline steps, then the outcome"),
    (PAGE, STUDENT, "16. Inspect each turn and its evidence"),
]


def build_svg() -> str:
    col, left, top, row = 262, 30, 92, 54
    width = left * 2 + col * len(PARTICIPANTS)
    centre = [left + col * i + col // 2 for i in range(len(PARTICIPANTS))]
    rows = sum(1 for m in MESSAGES if len(m) == 3 or m[0] in ("frame", "alt")) + 1
    lifeline_end = top + 70 + rows * row + 30
    height = lifeline_end + 70
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-labelledby="title desc">',
        '<title id="title">C10 UML sequence workflow (Blazor / .NET 10)</title>',
        f'<desc id="desc">{escape(" ".join(m[2] for m in MESSAGES if len(m) == 3))}</desc>',
        '<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10z" fill="#087fff"/></marker></defs>',
        '<rect width="100%" height="100%" rx="22" fill="#fff"/>',
        '<text x="40" y="54" font-family="Arial" font-weight="700" font-size="30" fill="#090d3b">C10 · Multi-turn Sessions and Message Queues · Blazor / .NET 10</text>',
    ]
    for (name, colour), x in zip(PARTICIPANTS, centre):
        out.append(f'<rect x="{x - col // 2 + 8}" y="{top}" width="{col - 16}" height="62" rx="14" fill="{colour}"/>')
        out.append(f'<text x="{x}" y="{top + 39}" text-anchor="middle" fill="white" font-size="19" font-family="Arial" font-weight="700">{escape(name)}</text>')
        out.append(f'<path d="M{x} {top + 62} V{lifeline_end}" stroke="#b1c9e4" stroke-dasharray="6 6"/>')
    y = top + 62 + row
    frame_top = None
    for message in MESSAGES:
        if message[0] == "frame":
            frame_top = y - 22
            out.append(f'<text x="{centre[QUEUE] - col // 2 + 14}" y="{y}" font-family="Arial" font-weight="700" font-size="17" fill="#e5484d">{escape(message[1])}</text>')
            y += row
        elif message[0] == "alt":
            x1, x2 = centre[QUEUE] - col // 2 + 4, centre[SESSION] + col // 2 - 4
            out.append(f'<path d="M{x1} {y - 30} H{x2}" stroke="#e5484d" stroke-dasharray="8 5"/>')
            out.append(f'<text x="{x1 + 10}" y="{y}" font-family="Arial" font-style="italic" font-size="17" fill="#a13a20">alt · {escape(message[1])}</text>')
            y += row
        elif message[0] == "end":
            x1, x2 = centre[QUEUE] - col // 2 + 4, centre[SESSION] + col // 2 - 4
            out.append(f'<rect x="{x1}" y="{frame_top}" width="{x2 - x1}" height="{y - row // 2 - frame_top}" rx="10" fill="none" stroke="#e5484d" stroke-width="2"/>')
        else:
            source, target, label = message
            x1, x2 = centre[source], centre[target]
            if source == target:
                out.append(f'<path d="M{x1} {y - 14} h70 v22 h-66" fill="none" stroke="#087fff" stroke-width="2.5" marker-end="url(#arrow)"/>')
                out.append(f'<text x="{x1 + 80}" y="{y + 2}" font-family="Arial" font-size="17" fill="#090d3b">{escape(label)}</text>')
            else:
                dashed = ' stroke-dasharray="9 6"' if x2 < x1 else ""
                out.append(f'<path d="M{x1} {y} H{x2 + (-6 if x2 > x1 else 6)}" stroke="#087fff" stroke-width="2.5"{dashed} marker-end="url(#arrow)"/>')
                out.append(f'<text x="{min(x1, x2) + 12}" y="{y - 10}" font-family="Arial" font-size="17" fill="#090d3b">{escape(label)}</text>')
            y += row
    out.append(f'<text x="40" y="{height - 26}" font-family="Arial" font-size="18" fill="#3a5578">LOCAL: authored experiment → evidence → journal. LIVE QUEUE: one writer, one message per turn, advance only on a settled idle.</text>')
    out.append("</svg>")
    return "\n".join(out)


# ---------------------------------------------------------------- page

def build_html() -> str:
    lesson = "src/Lesson.C10/Lessons/C10Lesson.cs"
    home = "src/Lesson.C10/Components/Pages/Home.razor"
    sections = [
        '<section class="student-section"><h2>Step 0 · Prepare the application before the workflow</h2>'
        '<p>No API key is required for the local experiment. The two live features need your own API key, agent ID and environment ID.</p>'
        '<p>If you do not have resource IDs yet, set your API key and run <code>dotnet run --project src/Lesson.C10 -- setup</code>. '
        'It creates the environment before a text-only agent, saves each ID to <code>.env</code> immediately, and reuses IDs already configured. '
        'It uses CLAUDE_MODEL or the source default. An uncertain network failure may have created a resource: inspect your account before retrying.</p>'
        '<pre><code class="language-powershell">dotnet restore\nCopy-Item .env.example .env\ndotnet run --project src/Lesson.C10</code></pre>'
        '<p>Create your local .env from this safe template. The local path needs no remote resources.</p>'
        + code_block(".env.example", "ANTHROPIC_API_KEY", "PORT", "dotenv")
        + '<p>Optional prerequisite: the environment is created before the agent, and each ID is saved as soon as it exists.</p>'
        + code_block("src/Lesson.C10/Services/ResourceSetup.cs", "if (!ManagedAgentSettings.Valid(Environment.GetEnvironmentVariable(\"CLAUDE_MANAGED_ENVIRONMENT_ID\")))", "DotEnv.Save(envPath, \"CLAUDE_MANAGED_AGENT_ID\"")
        + "</section>",

        '<section class="student-section"><h2>UML sequence diagram</h2><p>Read from top to bottom. Columns are participants; arrows are ordered messages. '
        'Steps 1–6 are the local experiment; steps 7–16 are the live queue. Open the full-size image or scroll horizontally to read every label.</p>'
        '<p><a href="/architecture.svg" target="_blank" rel="noopener">Open SVG</a> · <a href="/architecture.svg" download>Download SVG</a> · <a href="/architecture.png" download>Download PNG</a></p>'
        '<div class="student-diagram"><img src="/architecture.svg" alt="UML sequence diagram showing the local experiment and the live session queue"></div></section>',

        '<section class="student-section"><h2>Walk through the application step by step</h2>'
        + step("1 · Load scenarios and collect input",
               "Home.razor is an interactive server component. It turns each scenario input into an editable field and reads the fields back into JSON. Lists and objects are edited as JSON text.",
               code_block(home, "void Populate(JsonObject input)", "}).ToList();"),
               code_block(home, "JsonObject Read()", "return input;"))
        + step("2 · Run on the server, one experiment at a time",
               "Button clicks travel over the Blazor SignalR circuit, so there is no public JSON API. The server binds to 127.0.0.1 only. ExperimentGate admits one experiment across all browser tabs, and local results are appended to the run journal.",
               code_block(home, "await Gate.Run(async () =>", "return true;"),
               code_block("src/Lesson.C10/Services/RunJournal.cs", "public sealed class ExperimentGate", "finally { _busy.Release(); }"))
        + step("3 · Apply the Multi-turn Sessions and Message Queues rules",
               "The local reducer rejects blank messages, reports a conflict when a competing writer is present, and otherwise issues one receipt per message. Its idle/end_turn status is authored data, not an observation.",
               code_block(lesson, "public Task<Result> Run(JsonObject input)", "\"Confirm reusable idle before advancing\""))
        + step("4.1 · Live queue: open the stream before sending",
               "SSE has no replay. The C# stream is a lazy IAsyncEnumerable, so a background task starts it and buffers every event in a channel before the first message is sent.",
               code_block("src/Lesson.C10/Services/SessionStream.cs", "public SessionStream(AnthropicClient client", "        });"),
               note="This is the step the local experiment only describes. The live queue drives a real Managed Agents session; API use may incur charges.")
        + step("4.2 · Live queue: send exactly one message and correlate it",
               "Each send returns the server-assigned user event ID, which becomes the command's correlation key. With Concurrent Input checked, a second writer posts into the same session and the queue never learns that ID. The queue reads events only after the send returns, so its own ID is always known first.",
               code_block("src/Lesson.C10/Services/LiveQueue.cs", "for (var index = 0; index < messages.Count; index++)", "}, turn.Answer));"))
        + step("4.3 · Live queue: advance only after a settled idle",
               "TurnTracker has no SDK types, so it is unit-tested against recorded event sequences. Classify decides what each event means for the current turn: a foreign user event is a conflict; an idle seen before our message was processed belongs to an earlier turn; requires_action is a transient idle; only end_turn settles the turn.",
               code_block("src/Lesson.C10/Services/TurnTracker.cs", "Observation Classify(SessionSignal signal)", "_ => Observation.Ignored,"))
        + step("4.4 · Explain each event as it arrives",
               "Every observation becomes a plain-language timeline step tagged with the concept it demonstrates. LiveQueue passes each step to the page through a callback, and Blazor pushes it to the browser while the session is still running.",
               code_block("src/Lesson.C10/Services/Teaching.cs", "public static TeachingStep? Describe(", "_ => null,"),
               code_block("src/Lesson.C10/Components/Pages/Home.razor", "_queue = await Queue.Run(", "}));"))
        + step("5 · Optional live commentary",
               "A separate text-only session explains the local evidence in three sentences. It shares the stream-first helper and the interrupt-on-failure path with the live queue.",
               code_block("src/Lesson.C10/Services/LiveCommentary.cs", "var session = await ManagedSessions.CreateTextOnly", "throw ManagedSessions.Failure(sessionId, error);"),
               note="This request asks Claude to explain the local result. It does not run the queue.")
        + "</section>",

        '<section class="student-section"><h2>What the evidence proves</h2>'
        '<p class="student-note">The local experiment is an in-process serialized queue with authored turn events. It demonstrates admission and attribution, not a durable distributed queue. The run journal stores results, not production workflow state.</p>'
        '<p>The live queue observes a real session: accepted user event IDs, processed echoes, each turn\'s answer, and the idle/end_turn that allowed the next message. '
        'It still has a single in-process writer, so it does not show a durable or distributed queue. Check the outcome, the receipts and the answers together. '
        'A rendered answer alone does not prove that a tool ran, a remote operation succeeded, or work was cancelled.</p></section>',
    ]
    return (
        '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
        '<title>C10 · Multi-turn Sessions and Message Queues</title><link rel="stylesheet" href="/student.css"><link rel="stylesheet" href="/code-highlighting.css"></head>'
        '<body><main class="student-shell"><div class="student-brand"><a href="/">How <span>Claude Managed Agents</span> Works</a><span class="student-badge">CONCEPT 10 / 60</span></div>'
        '<div class="student-hero"><div><div class="student-eyebrow">STUDENT APPLICATION GUIDE · C# / BLAZOR</div><h1>Multi-turn Sessions and Message Queues</h1>'
        '<p>Understand the concepts, follow the source code, and verify the application yourself.</p></div><img src="/astronaut.png" alt=""></div>'
        '<nav class="student-nav" aria-label="Application sections"><a href="/guide.html#learning">What you will learn</a><a href="/guide.html#run-guide">How to run</a>'
        '<a href="/architecture.html">Architecture &amp; workflow</a><a href="/#application-lab">Application lab</a>'
        '<button id="student-theme-toggle" type="button" aria-pressed="false">Switch to dark mode</button></nav>'
        + "".join(sections)
        + '<footer>LESSON C10 · LEARN → CONFIGURE → RUN → VERIFY<p><a href="/guide.html#learning">Learning goals</a> · <a href="/guide.html#run-guide">Run instructions</a> · '
        '<a href="/architecture.html">Workflow</a> · <a href="/#application-lab">Lab</a></p></footer></main>'
        '<script src="/navigation.js"></script><script defer src="/prism.js"></script><script defer src="/code-highlighting.js"></script></body></html>'
    )


if __name__ == "__main__":
    (WWW / "architecture.svg").write_text(build_svg(), encoding="utf-8")
    (WWW / "architecture.html").write_text(build_html(), encoding="utf-8")
    print("Wrote wwwroot/architecture.svg and wwwroot/architecture.html")
