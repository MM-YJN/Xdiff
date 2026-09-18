// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff.Emit;

/// <summary>
/// Base class for sinks that receive a unified diff one hunk at a time. Tracks the current hunk's
/// header fields so subclasses can read them as properties instead of carrying them through every callback.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: the emitter calls <see cref="BeginHunk" /> once per hunk, then <see cref="Line" /> once per
/// line in that hunk, and <see cref="EndHunk" /> when the next hunk starts or when
/// <see cref="Diff.Compute(HunkSinkBase, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, DiffOptions?)" />
/// flushes at the end of the diff. Hunk properties are only meaningful from <see cref="BeginHunk" />
/// through the matching <see cref="EndHunk" />; they read <c>0</c>/empty while idle.
/// </para>
/// <para>
/// After success or failure, <c>Diff.Compute</c> silently resets all base-class state, including the
/// borrowed function-name memory. If a callback throws, no further callbacks run (including cleanup
/// or retries of <see cref="EndHunk" />), and the original exception propagates. Partial callback
/// effects remain; subclasses must manage their own retained state before reuse.
/// </para>
/// <para>
/// Instances may be reused across <c>Diff.Compute</c> calls, but concurrent or recursive use of the
/// same sink is unsupported.
/// </para>
/// </remarks>
public abstract class HunkSinkBase : IHunkSink
{
    private bool _started;
    private int _oldStart;
    private int _oldCount;
    private int _newStart;
    private int _newCount;
    private ReadOnlyMemory<byte> _func;

    /// <summary>
    /// 1-based starting line of the current hunk in the old buffer, matching the number emitted after
    /// <c>@@ -</c> in unified-diff text. When <see cref="OldCount" /> is <c>0</c>, the value identifies
    /// the line before the insertion point and may be <c>0</c> for an insertion at the beginning of the buffer.
    /// </summary>
    public int OldStart => _oldStart;

    /// <summary>Number of old-buffer lines covered by the current hunk (context + deletions).</summary>
    public int OldCount => _oldCount;

    /// <summary>
    /// 1-based starting line of the current hunk in the new buffer, matching the number emitted after
    /// <c>+</c> in unified-diff text. When <see cref="NewCount" /> is <c>0</c>, the value identifies
    /// the line before the deletion point and may be <c>0</c> for a deletion at the beginning of the buffer.
    /// </summary>
    public int NewStart => _newStart;

    /// <summary>Number of new-buffer lines covered by the current hunk (context + additions).</summary>
    public int NewCount => _newCount;

    /// <summary>
    /// The undecoded raw bytes that follow <c>@@ ... @@</c> in unified-diff text for the current hunk.
    /// Empty when function-name emission is disabled or no matching line has been found; a later hunk
    /// without a match reuses the most recently matched name. The memory aliases the old input buffer
    /// (no copy), so it is only valid while that buffer is alive and unmodified.
    /// </summary>
    public ReadOnlyMemory<byte> Func => _func;

    /// <summary>
    /// Called once when a new hunk starts, after the hunk properties (including <see cref="Func" />) have
    /// been updated. The default implementation does nothing. <see cref="EndHunk" /> for the previous
    /// hunk (if any) fires first.
    /// </summary>
    public virtual void BeginHunk() { }

    /// <summary>
    /// Called once when the current hunk is complete (the next hunk begins, or the diff is flushed).
    /// The hunk properties still hold the completed hunk's values during this call. The default
    /// implementation does nothing.
    /// </summary>
    public virtual void EndHunk() { }

    /// <summary>
    /// Called once per line of the current hunk, in hunk order.
    /// </summary>
    /// <param name="kind">Whether the line is context, an addition, or a deletion.</param>
    /// <param name="content">The raw line bytes (including any trailing newline); aliases an input buffer, no copy.</param>
    /// <param name="oldLine">1-based line number in the old buffer, or <c>0</c> for additions.</param>
    /// <param name="newLine">1-based line number in the new buffer, or <c>0</c> for deletions.</param>
    public virtual void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine) { }

    void IHunkSink.HunkHeader(int s1, int c1, int s2, int c2, ReadOnlyMemory<byte> func)
    {
        Flush();

        _oldStart = c1 != 0 ? s1 + 1 : s1;
        _oldCount = c1;
        _newStart = c2 != 0 ? s2 + 1 : s2;
        _newCount = c2;
        _func = func;
        _started = true;

        BeginHunk();
    }

    void IHunkSink.Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine)
        => Line(kind, content, oldLine, newLine);

    internal void Flush()
    {
        if (!_started)
        {
            return;
        }

        try
        {
            EndHunk();
        }
        finally
        {
            ResetState();
        }
    }

    internal void ResetState()
    {
        _oldStart = 0;
        _oldCount = 0;
        _newStart = 0;
        _newCount = 0;
        _func = ReadOnlyMemory<byte>.Empty;
        _started = false;
    }
}
