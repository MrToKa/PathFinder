using AddinRibbon.Shapes;
using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;

namespace AddinRibbon.Services
{
    public class ShapeService
    {
        public void AssignShapesTypes(Dictionary<int, Shape> branches)
        {
            foreach (var branch in branches)
            {
                string displayName = branch.Value.ModelItem.DisplayName;
                branch.Value.ShapeType = displayName.Contains("FTUBE") ? ShapeType.FTUBE :
                                         displayName.Contains("BEND") ? ShapeType.BEND :
                                         displayName.Contains("TEE") ? ShapeType.TEE :
                                         displayName.Contains("ELBOW") ? ShapeType.ELBOW : (ShapeType?)null;
            }
        }

        public void AssignShapesDirections(Dictionary<int, Shape> branches)
        {
            if (branches.Count == 0)
                return;

            if (branches.Count == 1)
            {
                //Check the shape type first!! Then assign the direction
                branches[1].Direction = SingleBranchDirection(branches[1]);
                return;
            }

            for (int i = 0; i < branches.Count; i++)
            {
                if (i == 0)
                {
                    var branchesToCompare = new Dictionary<int, Shape> { { 1, branches[i + 1] }, { 2, branches[i + 2] } };

                    branches[i + 1].Direction = FirstBranchDirection(branchesToCompare);
                }
                else //if (i < branches.Count - 1)
                {
                    var branchesToCompare = new Dictionary<int, Shape> { { 1, branches[i + 1] }, { 2, branches[i] } };
                    branches[i + 1].Direction = BranchDirection(branchesToCompare);
                }
            }

        }

        private Direction BranchDirection(Dictionary<int, Shape> branches)
        {
            var targetBranch = branches[1];
            var previousBranch = branches[2];

            var targetBranchBox = targetBranch.ModelItem.BoundingBox();
            var previousBranchBox = previousBranch.ModelItem.BoundingBox();

            if (previousBranch.ShapeType == ShapeType.FTUBE)
            {
                if (targetBranch.ShapeType == ShapeType.FTUBE)
                {
                    return previousBranch.Direction.Value;
                }
                else if (targetBranch.ShapeType == ShapeType.BEND)
                {
                    var vector = targetBranchBox.Center - previousBranchBox.Center;
                    return GetDirectionForBend(vector, previousBranch.Direction.Value);
                }
                else if (targetBranch.ShapeType == ShapeType.ELBOW)
                {
                    return previousBranch.Direction.Value;
                }

            }

            if (previousBranch.ShapeType == ShapeType.BEND)
            {
                if (targetBranch.ShapeType == ShapeType.FTUBE)
                {
                    var vector = targetBranchBox.Center - previousBranchBox.Center;
                    return GetDirection(vector);
                }
            }

            if (previousBranch.ShapeType == ShapeType.ELBOW)
            {
                if (targetBranch.ShapeType == ShapeType.FTUBE)
                {
                    return previousBranch.Direction.Value;
                }
            }

                return Direction.Unknown;

        }

        private Direction FirstBranchDirection(Dictionary<int, Shape> branches)
        {
            var firstBranch = branches[1];
            var secondBranch = branches[2];

            var firstBranchBox = firstBranch.ModelItem.BoundingBox();
            var secondBranchBox = secondBranch.ModelItem.BoundingBox();

            List<Point3D> firstBranchBoxPoints = GetBBoxEdgeCoordinates(firstBranchBox);
            List<Point3D> secondBranchBoxPoints = GetBBoxEdgeCoordinates(secondBranchBox);

            if (firstBranch.ShapeType == ShapeType.FTUBE)
            {
                List<Point3D> commonPoints = FindCommonPoints(firstBranchBoxPoints, secondBranchBoxPoints);

                if (commonPoints.Count == 4)
                {
                    return DetermineDirection(firstBranchBox.Center, secondBranchBox.Center, Direction.West, Direction.East, Direction.South, Direction.North, Direction.Down, Direction.Up);
                }

                if (commonPoints.Count == 2)
                {
                    var vector = firstBranchBox.Center - secondBranchBox.Center;
                    return GetDirection(vector);
                }

                return Direction.Unknown;
            }
            else if (firstBranch.ShapeType == ShapeType.BEND)
            {
                List<Point3D> commonPoints = FindCommonPoints(firstBranchBoxPoints, secondBranchBoxPoints);

                if (commonPoints.Count == 2)
                {
                    var vector = firstBranchBox.Center - secondBranchBox.Center;
                    return GetDirectionForBend(vector);
                }
            }

            return Direction.Unknown;
        }

        private Direction DetermineDirection(Point3D firstCenter, Point3D secondCenter, Direction xPositive, Direction xNegative, Direction yPositive, Direction yNegative, Direction zPositive, Direction zNegative)
        {
            return Math.Round(firstCenter.X, 3) > Math.Round(secondCenter.X, 3) ? xPositive :
                   Math.Round(firstCenter.X, 3) < Math.Round(secondCenter.X, 3) ? xNegative :
                   Math.Round(firstCenter.Y, 3) > Math.Round(secondCenter.Y, 3) ? yPositive :
                   Math.Round(firstCenter.Y, 3) < Math.Round(secondCenter.Y, 3) ? yNegative :
                   Math.Round(firstCenter.Z, 3) > Math.Round(secondCenter.Z, 3) ? zPositive : zNegative;
        }

        private Direction DetermineDirection(Vector3D vector, double primary, double secondary, Direction positivePrimaryDirection, Direction negativePrimaryDirection, Direction positiveSecondaryDirection, Direction negativeSecondaryDirection)
        {
            //if (primary > 0)
            if (Math.Abs(primary) > Math.Abs(secondary))
            {
                return secondary > 0 ? positivePrimaryDirection : negativePrimaryDirection;
            }

            return secondary > 0 ? positiveSecondaryDirection : negativeSecondaryDirection;
        }

