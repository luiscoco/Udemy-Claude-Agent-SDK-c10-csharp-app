# C10 · Multi-turn Sessions and Message Queues (Blazor / .NET 10)

C# port of [`lesson-c10-app`](../lesson-c10-app). It is a Blazor Web App (interactive server rendering) that uses the official [Anthropic](https://www.nuget.org/packages/Anthropic) NuGet package, version 13.3.0, in place of `@anthropic-ai/sdk`.

Runs an in-process serialized queue with authored turn events. It demonstrates admission and attribution, not a durable distributed queue. Each run starts from a fresh local seed. The run journal stores results, not production workflow state.

## Concepts students will learn

- One writer per conversation
- Correlate each accepted user event
- Advance only after settled completion

## Configure and run

Use the .NET 10 SDK. In PowerShell, from this folder (`lesson-c10-csharp-app`):

```powershell
dotnet --version
dotnet restore
Copy-Item .env.example .env
notepad .env
```

No API key is required for the local experiment. The live queue and the live commentary need your own API key, agent ID and environment ID.

If you do not have resource IDs yet, set your API key and run the setup command. It creates the environment first, then a text-only agent. It saves each ID to `.env` as soon as it is created and reuses any IDs already configured. The agent model comes from `CLAUDE_MODEL`, or `claude-opus-5-5` if that is not set.

```powershell
dotnet run --project src/Lesson.C10 -- setup
dotnet run --project src/Lesson.C10
```

Open `http://127.0.0.1:3010` (or the `PORT` from `.env`). `.env` and the `.lab-data/runs.jsonl` journal are kept in this folder (the one containing `Lesson.C10.slnx`). Stop the server with Ctrl+C.

## How the page teaches the lesson

The lab page is laid out so a student can work through it top to bottom:

1. **The three ideas**: one card per concept, each with a one-sentence explanation. Every later step is tagged with the concept it demonstrates.
2. **Your lab, step by step**: four runs to do in order:
   - **Steps 1–2** run "Two correlated turns" and "Detect a competing writer" locally, with authored receipts.
   - **Steps 3–4** run the same two scenarios against a real session.

   Each step says what to look for. Its **Load and run** button loads the scenario and runs it, and the step is ticked when the run produces the expected outcome. The next step to do is highlighted.
3. **Result explanations**: after each run, a "What this means" (local) or "What this shows" (live) note explains the outcome in plain language.
4. **Live timeline**: before a live run, a short list says what will happen. During the run, each meaningful session event appears as it arrives, numbered and explained, with its concept tag. Examples are "The session picked up cmd-1's event", "Claude answered: the turn is not over yet", "Idle with end_turn: turn 1 is settled" and "Unknown user event: another writer!". Technical events such as spans and usage are left out of the timeline but kept in the evidence JSON.
5. **API errors**: these are shown as a cause and a fix, such as a rejected key (401), unknown agent or environment IDs (404), rate limiting (429), a temporary server problem (5xx), the network or a timeout.

The default scenario says "My project is called Aurora. Remember that." and then asks "What is my project called?". The second turn can only be answered from the first, so the shared session history shows in the answer.

## Project layout

| Node app (`lesson-c10-app`) | Blazor app (`lesson-c10-csharp-app`) |
|---|---|
| `src/core.ts` | `src/Lesson.C10/Lessons/Core.cs` (input validation, `Report`, `Hash`, `SafePath`) |
| `src/lesson.ts` | `src/Lesson.C10/Lessons/C10Lesson.cs` (scenarios and the queue reducer) |
| `src/provider.ts` | `src/Lesson.C10/Services/LiveCommentary.cs` (Managed Agents session over `client.Beta.Sessions`) |
| `scripts/setup-resources.mts` | `src/Lesson.C10/Services/ResourceSetup.cs` (`dotnet run -- setup`) |
| `src/server.ts` + `src/client.ts` + `public/index.html` | `Program.cs` + `Components/Pages/Home.razor` (interactive server component; no `/api/*` endpoints) |
| `.lab-data/runs.jsonl` writer, `busy` flag | `Services/RunJournal.cs` (`RunJournal`, `ExperimentGate`) |
| `process.loadEnvFile(".env")` | `Services/DotEnv.cs` |
| `src/lesson.test.ts` | `tests/Lesson.C10.Tests` (xUnit) |
| `public/*` | `src/Lesson.C10/wwwroot/*` (guide, architecture, styles, Prism, images) |

