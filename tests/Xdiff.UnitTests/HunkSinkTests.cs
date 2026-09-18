using System.Runtime.InteropServices;
using System.Text;

using Xdiff.Emit;

namespace Xdiff.UnitTests;

public class HunkSinkTests
{
    [Fact]
    public void Compute_Sink_ReceivesBeginLineEndInOrder()
    {
        var sink = new RecordingSink();

        Diff.Compute(sink, "a\nb\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray());

        string[] expectedSequence = ["begin", "line", "line", "line", "end"];
        (int, int, int, int, string)[] expectedHunks = [(1, 2, 1, 3, "")];
        (DiffLineKind, int, int, string)[] expectedLines =
        [
            (DiffLineKind.Context, 1, 1, "a\n"),
            (DiffLineKind.Context, 2, 2, "b\n"),
            (DiffLineKind.Addition, 0, 3, "c\n"),
        ];

        Assert.Equal(expectedSequence, sink.Sequence);
        Assert.Equal(expectedHunks, sink.Begins);
        Assert.Equal(expectedHunks, sink.Ends);
        Assert.Equal(expectedLines, sink.Lines);
    }

    [Fact]
    public void Compute_Sink_TwoHunks_EndHunkFiresBeforeNextBeginHunk()
    {
        var sink = new RecordingSink();

        Diff.Compute(
            sink,
            "1\n2\n3\n4\n5\n6\n7\n8\n9\n"u8.ToArray(),
            "A\n2\n3\n4\n5\n6\n7\n8\nB\n"u8.ToArray());

        string[] expectedSequence =
        [
            "begin", "line", "line", "line", "line", "line", "end",
            "begin", "line", "line", "line", "line", "line", "end",
        ];

        Assert.Equal(expectedSequence, sink.Sequence);

        // EndHunk fires when the next hunk begins (and once more on the final
        // flush) while the properties still hold the completed hunk's values.
        (int, int, int, int, string)[] expectedHunks = [(1, 4, 1, 4, ""), (6, 4, 6, 4, "")];
        Assert.Equal(expectedHunks, sink.Begins);
        Assert.Equal(expectedHunks, sink.Ends);
    }

    [Fact]
    public void Compute_Sink_IdenticalInputs_FiresNoCallbacks()
    {
        var sink = new RecordingSink();

        Diff.Compute(sink, "same\n"u8.ToArray(), "same\n"u8.ToArray());

        Assert.Empty(sink.Sequence);
    }

    [Fact]
    public void Compute_Sink_NullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Diff.Compute((HunkSinkBase)null!, "a\n"u8.ToArray(), "b\n"u8.ToArray()));
    }

    [Fact]
    public void HunkSinkBase_Properties_AreZeroWhenIdle_AndAfterFlush()
    {
        var sink = new RecordingSink();

        AssertIdle(sink);

        Diff.Compute(sink, "a\nb\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray());

        AssertIdle(sink);
    }

    [Fact]
    public void HunkSinkBase_ReusedAcrossComputes_BehavesIdentically()
    {
        var sink = new RecordingSink();
        byte[] oldData = "a\nb\n"u8.ToArray();
        byte[] newData = "a\nb\nc\n"u8.ToArray();

        Diff.Compute(sink, oldData, newData);
        string[] firstSequence = [.. sink.Sequence];
        (DiffLineKind, int, int, string)[] firstLines = [.. sink.Lines];

        sink.Reset();
        Diff.Compute(sink, oldData, newData);

        Assert.Equal(firstSequence, sink.Sequence);
        Assert.Equal(firstLines, sink.Lines);
    }

    [Fact]
    public void Compute_Sink_LineContentBorrowsFromCallerBuffer()
    {
        byte[] oldData = "a\n"u8.ToArray();
        byte[] newData = "a\nb\n"u8.ToArray();

        var sink = new RecordingSink();
        Diff.Compute(sink, oldData, newData);

        Assert.Equal(2, sink.LineContents.Count);
        Assert.True(MemoryMarshal.TryGetArray(sink.LineContents[1], out ArraySegment<byte> segment));
        Assert.True(ReferenceEquals(segment.Array, newData));
    }

    [Fact]
    public void Compute_Sink_FuncNameCarriesAcrossHunks()
    {
        // Only the first hunk's backward search finds a function line; the
        // second hunk (whose search space holds only indented lines) reuses the
        // most recently matched name, per HunkSinkBase.Func.
        string oldText =
            "def foo():\n" +
            "    a = 1\n" +
            "    b = 2\n" +
            "    c = 3\n" +
            "    x = 1\n" +
            "    d = 4\n" +
            "    e = 5\n" +
            "    f = 6\n" +
            "    g = 7\n" +
            "    h = 8\n" +
            "    i = 9\n" +
            "    j = 10\n" +
            "    y = 1\n";
        string newText = oldText.Replace("x = 1", "x = 2").Replace("y = 1", "y = 2");

        var sink = new RecordingSink();
        Diff.Compute(
            sink,
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            new DiffOptions { IncludeFunctionNames = true });

        Assert.Equal(2, sink.Funcs.Count);
        Assert.Equal("def foo():", sink.Funcs[0]);
        Assert.Equal("def foo():", sink.Funcs[1]);
    }

    [Fact]
    public void Compute_Sink_FunctionNameExtractor_SliceDisagreement_FallsBackToWholeLine()
    {
        // The match phase and the slice phase call the extractor separately, so
        // a stateful extractor can report IsMatch = true for the match and
        // false when slicing. SliceFuncName then falls through to the default
        // whole-line (trimmed) behavior instead of slicing the bogus range.
        string oldText = "# def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n";
        string newText = "# def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n";

        int calls = 0;
        (bool IsMatch, Range NameRange) Extractor(ReadOnlySpan<byte> line)
        {
            calls++;
            return calls == 1 ? (true, 0..line.Length) : (false, default);
        }

        var sink = new RecordingSink();
        Diff.Compute(
            sink,
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            new DiffOptions { IncludeFunctionNames = true, FunctionNameExtractor = Extractor });

        Assert.Equal("# def foo():", Assert.Single(sink.Funcs));
    }

    [Theory]
    [InlineData("begin", false)]
    [InlineData("line", false)]
    [InlineData("end", true)]
    [InlineData("end", false)]
    public void Compute_Sink_CallbackFailure_ResetsStateAndAllowsReuse(string failingCallback, bool twoHunks)
    {
        byte[] oldData = "def foo():\n    a = 1\n    unchanged\n    b = 1\n"u8.ToArray();
        byte[] newData = Encoding.UTF8.GetBytes(twoHunks
            ? "def foo():\n    a = 2\n    unchanged\n    b = 2\n"
            : "def foo():\n    a = 2\n    unchanged\n    b = 1\n");
        var options = new DiffOptions { ContextLines = 0, IncludeFunctionNames = true };
        var failure = new InvalidOperationException("Callback failed.");
        var sink = new RecordingSink();
        sink.OnCallback = callback =>
        {
            // The first hunk's properties, including the borrowed annotation,
            // remain available throughout every callback, even EndHunk.
            Assert.Equal(2, sink.OldStart);
            Assert.Equal(1, sink.OldCount);
            Assert.Equal(2, sink.NewStart);
            Assert.Equal(1, sink.NewCount);
            Assert.Equal("def foo():", Encoding.UTF8.GetString(sink.Func.Span));
            Assert.True(MemoryMarshal.TryGetArray(sink.Func, out ArraySegment<byte> segment));
            Assert.Same(oldData, segment.Array);
            if (callback == failingCallback)
            {
                throw failure;
            }
        };

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => Diff.Compute(sink, oldData, newData, options));

        Assert.Same(failure, thrown);
        string[] expectedFailureSequence = failingCallback switch
        {
            "begin" => ["begin"],
            "line" => ["begin", "line"],
            _ => ["begin", "line", "line", "end"],
        };
        Assert.Equal(expectedFailureSequence, sink.Sequence);
        AssertIdle(sink);

        // Only subclass-owned recordings and failure injection need clearing.
        sink.OnCallback = null;
        sink.Reset();
        Diff.Compute(sink, oldData, oldData, options);
        Assert.Empty(sink.Sequence);
        AssertIdle(sink);

        Diff.Compute(sink, oldData, newData, options);
        string[] expectedSuccessSequence = twoHunks
            ? ["begin", "line", "line", "end", "begin", "line", "line", "end"]
            : ["begin", "line", "line", "end"];
        Assert.Equal(expectedSuccessSequence, sink.Sequence);
        Assert.Equal(sink.Begins, sink.Ends);
        AssertIdle(sink);
    }

    private static void AssertIdle(HunkSinkBase sink)
    {
        Assert.Equal(0, sink.OldStart);
        Assert.Equal(0, sink.OldCount);
        Assert.Equal(0, sink.NewStart);
        Assert.Equal(0, sink.NewCount);
        Assert.Equal(ReadOnlyMemory<byte>.Empty, sink.Func);
    }

    private sealed class RecordingSink : HunkSinkBase
    {
        public Action<string>? OnCallback { get; set; }

        public List<string> Sequence { get; } = [];

        public List<(int OldStart, int OldCount, int NewStart, int NewCount, string Func)> Begins { get; } = [];

        public List<(DiffLineKind Kind, int OldLine, int NewLine, string Content)> Lines { get; } = [];

        public List<(int OldStart, int OldCount, int NewStart, int NewCount, string Func)> Ends { get; } = [];

        public List<string> Funcs { get; } = [];

        public List<ReadOnlyMemory<byte>> LineContents { get; } = [];

        public override void BeginHunk()
        {
            Sequence.Add("begin");
            Begins.Add((OldStart, OldCount, NewStart, NewCount, FuncText()));
            Funcs.Add(FuncText());
            OnCallback?.Invoke("begin");
        }

        public override void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine)
        {
            Sequence.Add("line");
            Lines.Add((kind, oldLine, newLine, Encoding.UTF8.GetString(content.Span)));
            LineContents.Add(content);
            OnCallback?.Invoke("line");
        }

        public override void EndHunk()
        {
            Sequence.Add("end");
            Ends.Add((OldStart, OldCount, NewStart, NewCount, FuncText()));
            OnCallback?.Invoke("end");
        }

        public void Reset()
        {
            Sequence.Clear();
            Begins.Clear();
            Lines.Clear();
            Ends.Clear();
            Funcs.Clear();
            LineContents.Clear();
        }

        private string FuncText()
            => Encoding.UTF8.GetString(Func.Span);
    }
}
