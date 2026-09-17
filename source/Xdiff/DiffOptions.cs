// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Optional parameters that control how a diff is computed and emitted. Maps to
/// the xdiff combination of <c>xpparam_t</c> (algorithm/whitespace flags)
/// and <c>xdemitconf_t</c> (context lines, function-name emission).
/// </summary>
public sealed record DiffOptions
{
    /// <summary>
    /// Diff algorithm to apply. Defaults to <see cref="DiffAlgorithm.Myers" />.
    /// </summary>
    public DiffAlgorithm Algorithm { get; init; } = DiffAlgorithm.Myers;

    /// <summary>
    /// Whitespace-equivalence mode applied to both hashing and line comparison.
    /// Defaults to <see cref="WhitespaceMode.None" /> (no equivalence).
    /// </summary>
    public WhitespaceMode Whitespace { get; init; }

    /// <summary>
    /// When <c>true</c>, hunks whose changed lines are all blank (per
    /// <see cref="WhitespaceMode" />) are marked ignorable and dropped from
    /// the emitted output if they don't merge with non-ignorable hunks. Equivalent
    /// to git's <c>--ignore-blank-lines</c>.
    /// </summary>
    public bool IgnoreBlankLines { get; init; }

    /// <summary>
    /// When <c>true</c>, run the indent heuristic on the raw edit script to shift
    /// hunks toward nicer indentation boundaries (git's <c>--indent-heuristic</c>).
    /// Applied after every algorithm (<see cref="DiffAlgorithm.Myers" />,
    /// <see cref="DiffAlgorithm.Minimal" />, <see cref="DiffAlgorithm.Patience" />,
    /// <see cref="DiffAlgorithm.Histogram" />), mirroring the reference
    /// implementation's <c>xdl_change_compact(..., xpp->flags)</c>.
    /// </summary>
    public bool IndentHeuristic { get; init; }

    /// <summary>
    /// Number of unchanged context lines to emit before and after each hunk's
    /// changes. Defaults to <c>3</c>. <c>0</c> emits no context (git's
    /// <c>--unified=0</c>). Accepted range is <c>0</c> through <see cref="int.MaxValue" />;
    /// validated when requesting a diff.
    /// </summary>
    public int ContextLines { get; init; } = 3;

    /// <summary>
    /// Maximum number of unchanged lines allowed between two adjacent changes
    /// within the same hunk before they are split into separate hunks. Defaults
    /// to <c>0</c>. Effective threshold is <c>2*<see cref="ContextLines" /> +
    /// <see cref="InterHunkLines" /></c>. Accepted range is <c>0</c> through
    /// <see cref="int.MaxValue" />; validated when requesting a diff.
    /// </summary>
    public int InterHunkLines { get; init; }

    /// <summary>
    /// When <c>true</c>, append the function-name annotation of the nearest
    /// preceding matching line to each hunk header (<c>XDL_EMIT_FUNCNAMES</c>).
    /// </summary>
    public bool IncludeFunctionNames { get; init; }

    /// <summary>
    /// When <c>true</c>, expand each hunk to cover the entire enclosing function
    /// block (<c>XDL_EMIT_FUNCCONTEXT</c>, git's <c>-W</c>/<c>--function-context</c>).
    /// </summary>
    public bool FunctionContext { get; init; }

    /// <summary>
    /// Optional predicate that decides whether a given line is a "function line"
    /// (used by <see cref="IncludeFunctionNames" /> and
    /// <see cref="FunctionContext" />). When <c>null</c>, the default matcher
    /// accepts any line whose first byte is an ASCII letter, <c>_</c>, or
    /// <c>$</c> (xdiff's <c>def_ff</c>).
    /// </summary>
    /// <remarks>
    /// When <see cref="FunctionNameExtractor"/> is non-null it takes precedence
    /// over this predicate for the match decision.
    /// </remarks>
    public Func<ReadOnlySpan<byte>, bool>? FunctionMatcher { get; init; }

    /// <summary>
    /// Optional function-name extractor: given a candidate line, returns whether
    /// it is a function line and, if so, the name text to emit after
    /// <c>@@ ... @@</c>. When non-null, this takes precedence over
    /// <see cref="FunctionMatcher"/> for both the match decision and the name
    /// text. When <c>null</c>, the whole matched line (trimmed) is used as the
    /// function name, per the xdiff default.
    /// </summary>
    /// <remarks>
    /// Supports per-language function-name regexes that extract
    /// capture group 1 (not the whole line) as the function name. The plain
    /// <see cref="FunctionMatcher"/> predicate is insufficient because it cannot
    /// return the captured substring. The returned <c>Name</c> slice may borrow
    /// from the input span or be a fresh allocation; it is consumed immediately
    /// by the emitter (copied into the hunk's <c>FunctionName</c>).
    /// </remarks>
    public Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)>? FunctionNameExtractor { get; init; }
}
