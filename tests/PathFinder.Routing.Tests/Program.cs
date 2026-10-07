using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AddinRibbon.Routing;

internal static class Program
{
    private static int passed;
    private static readonly RouteCalculator Calculator = new RouteCalculator();

    private static int Main(string[] args)
    {
        try
        {
            Run("Full route names and piece suffixes", CodeParsing);
            Run("A continuous polyline and middle projections", LongPolylineProjection);
            Run("Exactly two metres and both secondary markers", SecondaryBoundary);
            Run("Disconnected network does not fabricate a path", Disconnected);
            Run("Exact skew contact between samples at the tolerance boundary", ExactContacts);
            Run("Right-angle route length follows its joint at both tolerances", RightAngleLength);
            Run("Known 3D diagonal polyline length and interior junction", DiagonalAndInteriorJunction);
            Run("Parallel overlapping centreline contacts avoid artificial detours", ParallelOverlap);
            Run("Category filtering rejects a forbidden bridge", Categories);
            Run("Ordered transitions retain a route re-entry", Reentry);
            Run("Reverse swaps attachments and is an involution", Reversal);
            Run("Repeated requests cannot accumulate graph edges", Repeatability);
            Run("Invalid data and a graph size limit are handled", InvalidAndNodeLimit);
            Run("Dense overlapping geometry stops at a connection limit", DenseConnections);
            Run("Cancellation before and during calculation", Cancellation);
            Run("45 x 45 intersecting grid performance", GridPerformance);
            if (args.Length > 0) Run("Excel report tray code fixtures", () => ExcelCodes(args[0]));
            Console.WriteLine("All " + passed + " routing regression checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void Run(string name, Action action)
    {
        var timer = Stopwatch.StartNew();
        action();
        passed++;
        Console.WriteLine("PASS: " + name + " (" + timer.ElapsedMilliseconds + " ms)");
    }

    private static RoutePoint P(double x, double y = 0, double z = 0) { return new RoutePoint(x, y, z); }
    private static TraySegment T(string id, string name, CableCategory categories, params RoutePoint[] points)
    {
        return new TraySegment(id, name, categories, points);
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(double expected, double actual, string message)
    {
        Assert(Math.Abs(expected - actual) < 0.000001, message + " Expected " + expected + ", got " + actual);
    }
    private static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException("Expected " + typeof(TException).Name);
    }

    private static void CodeParsing()
    {
        foreach (string code in new[] { "A007", "B102", "BC001", "C007", "DFB007", "DFBC009", "DFC042", "DFV010", "VFD011", "B102-1", "DFBC001-2" })
        {
            Assert(RouteCodeParser.Parse("Cable Tray /" + code + " [geometry]") == "/" + code, code);
            Assert(RouteCodeParser.Parse("Cable Tray " + code.ToLowerInvariant()) == "/" + code, code + " lowercase");
        }
        Assert(RouteCodeParser.Parse("root /B002 child /BC050-2") == "/BC050-2", "Final route code must win.");
        Assert(RouteCodeParser.Parse("x") == "/X", "Short names must not crash.");
        Assert(RouteCodeParser.Parse(null) == "/UNNAMED", "Missing names must not crash.");
        string named;
        Assert(!RouteCodeParser.TryParseNamedCode("ABC001 screw", out named), "Embedded codes must not override a route ancestor.");
        Assert(RouteCodeParser.TryParseNamedCode("Tray /DFBC007", out named) && named == "/DFBC007",
            "A complete code must remain available without fallback parsing.");
    }

    private static void LongPolylineProjection()
    {
        var tray = T("long", "B102", CableCategory.LV, P(0), P(1000));
        var result = Calculator.Calculate(new[] { tray }, P(400, 1), P(410, 1), CableCategory.LV);
        Assert(result.Success, result.Message);
        Near(1, result.FromDistanceMeters, "Project to the interior, not an end.");
        Near(1, result.ToDistanceMeters, "Project to the interior, not an end.");
        Near(12, result.LengthMeters, "Polyline length must include attachment legs.");
        Assert(result.RouteText == "/B102", "One tray route expected.");
        var bent = T("bend", "BC001", CableCategory.Control, P(0), P(4), P(4, 4));
        result = Calculator.Calculate(new[] { bent }, P(1), P(4, 3), CableCategory.Control);
        Near(6, result.LengthMeters, "The path must follow the bend.");
    }

    private static void SecondaryBoundary()
    {
        var tray = T("one", "BC001", CableCategory.LV | CableCategory.Control, P(0), P(10));
        var exactly = Calculator.Calculate(new[] { tray }, P(2, 2), P(8, 2), CableCategory.LV);
        Assert(exactly.Success && !exactly.FromRequiresSecondary && !exactly.ToRequiresSecondary,
            "Exactly 2 m must not be secondary.");
        Assert(exactly.RouteText == "/BC001", "Boundary marker is incorrect.");
        var beyond = Calculator.Calculate(new[] { tray }, P(2, 2.000001), P(8, -3), CableCategory.LV);
        Assert(beyond.RouteText == "/SECONDARY/BC001/SECONDARY", "Both endpoint markers required.");
        var fromOnly = Calculator.Calculate(new[] { tray }, P(2, 3), P(8, 1), CableCategory.LV);
        Assert(fromOnly.RouteText == "/SECONDARY/BC001", "Only From is secondary.");
    }

    private static void Disconnected()
    {
        var trays = new[] { T("a", "B001", CableCategory.LV, P(0), P(2)),
            T("b", "B002", CableCategory.LV, P(2.251), P(4)) };
        var result = Calculator.Calculate(trays, P(0), P(4), CableCategory.LV);
        Assert(!result.Success && result.RouteCodes.Count == 0, "A gap over tolerance cannot be crossed.");
        var connected = new[] { trays[0], T("b", "B002", CableCategory.LV, P(2.25), P(4)) };
        Assert(Calculator.Calculate(connected, P(0), P(4), CableCategory.LV).Success,
            "A gap exactly at the connection tolerance is eligible.");
        var separatedByHeight = new[] { trays[0], T("b", "B002", CableCategory.LV, P(1, 0, 1), P(4, 0, 1)) };
        Assert(!Calculator.Calculate(separatedByHeight, P(0), P(4, 0, 1), CableCategory.LV).Success,
            "Routing must compare Z as well as X and Y.");
    }

    private static void Categories()
    {
        var trays = new[] { T("a", "A001", CableCategory.MV, P(0), P(2)),
            T("bridge", "B001", CableCategory.LV, P(2), P(4)),
            T("c", "A002", CableCategory.MV, P(4), P(6)) };
        Assert(!Calculator.Calculate(trays, P(0), P(6), CableCategory.MV).Success,
            "MV must never travel through an LV-only bridge.");
        Assert(!Calculator.Calculate(trays, P(0), P(6), CableCategory.Control).Success,
            "An empty eligible set must fail clearly.");
        trays[1] = T("bridge", "BC001", CableCategory.MV | CableCategory.LV, P(2), P(4));
        var result = Calculator.Calculate(trays, P(0), P(6), CableCategory.MV);
        Assert(result.Success && result.RouteText == "/A001/BC001/A002", "Explicit flags govern eligibility.");
    }

    private static void RightAngleLength()
    {
        var trays = new[] { T("a", "B001", CableCategory.LV, P(-10), P(0)),
            T("b", "B002", CableCategory.LV, P(0), P(0, 10)) };
        foreach (double tolerance in new[] { 0.25, 1.30 })
        {
            var result = Calculator.Calculate(trays, P(-10), P(0, 10), CableCategory.LV,
                new RoutingOptions { ConnectionToleranceMeters = tolerance });
            Assert(result.Success, result.Message);
            Near(20, result.LengthMeters, "Nearby samples must not shortcut the right-angle joint.");
            Assert(result.PathPoints.Any(point => point.DistanceTo(P(0)) == 0), "The route must pass through its true joint.");
        }
    }

    private static void DiagonalAndInteriorJunction()
    {
        var trays = new[] { T("a", "B001", CableCategory.LV, P(0), P(3, 3, 3)),
            T("b", "BC001", CableCategory.LV, P(3, 3, 3), P(6, 6, 3)) };
        double expected = 3 * Math.Sqrt(3) + 3 * Math.Sqrt(2);
        var result = Calculator.Calculate(trays, P(0), P(6, 6, 3), CableCategory.LV,
            new RoutingOptions { ConnectionToleranceMeters = 1.30 });
        Assert(result.Success, result.Message);
        Near(expected, result.LengthMeters, "A 3D diagonal must retain all three coordinates and both legs.");
        var branch = new[] { T("a", "B001", CableCategory.LV, P(-5), P(5)),
            T("b", "BC001", CableCategory.LV, P(0), P(0, 5)) };
        result = Calculator.Calculate(branch, P(-4), P(0, 4), CableCategory.LV);
        Assert(result.Success && result.RouteText == "/B001/BC001", "An interior T contact must remain connected.");
        Near(8, result.LengthMeters, "The route must follow the interior junction.");
    }

    private static void ParallelOverlap()
    {
        foreach (bool reverseFirst in new[] { false, true })
        foreach (bool reverseLast in new[] { false, true })
        {
            var first = T("a", "B001", CableCategory.LV,
                reverseFirst ? P(10) : P(0), reverseFirst ? P(0) : P(10));
            var last = T("b", "B002", CableCategory.LV,
                reverseLast ? P(15) : P(5), reverseLast ? P(5) : P(15));
            var result = Calculator.Calculate(new[] { first, last }, P(9), P(14), CableCategory.LV);
            Assert(result.Success, result.Message);
            Near(5, result.LengthMeters, "Overlap must not force a trip to only its starting boundary.");
            var reversed = result.Reverse();
            Near(5, reversed.LengthMeters, "Overlap reversal must preserve length.");
        }
        var diagonal = new[] { T("a", "B001", CableCategory.LV, P(0), P(10, 10, 10)),
            T("b", "B002", CableCategory.LV, P(5, 5, 5), P(15, 15, 15)) };
        var diagonalResult = Calculator.Calculate(diagonal, P(9, 9, 9), P(14, 14, 14), CableCategory.LV);
        Assert(diagonalResult.Success, diagonalResult.Message);
        Near(5 * Math.Sqrt(3), diagonalResult.LengthMeters, "Rotated overlap must retain the true centreline length.");
    }

    private static void ExactContacts()
    {
        // Neither polyline has a regular sample at the true nearest contact.
        // The gap in Z is exactly the tolerance, so sample distances are all too large.
        var horizontal = T("a", "B001", CableCategory.LV, P(-1, 0.0625), P(1, 0.0625));
        var vertical = T("b", "BC001", CableCategory.LV, P(0.0625, -1, 0.25), P(0.0625, 1, 0.25));
        var result = Calculator.Calculate(new[] { horizontal, vertical }, P(-1, 0.0625), P(0.0625, 1, 0.25), CableCategory.LV);
        Assert(result.Success && result.RouteText == "/B001/BC001", "An exact continuous-polyline contact must be found.");
        Assert(result.PathPoints.Any(point => point.DistanceTo(P(0.0625, 0.0625)) == 0),
            "The exact projection contact must be inserted on the first tray.");
        Assert(result.PathPoints.Any(point => point.DistanceTo(P(0.0625, 0.0625, 0.25)) == 0),
            "The exact projection contact must be inserted on the second tray.");
        var outside = T("b", "BC001", CableCategory.LV, P(0.0625, -1, 0.250001), P(0.0625, 1, 0.250001));
        Assert(!Calculator.Calculate(new[] { horizontal, outside }, P(-1, 0.0625), P(0.0625, 1, 0.250001), CableCategory.LV).Success,
            "Exact contacts must not widen the configured gap tolerance.");
    }

    private static void Reentry()
    {
        var trays = new[] { T("a1", "B001", CableCategory.LV, P(0), P(2)),
            T("b", "BC001", CableCategory.LV, P(2), P(4)),
            T("a2", "B001", CableCategory.LV, P(4), P(6)) };
        var result = Calculator.Calculate(trays, P(0), P(6), CableCategory.LV);
        Assert(result.Success && result.RouteText == "/B001/BC001/B001", "Re-entry cannot be globally deduplicated.");
        Assert(result.SegmentIds.SequenceEqual(new[] { "a1", "b", "a2" }), "Highlight order must retain every segment.");
    }

    private static void Reversal()
    {
        var trays = new[] { T("a", "B001", CableCategory.LV, P(0), P(2)),
            T("b", "DFB007-1", CableCategory.LV, P(2), P(4)) };
        var original = Calculator.Calculate(trays, P(0, 3), P(4, 1), CableCategory.LV);
        var reverse = original.Reverse();
        Assert(reverse.RouteText == "/DFB007-1/B001/SECONDARY", "Reverse must swap ordered route markers.");
        Near(original.LengthMeters, reverse.LengthMeters, "Reverse preserves total distance.");
        Near(original.FromDistanceMeters, reverse.ToDistanceMeters, "Reverse swaps attachment distances.");
        Assert(reverse.PathPoints.First().DistanceTo(original.PathPoints.Last()) == 0, "Reverse swaps point order.");
        Assert(reverse.Reverse().RouteText == original.RouteText, "Reverse twice returns original route.");
        Assert(reverse.Reverse().SegmentIds.SequenceEqual(original.SegmentIds), "Reverse twice returns original IDs.");
    }

    private static void Repeatability()
    {
        var trays = new[] { T("a", "B001", CableCategory.LV, P(0), P(3)),
            T("b", "BC002", CableCategory.LV, P(3), P(6)) };
        var baseline = Calculator.Calculate(trays, P(1), P(5), CableCategory.LV);
        for (int i = 0; i < 30; i++)
        {
            var current = Calculator.Calculate(i % 2 == 0 ? trays : trays.Reverse(), P(1), P(5), CableCategory.LV);
            Assert(current.RouteText == baseline.RouteText, "Stable routing independent of input enumeration order.");
            Near(baseline.LengthMeters, current.LengthMeters, "No graph accumulation between requests.");
            Assert(current.PathPoints.Count == baseline.PathPoints.Count, "No accumulated path points.");
        }
    }

    private static void InvalidAndNodeLimit()
    {
        Throws<ArgumentException>(() => P(double.NaN));
        Throws<ArgumentException>(() => T("", "B001", CableCategory.LV, P(0), P(1)));
        Throws<ArgumentException>(() => T("a", "B001", CableCategory.LV, P(0)));
        var tray = T("a", "B001", CableCategory.LV, P(0), P(5));
        Throws<ArgumentException>(() => Calculator.Calculate(new[] { tray }, P(0), P(1), CableCategory.All));
        Throws<ArgumentException>(() => Calculator.Calculate(new[] { tray }, P(0), P(1), CableCategory.LV,
            new RoutingOptions { ConnectionToleranceMeters = 0 }));
        var limited = Calculator.Calculate(new[] { tray }, P(0), P(5), CableCategory.LV,
            new RoutingOptions { MaxGraphNodes = 10 });
        Assert(!limited.Success && limited.Message.Contains("node limit"), "Oversized geometry must stop cleanly.");
        var zero = T("zero", "B001", CableCategory.LV, P(0), P(0));
        var same = Calculator.Calculate(new[] { zero }, P(0), P(0), CableCategory.LV);
        Assert(same.Success && same.RouteText == "/B001" && same.LengthMeters == 0, "A degenerate leaf must be safe.");
    }

    private static void Cancellation()
    {
        var tray = T("a", "B001", CableCategory.LV, P(0), P(5));
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            Throws<OperationCanceledException>(() => Calculator.Calculate(new[] { tray }, P(0), P(1),
                CableCategory.LV, cancellationToken: source.Token));
        }
        using (var source = new CancellationTokenSource())
        {
            var grid = MakeGrid(85);
            source.CancelAfter(5);
            Throws<OperationCanceledException>(() => Calculator.Calculate(grid, P(0), P(84, 84),
                CableCategory.LV, cancellationToken: source.Token));
        }
    }

    private static void DenseConnections()
    {
        var dense = Enumerable.Range(0, 100).Select(index =>
            T("dense" + index, "B001", CableCategory.LV, P(0), P(0))).ToArray();
        var result = Calculator.Calculate(dense, P(0), P(0), CableCategory.LV,
            new RoutingOptions { MaxGraphNodes = 500, MaxGraphConnections = 1000 });
        Assert(!result.Success && result.Message.Contains("connection limit"),
            "A node cap alone cannot prevent quadratic memory use for overlapping trays.");
    }

    private static List<TraySegment> MakeGrid(int size)
    {
        var result = new List<TraySegment>();
        for (int i = 0; i < size; i++)
        {
            result.Add(T("h" + i.ToString("000"), "B" + i.ToString("000"), CableCategory.LV,
                P(0, i), P(size - 1, i)));
            result.Add(T("v" + i.ToString("000"), "BC" + i.ToString("000"), CableCategory.LV,
                P(i), P(i, size - 1)));
        }
        return result;
    }

    private static void GridPerformance()
    {
        var timer = Stopwatch.StartNew();
        var result = Calculator.Calculate(MakeGrid(45), P(0), P(44, 44), CableCategory.LV);
        Assert(result.Success, result.Message);
        Near(88, result.LengthMeters, "An orthogonal grid must follow its centreline junctions without cutting corners.");
        Assert(timer.ElapsedMilliseconds < 30000, "Grid routing exceeded 30 seconds.");
        Console.WriteLine("  90 polylines / approximately 31,770 samples; " + result.LengthMeters.ToString("0.000") + " m path.");
    }

    private static void ExcelCodes(string file)
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(file)))
        {
            int count = 0;
            foreach (var tray in document.RootElement.GetProperty("tray_catalog").EnumerateArray())
            {
                string name = tray.GetProperty("name").GetString();
                Assert(RouteCodeParser.Parse("Cable tray /" + name + " geometry") == "/" + name.ToUpperInvariant(), name);
                count++;
            }
            Assert(count == 327, "Expected all 327 source-report tray names.");
            Console.WriteLine("  Verified " + count + " tray codes against the supplied report fixture.");
        }
    }
}
