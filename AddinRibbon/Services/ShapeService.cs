using AddinRibbon.Shapes;
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
                branches[1].Direction = SingleBranchDirection(branches[1]);
                return;
            }


            for (int i = 0; i < branches.Count; i++)
            {

            }
        }

        //private Direction FirstBranchDirection(Dictionary<int, Shape> branch)
        //{
        //}

        //private Direction MiddleBranchDirection(Dictionary<int, Shape> branch)
        //{
        //}
        //private Direction LastBranchDirection(Dictionary<int, Shape> branch)
        //{
        //}

        private Direction SingleBranchDirection(Shape shape)
        {
            return shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Y ? shape.ModelItem.BoundingBox().Size.X > shape.ModelItem.BoundingBox().Size.Z ? Direction.EastWest : Direction.UpDown : shape.ModelItem.BoundingBox().Size.Y > shape.ModelItem.BoundingBox().Size.Z ? Direction.NorthSouth : Direction.UpDown;
        }
    }
}