        private Direction DetermineDirection(Direction primary, Vector3D vector)
        {
            if (Math.Abs(vector.X) > Math.Abs(vector.Y))
            {
                if (vector.Y > 0)
                    return (Direction)Enum.Parse(typeof(Direction), primary.ToString() + "North");
                else
                    return (Direction)Enum.Parse(typeof(Direction), primary.ToString() + "South");
            }
            else if (vector.Y > 0)
                return (Direction)Enum.Parse(typeof(Direction), primary.ToString() + "West");
            else
                return (Direction)Enum.Parse(typeof(Direction), primary.ToString() + "East");

        }

        private Direction GetDirection(Vector3D vector)
        {
            if (Math.Abs(vector.Z) == 0)
            {
                return DetermineDirection(vector, vector.X, vector.Y, Direction.West, Direction.East, Direction.North, Direction.South);
            }
            if (Math.Abs(vector.Y) == 0)
            {
                return DetermineDirection(vector, vector.X, vector.Z, Direction.East, Direction.West, Direction.Up, Direction.Down);
            }
            if (Math.Abs(vector.X) == 0)
            {
                return DetermineDirection(vector, vector.Y, vector.Z, Direction.North, Direction.South, Direction.Up, Direction.Down);
            }
            return Direction.Unknown;
        }

        private Direction GetDirectionForBend(Vector3D vector)
        {
            if (Math.Abs(vector.Z) == 0)
            {
                return DetermineDirection(vector, vector.X, vector.Y, Direction.EastNorth, Direction.SouthEast, Direction.WestNorth, Direction.SouthWest);
            }
            if (Math.Abs(vector.Y) == 0)
            {
                return DetermineDirection(vector, vector.X, vector.Z, Direction.UpEast, Direction.DownEast, Direction.UpWest, Direction.DownWest);
            }
            if (Math.Abs(vector.X) == 0)
            {
                return DetermineDirection(vector, vector.Y, vector.Z, Direction.UpNorth, Direction.DownNorth, Direction.DownWest, Direction.DownSouth);
            }
            return Direction.Unknown;
        }

        private Direction GetDirectionForBend(Vector3D vector, Direction direction)
        {
            if (Math.Abs(vector.Z) == 0)
            {
                return DetermineDirection(direction, vector);
            }
            if (Math.Abs(vector.Y) == 0)
            {
                return DetermineDirection(vector, vector.X, vector.Z, Direction.UpEast, Direction.DownEast, Direction.UpWest, Direction.DownWest);
            }
            if (Math.Abs(vector.X) == 0)
            {
                return DetermineDirection(vector, vector.Y, vector.Z, Direction.UpNorth, Direction.DownNorth, Direction.DownWest, Direction.DownSouth);
            }
            return Direction.Unknown;
        }


        private static List<Point3D> FindCommonPoints(List<Point3D> firstBranchBoxPoints, List<Point3D> secondBranchBoxPoints)
        {
            List<Point3D> commonPoints = new List<Point3D>();
            foreach (var point in firstBranchBoxPoints)
            {
                var pointCoordinates = new double[] { point.X, point.Y, point.Z };

                foreach (var point2 in secondBranchBoxPoints)
                {
                    var point2Coordinates = new double[] { point2.X, point2.Y, point2.Z };
                    if (pointCoordinates[0] == point2Coordinates[0] && pointCoordinates[1] == point2Coordinates[1] && pointCoordinates[2] == point2Coordinates[2])
                    {
                        commonPoints.Add(point);
                    }
                }
            }

            return commonPoints;
        }

        private List<Point3D> GetBBoxEdgeCoordinates(BoundingBox3D boundingBox)
        {
            List<Point3D> branchBoxPoints = new List<Point3D>
            {
                new Point3D(Math.Round(boundingBox.Min.X, 3), Math.Round(boundingBox.Min.Y, 3), Math.Round(boundingBox.Min.Z, 3)), // bottom left front
                new Point3D(Math.Round(boundingBox.Min.X, 3), Math.Round(boundingBox.Max.Y, 3), Math.Round(boundingBox.Min.Z, 3)), // bottom right front
                new Point3D(Math.Round(boundingBox.Min.X, 3), Math.Round(boundingBox.Min.Y, 3), Math.Round(boundingBox.Max.Z, 3)), // bottom left back
                new Point3D(Math.Round(boundingBox.Min.X, 3), Math.Round(boundingBox.Max.Y, 3), Math.Round(boundingBox.Max.Z, 3)), // bottom right back
                new Point3D(Math.Round(boundingBox.Max.X, 3), Math.Round(boundingBox.Min.Y, 3), Math.Round(boundingBox.Min.Z, 3)), // top left front
                new Point3D(Math.Round(boundingBox.Max.X, 3), Math.Round(boundingBox.Max.Y, 3), Math.Round(boundingBox.Min.Z, 3)), // top right front
                new Point3D(Math.Round(boundingBox.Max.X, 3), Math.Round(boundingBox.Min.Y, 3), Math.Round(boundingBox.Max.Z, 3)), // top left back
                new Point3D(Math.Round(boundingBox.Max.X, 3), Math.Round(boundingBox.Max.Y, 3), Math.Round(boundingBox.Max.Z, 3)) // top right back
            };
            return branchBoxPoints;
        }

        private Direction SingleBranchDirection(Shape shape)
        {
            return shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Y ? shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Z ? Direction.East : Direction.Up : shape.ModelItem.BoundingBox().Size.Y > shape.ModelItem.BoundingBox().Size.Z ? Direction.North : Direction.Down;
        }
    }
}