## Live queue (new in the C# port)

The Node app only had a live *commentary* about the local result. The **Run queue live** button runs the lesson itself in a real Managed Agents session ([`LiveQueue.cs`](src/Lesson.C10/Services/LiveQueue.cs)):

1. Create one text-only session and open its event stream before anything is sent.
2. For each queued message (at most 5), send exactly one `user.message` and keep the server-assigned event ID from the send response as that command's correlation key.
3. [`TurnTracker`](src/Lesson.C10/Services/TurnTracker.cs) follows the turn. It records the queued and processed echoes of our event and collects the `agent.message` text. It settles only on a `session.status_idle` with `end_turn` that arrives after our message was processed. `requires_action` is transient, and other stop reasons stop the queue.
4. Only then is the next message sent. The second answer shows whether the session kept the first turn.

[`Teaching.cs`](src/Lesson.C10/Services/Teaching.cs) turns each `TurnTracker` observation into a timeline step. `LiveQueue` passes the steps to the page through a callback while the session is still running.

With **Concurrent Input** checked, a second writer posts into the same session after the first message. The queue sees a `user.message` ID it never sent, reports `conflict` and stops advancing. It does not send an interrupt, because that would be one more write into a conversation it no longer owns.

`TurnTracker` has no SDK types, so [TurnTrackerTests](tests/Lesson.C10.Tests/TurnTrackerTests.cs) replays recorded event sequences without calling the API.

## Live commentary with the C# SDK

`LiveCommentary.Explain` follows the same flow as `provider.ts`:

1. `client.Beta.Sessions.Create` with `BetaManagedAgentsAgentWithOverridesParams` (no tools, MCP servers or skills) and a `BetaManagedAgentsBudgetLimit` of 10 USD.
2. Open `client.Beta.Sessions.Events.StreamStreaming` **before** sending. The C# stream is a lazy `IAsyncEnumerable`, so a background task starts it and buffers events into a `Channel`. SSE has no replay.
3. `client.Beta.Sessions.Events.Send` with a `BetaManagedAgentsUserMessageEventParams`.
4. Read events with `TryPick*` until `session.status_idle` with a stop reason other than `requires_action`, or until the session is terminated or reports an error. Each request has a 60-second deadline.
5. On failure, send a `BetaManagedAgentsUserInterruptEventParams` and report the session ID, because the session's stop is unconfirmed.

## Validation

```powershell
dotnet test
```

The tests never call the API. They cover four areas:
- **Scenarios:** each lesson scenario must produce its expected outcome and evidence, as in `lesson.test.ts`, and a blank queued message must be rejected.
- **Turn tracking:** `TurnTracker` replays the event order recorded from two real sessions (a passed queue and a competing writer). It is also checked against a foreign writer, an idle left over from an earlier turn, `requires_action`, other stop reasons and termination.
- **Narration and errors:** timeline steps for key observations, and the student-facing error messages.
- **`.env` handling:** `DotEnv` load and save.

The GitHub Actions workflow in `.github/workflows/dotnet.yml` runs restore, build and test on every push and pull request.

If the app is running, `dotnet test` cannot overwrite the locked binaries. Either stop the app, or build into a separate folder: `dotnet test --artifacts-path .artifacts`.

## Architecture page

[`wwwroot/architecture.html`](src/Lesson.C10/wwwroot/architecture.html) and its sequence diagram (`architecture.svg`) are generated from the C# sources. Code excerpts are cut from the real files, with their starting line numbers. After changing the code, regenerate them:

```powershell
python scripts/build_architecture.py
```

`architecture.png` is a rendering of the SVG. Re-export it (for example, by screenshotting `/architecture.svg`) when the diagram changes.

## Notes

- The "How it works" panel shows `Lessons/C10Lesson.cs`, which is embedded in the assembly.
- Both live features create real sessions capped at 10 USD each and may incur charges. Sessions are left in your account; the app does not archive or delete them.
