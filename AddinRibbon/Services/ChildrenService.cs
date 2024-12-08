using Autodesk.Navisworks.Api;
using System.Collections.Generic;
using System.Linq;

namespace AddinRibbon.Services
{
    public class ChildrenService
    {
        public string AddChildrenNodesCode(ModelItem item, ItemService itemService)
        {
            var code = string.Empty;
            foreach (var child in item.Children)
            {
                if (!child.Children.Any())
                {
                    //Check if the child.DisplayName is null or empty string
                    if (string.IsNullOrEmpty(child.DisplayName))
                    {
                        continue;
                    }
                    code += itemService.AddNodeCode(child, itemService.CalculateMedianPoints(child.BoundingBox()));
                }
                else
                {
                    code += AddChildrenNodesCode(child, itemService);
                }
            }
            return code;
        }

        public void AddChildrenPoints(IEnumerable<ModelItem> children, List<Point3D> pointsList, int level)
        {
            foreach (var child in children)
            {
                if (!child.Children.Any())
                {
                    pointsList.Add(child.BoundingBox().Center);
                }
                else
                {
                    AddChildrenPoints(child.Children, pointsList, level + 1);
                }
            }
        }

        public void AddChildrenProperties(IEnumerable<ModelItem> children, List<string> lines, int level)
        {
            foreach (var child in children)
            {
                if (!child.Children.Any())
                {
                    var indent = new string('.', level * 1);
                    lines.Add(string.Concat(indent, child.DisplayName));
                    lines.Add(string.Concat(indent, ".   BoundingBox Center Point - ", "X: ", child.BoundingBox().Center.X.ToString("0.000") + " Y: ", child.BoundingBox().Center.Y.ToString("0.000") + " Z: ", child.BoundingBox().Center.Z.ToString("0.000")));
                    lines.Add(string.Concat(indent, ".   BoundingBox Min Point - ", "X: ", child.BoundingBox().Min.X.ToString("0.000") + " Y: ", child.BoundingBox().Min.Y.ToString("0.000") + " Z: ", child.BoundingBox().Min.Z.ToString("0.000")));
                    lines.Add(string.Concat(indent, ".   BoundingBox Max Point - ", "X: ", child.BoundingBox().Max.X.ToString("0.000") + " Y: ", child.BoundingBox().Max.Y.ToString("0.000") + " Z: ", child.BoundingBox().Max.Z.ToString("0.000")));
                    lines.Add(string.Concat(indent, ".   BoundingBox Size - ", "X: ", child.BoundingBox().Size.X.ToString("0.000") + " Y: ", child.BoundingBox().Size.Y.ToString("0.000") + " Z: ", child.BoundingBox().Size.Z.ToString("0.000")));
                }
                else
                {
                    AddChildrenProperties(child.Children, lines, level + 1);
                }
            }
        }

        public List<ModelItem> GetDeepestChildrens(ModelItem item)
        {
            var children = new List<ModelItem>();
            foreach (var child in item.Children)
            {
                if (!child.Children.Any())
                {
                    children.Add(child);
                }
                else
                {
                    children.AddRange(GetDeepestChildrens(child));
                }
            }
            return children;
        }
    }
}
