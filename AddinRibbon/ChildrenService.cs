using Autodesk.Navisworks.Api;
using System.Collections.Generic;
using System.Linq;

namespace AddinRibbon
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
                    var indent = new string('.', level * 4);
                    lines.Add(string.Concat(indent, child.DisplayName));
                    lines.Add(string.Concat(indent, ".   BoundingBox Point"));
                    lines.Add(string.Concat(indent, ".   .   X:", child.BoundingBox().Center.X.ToString("0.000")));
                    lines.Add(string.Concat(indent, ".   .   Y:", child.BoundingBox().Center.Y.ToString("0.000")));
                    lines.Add(string.Concat(indent, ".   .   Z:", child.BoundingBox().Center.Z.ToString("0.000")));
                }
                else
                {
                    AddChildrenProperties(child.Children, lines, level + 1);
                }
            }
        }
    }
}
