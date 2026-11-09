# Documentation

Entry point for all Taakht design documents. The root [README](../README.md) is the showcase; this folder is the detail behind it.

## Layout

| Folder / file | What goes here | Not here |
|---|---|---|
| [`product/`](product/) | The business case: [business summary](product/business-summary.md), [business decisions log](product/business-decisions-log.md), [parked ideas](product/parked-ideas.md); later PRD, risk assessment, roadmap | Technical design |
| [`domain/`](domain/) | [Ubiquitous language](domain/ubiquitous-language.md), [Event Storming output](domain/event-storming.md), [context map](domain/context-map.md), [event reconciliation](domain/event-storming-reconciliation.md) | Technology choices |
| [`architecture/`](architecture/) | [Architecture overview](architecture/architecture-overview.md) (current state), [chronological decisions log](architecture/architecture-decisions-log.md), [design principles](architecture/design-principles.md), diagrams | The final "why" of a single decision (that is an ADR) |
| `adr/` | One file per architecture decision, in English. See [adr/README.md](adr/README.md) | Conventions, remaining work |
| `guidelines/` | Conventions that follow from decisions: topic naming, retry/DLQ patterns, client libraries, deploy config, code style | Decisions with trade-offs |
| `api/` | API documentation (OpenAPI, Postman) and event contracts | Internal design |
| `testing/` | Test strategy, load and stress test scenarios and recorded results (the scripts themselves live with the code) | Unit test code |
| [`process/`](process/) | [How the team works](process/development-process.md): Scrum, GitHub workflow, meetings | Architecture |
| `get-started.md` | Developer onboarding: prerequisites, setup, everyday commands, git hooks, CI, troubleshooting | Architecture, conventions that follow from decisions |
| `open-items.md` | Undecided questions and remaining steps, the working list that feeds new ADRs | Decided material |

## How the pieces relate

```
Event Storming (domain/)  ->  decisions log (architecture/)  ->  ADR (adr/)  ->  guideline (guidelines/)
      discover                    think out loud                  the "why"        the "how"
```

- `open-items.md` holds a question until it is decided. Then it moves to the decisions log and, if it passes the ADR test, becomes an ADR.
- `architecture/overview` is the current state; the decisions log is the history. When they disagree, the overview wins.

## Language

All documentation is written in English, including file names and identifiers.
