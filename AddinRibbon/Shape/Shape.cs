using Autodesk.Navisworks.Api;

namespace AddinRibbon.Shapes
{
    public class Shape
    {
        public string Name { get; set; }
        public ModelItem ModelItem { get; set; }
        public ShapeType? ShapeType { get; set; } = null;
        public Direction? Direction { get; set; } = null;

        public Shape(string name, ModelItem modelItem)
        {
            Name = name;
            ModelItem = modelItem;
        }
    }
}
