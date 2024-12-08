using Autodesk.Navisworks.Api;
using System.Collections.Generic;
using System;

namespace AddinRibbon.Services
{
    public class ItemService
    {
        public List<Point3D> CalculateMedianPoints(BoundingBox3D boundingBox)
        {
            List<Point3D> pointsList = new List<Point3D>();

            // Extract min and max points
            Point3D minPoint = boundingBox.Min;
            Point3D maxPoint = boundingBox.Max;

            // Check if it's an arc case
            bool isArc = Math.Abs((maxPoint.X - minPoint.X) - (maxPoint.Y - minPoint.Y)) < 1e-6;

            if (isArc)
            {
                // Arc case

                // Center of the arc
                double centerX = maxPoint.X; // 44.178
                double centerY = minPoint.Y; // -8.125
                double centerZ = (minPoint.Z + maxPoint.Z) / 2; // 8.211

                // Radius calculation using one of the definition points
                double radius = Math.Sqrt(Math.Pow(centerX - maxPoint.X, 2) + Math.Pow(centerY - (minPoint.Y + (maxPoint.Y - minPoint.Y) / 2), 2));

                // Start and end angles in radians
                double startAngle = Math.PI / 2; // 90 degrees
                double endAngle = Math.PI;       // 180 degrees

                // Angle increment for every 15 degrees
                double angleIncrement = Math.PI / 12; // 15 degrees in radians

                // Generate points from 90 degrees to 180 degrees
                for (double angle = startAngle; angle <= endAngle + 1e-6; angle += angleIncrement)
                {
                    double x = centerX + radius * Math.Cos(angle);
                    double y = centerY + radius * Math.Sin(angle);
                    double z = centerZ;

                    pointsList.Add(new Point3D(x, y, z));
                }
            }
            else
            {
                // Straight line case

                // Determine the length axis
                double deltaX = maxPoint.X - minPoint.X;
                double deltaY = maxPoint.Y - minPoint.Y;
                double deltaZ = maxPoint.Z - minPoint.Z;

                string lengthAxis = "X";
                double maxDelta = deltaX;
                if (deltaY > maxDelta)
                {
                    lengthAxis = "Y";
                    maxDelta = deltaY;
                }
                if (deltaZ > maxDelta)
                {
                    lengthAxis = "Z";
                    maxDelta = deltaZ;
                }

                // Definition points
                Point3D startPoint;
                Point3D endPoint;
                double fixedCoord1, fixedCoord2;

                if (lengthAxis == "X")
                {
                    // Fixed Y and Z
                    fixedCoord1 = minPoint.Y + deltaY / 2;
                    fixedCoord2 = minPoint.Z + deltaZ / 2;
                    startPoint = new Point3D(minPoint.X, fixedCoord1, fixedCoord2);
                    endPoint = new Point3D(maxPoint.X, fixedCoord1, fixedCoord2);
                }
                else if (lengthAxis == "Y")
                {
                    // Fixed X and Z
                    fixedCoord1 = minPoint.X + deltaX / 2;
                    fixedCoord2 = minPoint.Z + deltaZ / 2;
                    startPoint = new Point3D(fixedCoord1, minPoint.Y, fixedCoord2);
                    endPoint = new Point3D(fixedCoord1, maxPoint.Y, fixedCoord2);
                }
                else // lengthAxis == "Z"
                {
                    // Fixed X and Y
                    fixedCoord1 = minPoint.X + deltaX / 2;
                    fixedCoord2 = minPoint.Y + deltaY / 2;
                    startPoint = new Point3D(fixedCoord1, fixedCoord2, minPoint.Z);
                    endPoint = new Point3D(fixedCoord1, fixedCoord2, maxPoint.Z);
                }

                // Calculate total distance
                double totalDistance = Math.Sqrt(
                    Math.Pow(endPoint.X - startPoint.X, 2) +
                    Math.Pow(endPoint.Y - startPoint.Y, 2) +
                    Math.Pow(endPoint.Z - startPoint.Z, 2)
                );

                // Interval between points
                double interval = 0.300;

                // Number of points
                int numberOfPoints = (int)(totalDistance / interval) + 1;

                // Generate points
                for (int i = 0; i <= numberOfPoints; i++)
                {
                    double t = (double)i / numberOfPoints;
                    double x = startPoint.X + t * (endPoint.X - startPoint.X);
                    double y = startPoint.Y + t * (endPoint.Y - startPoint.Y);
                    double z = startPoint.Z + t * (endPoint.Z - startPoint.Z);

                    pointsList.Add(new Point3D(x, y, z));
                }
            }

            return pointsList;
        }

        public string AddNodeCode(ModelItem item, List<Point3D> medianPoints)
        {
            var lines = new List<string>
            {
                "Node Code:",
                $"{item.DisplayName}",
                "--------------------",
                $"var {item.DisplayName.Substring(item.DisplayName.Length - 4).ToLower()}nodes = new List<Node>",
                "{"
            };
            foreach (var point in medianPoints)
            {
                lines.Add($"new Node {{ TrayName = \"{item.DisplayName.Substring(item.DisplayName.Length - 4)}\", X = {point.X.ToString("0.000")}, Y = {point.Y.ToString("0.000")}, Z = {point.Z.ToString("0.000")} }},");
            }
            lines.Add("};");
            lines.Add($"trays[\"{item.DisplayName.Substring(item.DisplayName.Length - 4)}\"] = {item.DisplayName.Substring(item.DisplayName.Length - 4).ToLower()}nodes;");
            lines.Add("--------------------");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
