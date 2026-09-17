# Repository guidance

Xdiff is a managed C# port of standalone xdiff. Read the project files under
`source/` and `tests/` for target frameworks, and `global.json` for the SDK
version. Read `docs/overview.md` for API contracts and compatibility scope
before changing diff or merge behavior.

## Layout

- `source/Xdiff/`: library; `Diff.cs` and `Merger.cs` are the public facades.
  `Core/` holds internal data structures, `Prepare/` classifies input records,
  `Algorithms/` implements diff algorithms and compaction, `Emit/` produces
  hunks and output, `Merge/` handles three-way merging, and `Util/` holds helpers.
- `tests/Xdiff.UnitTests/`: xUnit v3 tests, embedded `Fixtures/`, and reference
  fixture generation scripts.
- `docs/`: user guides and option references. `source/Xdiff/README.md` is the
  NuGet package readme; the root `README.md` is a brief project introduction.
- `Directory.Build.props`: shared build, analyzer, and versioning settings.
  `global.json` selects the SDK and Microsoft Testing Platform (MTP).

## Development commands

Run commands from the repository root with the SDK selected by `global.json`.

```sh
dotnet restore Xdiff.slnx
dotnet build Xdiff.slnx --no-restore
dotnet test --solution Xdiff.slnx
```

For focused test runs, discover names first and put xUnit runner options after
`--`:

```sh
dotnet test tests/Xdiff.UnitTests/Xdiff.UnitTests.csproj --list-tests
dotnet test tests/Xdiff.UnitTests/Xdiff.UnitTests.csproj -- --filter-class "Xdiff.UnitTests.GoldenDiffTests"
```

Use `.agents/skills/dotnet-mtp-tests/SKILL.md` for filtering and coverage details.
This repository uses MTP with `coverlet.MTP`; do not substitute VSTest
`--filter` or `--collect:"XPlat Code Coverage"` options. Keep generated coverage
and reports under `artifacts/`. Restore repository tools with `dotnet tool restore`
when needed; see `.agents/skills/reportgenerator/SKILL.md` for coverage reports
and `.agents/skills/dotnet-inspect/SKILL.md` for .NET API inspection.

For code changes, run the relevant tests while iterating and the full solution
tests before finishing. For documentation-only changes, verify paths, examples,
and claims; a build or test run is unnecessary unless executable behavior is
affected. Report checks performed and any checks that could not run.

## Implementation conventions

- Follow `.editorconfig` and surrounding code: four-space C# indentation,
  file-scoped namespaces, nullable annotations, and XML documentation for public
  APIs. Preserve existing copyright and license headers.
- If whitespace or code-style warnings occur, try `dotnet format Xdiff.slnx`
  for automatic fixes. Review the diff and keep formatting changes scoped to
  the task.
- Keep the library synchronous, allocation-aware, and AOT-compatible. Preserve
  its lack of unsafe code, reflection, I/O, and process-global mutable state.
- Preserve byte-oriented behavior, including line endings, missing final
  newlines, and non-UTF-8 input. String overloads perform UTF-8 conversion.
- `Diff.Compute` returns line content borrowing input memory; unified diffs and
  merge results own their output. Preserve these ownership contracts.
- Unified output contains hunks without Git file headers or repository metadata.
  Do not assume Git configuration, attributes, binary detection, or language
  drivers are loaded. The indentation heuristic defaults to off for diffs and
  remains disabled during merges and conflict refinement.
- Add regression coverage for behavior fixes, especially algorithm boundaries,
  whitespace handling, output framing, and conflicts. Match reference options
  explicitly when comparing output with Git or C xdiff.
- Update the relevant guides and package readme when public behavior changes.
  Keep dependency changes intentional. Whenever any dependency or the .NET SDK
  version changes, run `dotnet restore Xdiff.slnx --force-evaluate` to regenerate
  all solution package lock files. Review and include the resulting
  `packages.lock.json` changes with the dependency or SDK change. Avoid unrelated
  formatting or dependency churn.

## Fixtures and attribution

Treat golden fixtures as reference evidence. Investigate mismatches before
changing expected output; do not regenerate goldens merely to make tests pass.
Read `tests/Xdiff.UnitTests/FIXTURE-NOTICES.md` and the relevant generator before
modifying reference fixtures. Generators may need external source checkouts and
C build tools; ordinary test runs use the embedded fixtures.

Only `tests/Xdiff.UnitTests/generate-coverage-goldens.sh` enforces a standalone
xdiff revision; read that script for the required commit and source checks.
Do not extend that provenance claim to other fixture families. Record source revisions,
generation details, and attribution when adding or replacing upstream material.

Respect `.gitattributes`: C# source uses CRLF, shell scripts use LF, and fixture
text uses LF with explicit binary exceptions. Do not normalize fixture bytes
or add/remove final newlines as cleanup.

Preserve `LICENSE`, `THIRD-PARTY-NOTICES.md`, and fixture-specific notices and
licenses; update the applicable notices when incorporating upstream material.

## Before committing

Before any agent-created commit, verify the effective repository setting with
`git config --get core.autocrlf`: it must be `true` on Windows and `input` on
Linux or macOS. Instruct the user to configure the appropriate setting from
the repository root before any commits:

- Windows: `git config core.autocrlf true`
- Linux/macOS: `git config core.autocrlf input`

If the setting is missing or incorrect, ask the user to run the appropriate
command and verify it again before committing. Do not commit until the setting
matches the operating system.
