# Project Memory

This folder stores durable project knowledge for AI agents.

Use it for verified findings that should survive chat resets:

- architecture notes
- debugging findings
- important decisions
- product and business rules
- platform-neutral feature specifications
- workflow algorithms and state diagrams
- architecture migration history
- known pitfalls
- local workflows
- dependency maps
- reusable agent experience that may improve `gi`

Do not store secrets or credentials here.

## Summary Versus Project Memory

`tools/summary/` is compact handoff state for the current or recent chat.
`tools/project-memory/` is long-lived product and project knowledge.

Write project-memory documents so another agent could rebuild the project on a
different language, framework, platform, or UI stack and preserve the same
behavior. Code is the current implementation; project-memory specifications are
the portable behavioral source of truth.

Recommended specification structure:

```text
tools/project-memory/
  architecture-migrations.md
  specs/
    product-overview.md
    glossary.md
    features/
    business-rules/
    data-model/
    integration-contracts/
```

Split documents by meaning. Keep feature behavior, business logic, architecture
history, and implementation mapping searchable as separate focused files instead
of one giant document.

For each non-trivial feature or workflow, record:

- product intent and success signal;
- actors, roles, permissions, and preconditions;
- user-visible workflow, branches, and terminal states;
- business rules, invariants, inputs, outputs, and validation;
- background work, ordering, retries, cancellation, and failure behavior;
- Mermaid flowcharts or state diagrams when useful;
- verification rules for preserving behavior after a rewrite;
- current implementation map with evidence paths.

Keep major rewrites, platform moves, framework replacements, storage changes,
service splits, and routing changes in `architecture-migrations.md`.

## Reusable Experience For GI

When this project reveals a reusable workflow, failure pattern, token-saving
tactic, or agent-instruction improvement, write a concise recommendation for the
shared instruction kit.

Prefer the `updates/` folder in an available checkout/cache of the canonical
shared-instruction source repo when this repository is being maintained:

```text
<general-instructions checkout>\updates\
```

If the shared library is unavailable, use a local intake folder:

```text
tools/project-memory/instruction-updates/
```

Recommendations should include:

- observed problem or repeated friction
- reusable rule, pattern, template, checklist, or migration idea
- evidence paths or commands
- expected benefit for token economy, startup retrieval, safety, or workflow
- privacy review notes

Do not include secrets, credentials, private user data, production data, or
unnecessary project-specific details.

## Agent Memory SQLite

If the project benefits from searchable agent memory, use a local SQLite
database as an agent index/experience store, not as the application database.

Recommended path:

```text
tools/project-memory/project_memory.sqlite
```

The SQLite file is usually local/generated and ignored by git when it is large
or rebuildable. Commit the indexing script, schema notes, and Markdown exports
instead.

Use the database for verified facts, searchable file/symbol indexes, debugging
findings, useful commands, recurring failures, and durable notes with evidence
paths. Do not store secrets, credentials, private user data, or production data.

Do not dump the database into chat. Query it by symbol, path, topic, error, or
feature name with small limits.

Use SQLite for deterministic project facts and graphs: paths, symbols, exact
references, generated identifiers, asset links, reverse dependencies, commands,
failures, and evidence-backed notes.

Keep logical separation between code memory and specification memory. Code
memory tracks current implementation facts such as files, symbols, commands,
schemas, and errors. Specification memory tracks product behavior, business
rules, feature algorithms, workflow contracts, architecture migrations, and
verification guarantees. Small projects may use one SQLite database with source
metadata. Larger projects should split code and spec indexes into separate
databases, schemas, collections, or source groups.

Use vector retrieval only as a second semantic layer for conceptual questions
over curated notes, summaries, architecture docs, and selected chunks. Do not
replace exact graph queries with embeddings, and always verify current source
files before editing because generated indexes can be stale.

## Two Memory Layers

- Markdown is the human-reviewable layer. Keep summaries, decisions,
  architecture notes, and curated exports concise.
- SQLite is the searchable agent-memory layer for detailed findings,
  file/symbol indexes, references, commands, failures, and evidence-backed notes.

Do not blindly migrate all Markdown into SQLite. When Markdown memory becomes
too large to read cheaply, introduce or rebuild the SQLite memory/index and keep
Markdown as the concise reviewable export.

## RAG System Structure

When the project needs retrieval that can grow beyond Markdown and SQLite FTS,
add:

```text
tools/project-memory/rag-system.json
```

Keep vector stores such as Chroma, Qdrant, and pgvector behind retrieval
adapters so prompts and agent workflows do not depend on one storage backend.
Before enabling vector retrieval, prepare semantic-ready chunks and embedding
metadata. Keep generated files such as
`tools/project-memory/semantic-corpus.jsonl` ignored.

For a local semantic MVP, build Chroma from exported chunks:

```powershell
python .\tools\project-memory\build_project_memory_index.py rebuild
python .\tools\project-memory\build_project_memory_index.py export-chunks
uv run --with chromadb python .\tools\project-memory\build_chroma_index.py rebuild
```

## Activation Limits And Diagnostics

Start with Markdown specifications and targeted search. Use generated databases
when size or retrieval failures justify them.

Default SQLite/FTS activation limits:

- tracked text sources exceed 50 files;
- project-memory Markdown/JSON exceeds 25 files or about 200 KB;
- feature specifications exceed 10 files;
- exact retrieval repeatedly misses paths, commands, symbols, or feature specs;
- startup restore needs too many focused file reads.

Default vector activation limits:

- semantic-ready chunks exceed 300;
- curated project-memory specs exceed about 500 KB;
- feature specifications exceed 25 files;
- conceptual retrieval misses relevant memory at least three times;
- multiple agents need conceptual lookup over the same memory.

Use `gi sql` to inspect SQLite/FTS readiness and metrics. The agent should read
`rag-system.json`, run the local stats helper when available, count memory/spec
files, compare them with limits, and report whether SQL indexing is absent,
current, stale, or recommended.

Use `gi vector` to inspect vector readiness and metrics. The agent should read
embedding/vector metadata, check semantic corpus size and chunk count, run the
vector adapter status helper when available, and report collection, record
count, index path, freshness caveats, and readiness.

## Suggested Files

- `pending-tasks.md`: active project-wide plans and multi-step work.
- `STUDY_PLAN.md`: roadmap for understanding the project.
- `git-preferences.json`: commit-message language preferences.
- `system-preferences.json`: agent user-facing working language preferences.
- `rag-system.json`: RAG source, exclusion, retrieval, context-packet, and
  writeback configuration.
- `architecture-migrations.md`: major architecture rewrites, platform moves,
  framework replacements, and storage/service/routing migrations.
- `specs/`: platform-neutral feature, business-rule, data-model, and
  integration-contract specifications.
- `semantic-retrieval-evals.md`: small eval set for semantic and hybrid
  retrieval quality.
- `build_chroma_index.py`: optional local Chroma adapter when semantic
  retrieval is enabled.
- `NOTES.md`: reviewable export of durable notes from local agent memory.
- `architecture.md`: verified architecture notes.
- `decisions.md`: durable decisions and rationale.
- `known-issues.md`: recurring bugs, caveats, and workarounds.

## Task Planning

For analysis, refactoring, migration, or multi-step implementation tasks, keep a
concise checklist in `pending-tasks.md` or a dedicated task plan in this folder.

Include:

- goal
- planned changes
- execution order
- risks or dependencies
- verification steps

Update progress as meaningful steps complete. Keep plans task-relevant and avoid
full diffs, large logs, generated outputs, secrets, credentials, or private
production data.

## Rule

If a future agent would waste time rediscovering the same fact, write it down.
