using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AddinRibbon.Routing;

internal static class GeometryTests
{
    public static void Run(Action<string, Action> run)
    {
        run("Mesh-validated quarter bends in XY, XZ and YZ, with every turn orientation", PlanesAndTurns);
        run("Quarter bend length follows the arc and retains route categories", ArcLength);
        run("A rotated square elbow with unchanged bounds rejects obsolete ports", RotatedFaceEvidence);
        run("Missing mesh evidence or neighbouring straight arms cannot invent a bend", MissingEvidence);
        run("Ambiguous ports and unequal radii retain the fallback with diagnostics", UnsupportedFits);
        run("A small physical gap remains a gap after bend reconstruction", PhysicalGap);
        run("Only named elbows are fitted; axis fallback and cancellation are preserved", FallbackAndCancellation);
    }

    private static void PlanesAndTurns()
    {
        foreach (var axes in new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 2 } })
            foreach (int firstSign in new[] { -1, 1 })
                foreach (int secondSign in new[] { -1, 1 })
                {
                    var fixture = Create(axes[0], axes[1], firstSign, secondSign);
                    var result = TrayGeometryBuilder.Build(fixture.Inputs);
                    Assert(result.ValidatedBends == 1 && result.FallbackBends == 0, "A supported turn must have one validated bend.");
                    var arc = result.Segments[0].Points;
                    Assert(arc.Count == 19, "The quarter turn must retain all five-degree samples.");
                    Near(0, arc[0].DistanceTo(fixture.Start), 0.000001, "First physical mesh port.");
                    Near(0, arc[arc.Count - 1].DistanceTo(fixture.End), 0.000001, "Last physical mesh port.");
                    foreach (var point in arc)
                        Near(1, point.DistanceTo(fixture.Centre), 0.000001, "Each sample must remain on the quarter circle.");
                    Near(Math.PI * 0.5, Length(arc), 0.001, "Every plane and turn needs the same arc length.");
                }
    }

    private static void ArcLength()
    {
        var fixture = Create(0, 1, 1, -1);
        var built = TrayGeometryBuilder.Build(fixture.Inputs);
        var bend = built.Segments[0];
        Assert(bend.AllowedCategories == (CableCategory.LV | CableCategory.Control), "Geometry must preserve category permissions.");
        Assert(bend.Id == "bend" && bend.RouteCode == "/B002", "Geometry must preserve routing identifiers and codes.");
        double arcLength = Length(bend.Points);
        Assert(arcLength > fixture.Start.DistanceTo(fixture.End) + 0.15, "A straight chord must not replace the physical quarter arc.");
        var route = new RouteCalculator().Calculate(built.Segments, fixture.From, fixture.To, CableCategory.LV,
            new RoutingOptions { ConnectionToleranceMeters = 0.00001 });
        Assert(route.Success, route.Message);
        Near(4 + Math.PI * 0.5, route.LengthMeters, 0.001, "Routing must retain both straight arms and the bend length.");
        Assert(route.RouteText == "/B001/B002/B003", "The curve must connect the ordered route names.");
    }

    private static void RotatedFaceEvidence()
    {
        var fixture = Create(0, 1, 1, 1);
        var original = fixture.Inputs[0];
        var rotated = RotateFaces(original, 0, 1);
        var replacement = Input(original.Id, original.RouteName, original.ShapeName, original.Minimum, original.Maximum, rotated);
        var built = TrayGeometryBuilder.Build(new[] { replacement, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(built.ValidatedBends == 0 && built.FallbackBends == 1, "Unchanged box bounds must not conceal changed physical openings.");
        Assert(built.Segments[0].Points.Count == 2, "Unsupported evidence must retain the fallback.");
        var shifted = original.FaceExtents.Select(face => new TrayFaceExtent(face.Face,
            Shift(face.Minimum, (int)face.Face / 2 == 0 ? 1 : 0, -0.1),
            Shift(face.Maximum, (int)face.Face / 2 == 0 ? 1 : 0, -0.1), face.SampleCount));
        replacement = Input(original.Id, original.RouteName, original.ShapeName, original.Minimum, original.Maximum, shifted);
        built = TrayGeometryBuilder.Build(new[] { replacement, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(built.ValidatedBends == 0, "A neighbouring endpoint must match the occupied face centre.");
    }

    private static void MissingEvidence()
    {
        var fixture = Create(0, 2, 1, -1);
        var bend = fixture.Inputs[0];
        var missing = Input(bend.Id, bend.RouteName, bend.ShapeName, bend.Minimum, bend.Maximum);
        var result = TrayGeometryBuilder.Build(new[] { missing, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0 && result.Diagnostics[0].Contains("unavailable"), "No mesh evidence must be reported.");
        result = TrayGeometryBuilder.Build(new[] { bend });
        Assert(result.ValidatedBends == 0, "Mesh evidence alone cannot infer physical connectivity.");
        result = TrayGeometryBuilder.Build(new[] { bend, fixture.Inputs[1] });
        Assert(result.ValidatedBends == 0, "One arm is insufficient.");
        var undersampled = bend.FaceExtents.Select(f => new TrayFaceExtent(f.Face, f.Minimum, f.Maximum, 2));
        var sparse = Input(bend.Id, bend.RouteName, bend.ShapeName, bend.Minimum, bend.Maximum, undersampled);
        result = TrayGeometryBuilder.Build(new[] { sparse, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0, "An isolated face point is insufficient to identify a port.");
        var tangentBands = bend.FaceExtents.Select(f =>
        {
            int perpendicular = (int)f.Face / 2 == 0 ? 2 : 0;
            return new TrayFaceExtent(f.Face, Shift(f.Minimum, perpendicular, 0.18), Shift(f.Maximum, perpendicular, -0.18), 30);
        });
        var narrow = Input(bend.Id, bend.RouteName, bend.ShapeName, bend.Minimum, bend.Maximum, tangentBands);
        result = TrayGeometryBuilder.Build(new[] { narrow, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0, "A narrow vertex band must match the neighbouring arm cross-section to count as a port.");
    }

    private static void UnsupportedFits()
    {
        var fixture = Create(0, 1, 1, 1);
        var bend = fixture.Inputs[0];
        var rotated = RotateFaces(bend, 0, 1).ToArray();
        var allFaces = Input(bend.Id, bend.RouteName, bend.ShapeName, bend.Minimum, bend.Maximum, bend.FaceExtents.Concat(rotated));
        var extraArms = rotated.Select((face, index) =>
        {
            int axis = (int)face.Face / 2;
            var centre = Midpoint(face.Minimum, face.Maximum);
            var outside = Shift(centre, axis, (int)face.Face % 2 == 0 ? -2 : 2);
            return Straight("extra" + index, "/B004", centre, outside, axis, 2);
        });
        var result = TrayGeometryBuilder.Build(new[] { allFaces, fixture.Inputs[1], fixture.Inputs[2] }.Concat(extraArms));
        Assert(result.ValidatedBends == 0 && result.Diagnostics[0].Contains("ambiguous"), "Multiple possible turns must be rejected.");
        var changedFace = bend.FaceExtents.Select(f => (int)f.Face / 2 == 1
            ? new TrayFaceExtent(f.Face, Shift(f.Minimum, 0, -0.05), Shift(f.Maximum, 0, -0.05), f.SampleCount) : f).ToArray();
        var unequal = Input(bend.Id, bend.RouteName, bend.ShapeName, bend.Minimum, bend.Maximum, changedFace);
        var movedEnd = Shift(fixture.End, 0, -0.05);
        var movedArm = Straight("out", "/B003", movedEnd, Shift(movedEnd, 1, -2), 1, 2);
        result = TrayGeometryBuilder.Build(new[] { unequal, fixture.Inputs[1], movedArm });
        Assert(result.ValidatedBends == 0, "Unequal radial offsets cannot be called a circular bend.");
        var thick = Input("bend", "/B002", "ELBOW", Shift(bend.Minimum, 2, -1), Shift(bend.Maximum, 2, 1), bend.FaceExtents);
        result = TrayGeometryBuilder.Build(new[] { thick, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0, "A non-planar shape must not be fitted.");
    }

    private static void PhysicalGap()
    {
        var fixture = Create(0, 1, 1, 1);
        double gap = 0.015;
        var gapStart = Shift(fixture.Start, 0, -gap);
        var gapped = Straight("in", "/B001", gapStart, fixture.From, 0, 2);
        var result = TrayGeometryBuilder.Build(new[] { fixture.Inputs[0], gapped, fixture.Inputs[2] });
        Assert(result.ValidatedBends == 1, "A near neighbour can validate a physical mesh port.");
        Near(gap, result.Segments[0].Points[0].DistanceTo(gapStart), 0.000001, "The curve must retain its own physical endpoint.");
        var route = new RouteCalculator().Calculate(result.Segments, fixture.From, fixture.To, CableCategory.LV,
            new RoutingOptions { ConnectionToleranceMeters = 0.005 });
        Assert(!route.Success, "Geometry reconstruction must not bypass the configured strict gap limit.");
        var absent = Straight("in", "/B001", Shift(fixture.Start, 0, -0.03), fixture.From, 0, 2);
        result = TrayGeometryBuilder.Build(new[] { fixture.Inputs[0], absent, fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0, "An endpoint outside the tight fitting tolerance cannot validate an arm.");
    }

    private static void FallbackAndCancellation()
    {
        var fixture = Create(1, 2, -1, -1);
        var bend = fixture.Inputs[0];
        var unnamed = Input(bend.Id, bend.RouteName, "unidentified geometry", bend.Minimum, bend.Maximum, bend.FaceExtents);
        var result = TrayGeometryBuilder.Build(new[] { unnamed, fixture.Inputs[1], fixture.Inputs[2] });
        Assert(result.ValidatedBends == 0 && result.FallbackBends == 0, "Only positively named bends may be fitted.");
        Assert(result.Segments[0].Points.Count == 2, "The existing straight fallback must remain available.");
        Assert(!TrayGeometryBuilder.IsBendOrElbow("BENDING support"), "A substring must not classify a bend.");
        Assert(TrayGeometryBuilder.IsBendOrElbow("ELBOW 1") && TrayGeometryBuilder.IsBendOrElbow("BEND 2"), "Known shape tokens must be accepted.");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { TrayGeometryBuilder.Build(fixture.Inputs, cancellation.Token); }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Cancelled geometry work must stop.");
        }
    }

    private sealed class Fixture
    {
        public TrayGeometryInput[] Inputs;
        public RoutePoint Centre, Start, End, From, To;
    }
    private static Fixture Create(int first, int second, int firstSign, int secondSign)
    {
        int normal = 3 - first - second;
        var centre = new RoutePoint(10, 20, 30);
        var start = Shift(centre, second, secondSign);
        var end = Shift(centre, first, firstSign);
        var from = Shift(start, first, -2 * firstSign);
        var to = Shift(end, second, -2 * secondSign);
        var minimum = centre; var maximum = centre;
        minimum = Shift(minimum, first, firstSign < 0 ? -1.2 : 0);
        maximum = Shift(maximum, first, firstSign > 0 ? 1.2 : 0);
        minimum = Shift(minimum, second, secondSign < 0 ? -1.2 : 0);
        maximum = Shift(maximum, second, secondSign > 0 ? 1.2 : 0);
        minimum = Shift(minimum, normal, -0.05); maximum = Shift(maximum, normal, 0.05);
        var faces = new[] { Face(first, firstSign < 0, start, second, normal), Face(second, secondSign < 0, end, first, normal) };
        return new Fixture
        {
            Centre = centre, Start = start, End = end, From = from, To = to,
            Inputs = new[] { Input("bend", "/B002", "BEND 1", minimum, maximum, faces),
                Straight("in", "/B001", start, from, first, normal), Straight("out", "/B003", end, to, second, normal) }
        };
    }
    private static TrayFaceExtent Face(int axis, bool maximum, RoutePoint centre, int perpendicular, int normal)
    { return new TrayFaceExtent((TrayBoxFace)(axis * 2 + (maximum ? 1 : 0)), Shift(Shift(centre, perpendicular, -0.2), normal, -0.05), Shift(Shift(centre, perpendicular, 0.2), normal, 0.05), 12); }
    private static TrayGeometryInput Straight(string id, string name, RoutePoint first, RoutePoint last, int axis, int normal)
    {
        int perpendicular = 3 - axis - normal;
        var minimum = new RoutePoint(Math.Min(first.X, last.X), Math.Min(first.Y, last.Y), Math.Min(first.Z, last.Z));
        var maximum = new RoutePoint(Math.Max(first.X, last.X), Math.Max(first.Y, last.Y), Math.Max(first.Z, last.Z));
        minimum = Shift(Shift(minimum, perpendicular, -0.2), normal, -0.05);
        maximum = Shift(Shift(maximum, perpendicular, 0.2), normal, 0.05);
        return Input(id, name, "FTUBE 1", minimum, maximum);
    }
    private static TrayGeometryInput Input(string id, string name, string shape, RoutePoint min, RoutePoint max, IEnumerable<TrayFaceExtent> faces = null)
    { return new TrayGeometryInput(id, name, CableCategory.LV | CableCategory.Control, shape, min, max, faces); }
    private static IEnumerable<TrayFaceExtent> RotateFaces(TrayGeometryInput bend, int first, int second)
    {
        Func<RoutePoint, RoutePoint> rotate = p => Shift(Shift(p, first, Value(bend.Minimum, first) + Value(bend.Maximum, first) - 2 * Value(p, first)), second,
            Value(bend.Minimum, second) + Value(bend.Maximum, second) - 2 * Value(p, second));
        foreach (var face in bend.FaceExtents)
        {
            var a = rotate(face.Minimum); var b = rotate(face.Maximum);
            yield return new TrayFaceExtent((TrayBoxFace)((int)face.Face ^ 1),
                new RoutePoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
                new RoutePoint(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)), face.SampleCount);
        }
    }
    private static RoutePoint Midpoint(RoutePoint first, RoutePoint last) { return new RoutePoint((first.X + last.X) * 0.5, (first.Y + last.Y) * 0.5, (first.Z + last.Z) * 0.5); }
    private static double Value(RoutePoint point, int axis) { return axis == 0 ? point.X : axis == 1 ? point.Y : point.Z; }
    private static RoutePoint Shift(RoutePoint point, int axis, double amount) { return new RoutePoint(point.X + (axis == 0 ? amount : 0), point.Y + (axis == 1 ? amount : 0), point.Z + (axis == 2 ? amount : 0)); }
    private static double Length(IReadOnlyList<RoutePoint> points) { double sum = 0; for (int i = 1; i < points.Count; i++) sum += points[i - 1].DistanceTo(points[i]); return sum; }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(double expected, double actual, double tolerance, string message) { Assert(Math.Abs(expected - actual) < tolerance, message + " Expected " + expected + ", got " + actual); }
}
