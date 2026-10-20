using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// One queue of registered tests served by worker threads, longest estimate
// first: the slow tests start at once and the short ones fill the other
// workers around them. Each test's console output is buffered and printed as
// one block when it ends; long tests stream theirs so a run shows progress.
internal static partial class Program
{
    private sealed class TestCase
    {
        public string Name;
        public Action Body;
        // Measured wall time in seconds; 0 for tests under ten seconds.
        public int EstimateSeconds;
        public long Ms;
        public bool Passed;
        public bool Skipped;
        public string SkipReason;
    }

    private static readonly List<TestCase> registered = new List<TestCase>();
    private static readonly AsyncLocal<StringBuilder> testOutput = new AsyncLocal<StringBuilder>();
    private static TextWriter console;

    // Tests from this estimate up stream their output instead of buffering it.
    private const int StreamSeconds = 60;

    // Registers a test; Execute runs it. The estimate orders the queue; the
    // run's closing "slowest" line gives the numbers to keep it current.
    private static void Run(string name, Action test, int estimateSeconds = 0)
    {
        if (only != null && name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) return;
        registered.Add(new TestCase { Name = name, Body = test, EstimateSeconds = estimateSeconds });
    }

    private static void Skip(string name, string reason)
    {
        if (only != null && name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) return;
        registered.Add(new TestCase { Name = name, Skipped = true, SkipReason = reason });
    }

    private static int SkippedCount => registered.Count(t => t.Skipped);

    private static void Execute(int workers)
    {
        console = Console.Out;
        Console.SetOut(new RoutedWriter());
        // OrderByDescending is stable: equal estimates keep declaration order.
        List<TestCase> runnable = registered.Where(t => !t.Skipped).OrderByDescending(t => t.EstimateSeconds).ToList();
        List<TestCase> skipped = registered.Where(t => t.Skipped).ToList();
        var queue = new ConcurrentQueue<TestCase>(runnable);
        int total = runnable.Count, finished = 0;
        console.WriteLine("tests: " + total + " to run, " + skipped.Count + " skipped on " + workers +
            " workers, estimated " + runnable.Sum(t => t.EstimateSeconds) + "s of work, longest " +
            runnable.Select(t => t.EstimateSeconds).DefaultIfEmpty(0).Max() + "s");
        foreach (TestCase test in skipped)
            console.WriteLine("SKIP " + test.Name + ": " + test.SkipReason);
        Thread[] threads = Enumerable.Range(0, Math.Max(1, Math.Min(workers, total))).Select(_ => new Thread(() =>
        {
            while (queue.TryDequeue(out TestCase test))
            {
                StringBuilder buffer = test.EstimateSeconds >= StreamSeconds ? null : new StringBuilder();
                testOutput.Value = buffer;
                Stopwatch watch = Stopwatch.StartNew();
                string result;
                try
                {
                    test.Body();
                    test.Passed = true;
                    result = "PASS " + test.Name + " (" + watch.ElapsedMilliseconds + " ms)";
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failures);
                    result = "FAIL " + test.Name + ": " + ex.Message + "\n" + ex.StackTrace?.Split('\n').FirstOrDefault();
                }
                test.Ms = watch.ElapsedMilliseconds;
                testOutput.Value = null;
                int done = Interlocked.Increment(ref finished);
                lock (console) console.Write((buffer?.ToString() ?? string.Empty) + result + " [" + done + "/" + total + "]\n");
            }
        }, 16 * 1024 * 1024)).ToArray();
        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();
        Console.SetOut(console);
        console.WriteLine("results: passed=" + runnable.Count(t => t.Passed) + " skipped=" + skipped.Count +
            " failed=" + failures);
        console.WriteLine("slowest: " + string.Join(", ", runnable.OrderByDescending(t => t.Ms).Take(8)
            .Select(t => t.Name + "=" + t.Ms / 1000 + "s (estimate " + t.EstimateSeconds + "s)")));
    }

    // Routes a test's writes to its buffer; streamed and runner writes go
    // straight to the console, one line at a time.
    private sealed class RoutedWriter : TextWriter
    {
        public override Encoding Encoding => console.Encoding;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(char[] chars, int index, int count) => Write(new string(chars, index, count));

        public override void Write(string value)
        {
            StringBuilder buffer = testOutput.Value;
            if (buffer != null) buffer.Append(value);
            else lock (console) console.Write(value);
        }

        public override void WriteLine(string value) => Write(value + "\n");
    }
}
