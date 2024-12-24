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

                if (i == branches.Count - 1)
                {
                    //branches[i + 1].Direction = LastBranchDirection(branches);
                }
                else
                {
                    var branchesToCompare = new Dictionary<int, Shape> { { 1, branches[i + 1] }, { 2, branches[i + 2] } };

                    branches[i + 1].Direction = BranchDirection(branchesToCompare);
                }

            }
        }

        private Direction BranchDirection(Dictionary<int, Shape> branches)
        {
            var firstBranch = branches[1];
            var secondBranch = branches[2];

            var firstBranchBox = firstBranch.ModelItem.BoundingBox();
            var secondBranchBox = secondBranch.ModelItem.BoundingBox();

            List<Point3D> firstBranchBoxPoints = GetBBoxEdgeCoordinates(firstBranchBox);
            List<Point3D> secondBranchBoxPoints = GetBBoxEdgeCoordinates(secondBranchBox);

            if (firstBranch.ShapeType == ShapeType.FTUBE)
            {
                //Check for 2 edge points with the same coordinates in the branches box points
                //If there are 2 edge points with the same coordinates, then the first Bounding Box is aligned with one of the axis
                //The direction can be determined by that on which side of the Bounding Boxes are touching.
                //Touching edges should be determined
                //The direction is a vector from the center point to the BBox touching side.
                //The width of the touching side is equal to the width of the shape
                //The height of all the shapes is 100mm
                //The lenght of the shape is the distance between the touching side and the opposite side of the Bounding Box

                List<Point3D> commonPoints = FindCommonPoints(firstBranchBoxPoints, secondBranchBoxPoints);

                if (commonPoints.Count == 4)
                {
                    // the two shapes are FTUBE and are aligned with the same axis
                    // the direction is the vector from the center of the first shape to the center of the second shape
                    return Math.Round(firstBranchBox.Center.X, 3) > Math.Round(secondBranchBox.Center.X, 3) ? Direction.EastWest :
                           Math.Round(firstBranchBox.Center.X, 3) < Math.Round(secondBranchBox.Center.X, 3) ? Direction.WestEast :
                           Math.Round(firstBranchBox.Center.Y, 3) > Math.Round(secondBranchBox.Center.Y, 3) ? Direction.NorthSouth :
                           Math.Round(firstBranchBox.Center.Y, 3) < Math.Round(secondBranchBox.Center.Y, 3) ? Direction.SouthNorth :
                           Math.Round(firstBranchBox.Center.Z, 3) > Math.Round(secondBranchBox.Center.Z, 3) ? Direction.UpDown : Direction.DownUp;
                }

                if (commonPoints.Count == 2)
                {
                    // The direction is the bigges offset on the X,Y or Z axis between the center points.
                    var vector = firstBranchBox.Center - secondBranchBox.Center;

                    if (Math.Abs(vector.X) > Math.Abs(vector.Y) && Math.Abs(vector.X) > Math.Abs(vector.Z))
                    {
                        return vector.X > 0 ? Direction.EastWest : Direction.WestEast;
                    }
                    else if (Math.Abs(vector.Y) > Math.Abs(vector.X) && Math.Abs(vector.Y) > Math.Abs(vector.Z))
                    {
                        return vector.Y > 0 ? Direction.NorthSouth : Direction.SouthNorth;
                    }
                    else if (Math.Abs(vector.Z) > Math.Abs(vector.X) && Math.Abs(vector.Z) > Math.Abs(vector.Y))
                    {
                        return vector.Z > 0 ? Direction.UpDown : Direction.DownUp;
                    }
                }

                return Direction.Unknown;



                //If there are no 2 edge points with the same coordinates, then the first Bounding Box is not aligned with any of the axis



            }

            else if (firstBranch.ShapeType == ShapeType.BEND)
            {
                List<Point3D> commonPoints = FindCommonPoints(firstBranchBoxPoints, secondBranchBoxPoints);

                if (commonPoints.Count == 2)
                {
                    var vector = firstBranchBox.Center - secondBranchBox.Center;

                    if (Math.Abs(vector.Z) == 0)
                    {
                        if (Math.Abs(vector.X) > Math.Abs(vector.Y) && vector.Y > 0)
                        {
                            return vector.X > 0 ? Direction.NorthEast : Direction.NorthWest;
                        }
                        if (Math.Abs(vector.X) > Math.Abs(vector.Y) && vector.Y < 0)
                        {
                            return vector.X > 0 ? Direction.SouthEast : Direction.SouthWest;
                        }
                        else if (Math.Abs(vector.Y) > Math.Abs(vector.X) && vector.X > 0)
                        {
                            return vector.Y > 0 ? Direction.NorthEast : Direction.SouthEast;
                        }
                        else if (Math.Abs(vector.Y) > Math.Abs(vector.X) && vector.X < 0)
                        {
                            return vector.Y > 0 ? Direction.NorthWest : Direction.SouthWest;
                        }
                    }
                    if (Math.Abs(vector.Y) == 0)
                    {
                        if (Math.Abs(vector.X) > Math.Abs(vector.Z) && vector.Z > 0)
                        {
                            return vector.X > 0 ? Direction.UpEast : Direction.UpWest;
                        }
                        if (Math.Abs(vector.X) > Math.Abs(vector.Z) && vector.Z < 0)
                        {
                            return vector.X > 0 ? Direction.DownEast : Direction.DownWest;
                        }
                        else if (Math.Abs(vector.Z) > Math.Abs(vector.X) && vector.X > 0)
                        {
                            return vector.Z > 0 ? Direction.UpEast : Direction.DownEast;
                        }
                        else if (Math.Abs(vector.Z) > Math.Abs(vector.X) && vector.X < 0)
                        {
                            return vector.Z > 0 ? Direction.UpWest : Direction.DownWest;
                        }
                    }
                    if ((Math.Abs(vector.X) == 0))
                    {
                        if (Math.Abs(vector.Y) > Math.Abs(vector.Z) && vector.Z > 0)
                        {
                            return vector.Y > 0 ? Direction.UpNorth : Direction.UpSouth;
                        }
                        if (Math.Abs(vector.Y) > Math.Abs(vector.Z) && vector.Z < 0)
                        {
                            return vector.Y > 0 ? Direction.DownNorth : Direction.DownSouth;
                        }
                        else if (Math.Abs(vector.Z) > Math.Abs(vector.Y) && vector.Y > 0)
                        {
                            return vector.Z > 0 ? Direction.UpNorth : Direction.DownNorth;
                        }
                        else if (Math.Abs(vector.Z) > Math.Abs(vector.Y) && vector.Y < 0)
                        {
                            return vector.Z > 0 ? Direction.UpSouth : Direction.DownSouth;
                        }
                    }
                }
            }

            //else if (firstBranch.ShapeType == ShapeType.TEE)
            //{
            //}
            //else if (firstBranch.ShapeType == ShapeType.ELBOW)
            //{
            //}
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

        //private Direction MiddleBranchDirection(Dictionary<int, Shape> branches)
        //{
        //}
        //private Direction LastBranchDirection(Dictionary<int, Shape> branches)
        //{
        //}

        private Direction SingleBranchDirection(Shape shape)
        {
            return shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Y ? shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Z ? Direction.EastWest : Direction.UpDown : shape.ModelItem.BoundingBox().Size.Y > shape.ModelItem.BoundingBox().Size.Z ? Direction.NorthSouth : Direction.UpDown;
        }
    }
}
