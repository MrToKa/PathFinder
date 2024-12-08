using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisworksApp = Autodesk.Navisworks.Api.Application;
using Autodesk.Navisworks.Api.Plugins;
using AddinRibbon.Shapes;
using AddinRibbon.Services;

namespace AddinRibbon.Ctr
{
    /// <summary>
    /// Aula/Lesson 6
    /// </summary>
    /// 

    [DockPanePlugin(800, 1500, AutoScroll = true, MinimumHeight = 100, MinimumWidth = 200)]

    public partial class UcProperties : UserControl
    {

        /// <summary>
        /// Aula/Lesson 6
        /// </summary>
        public UcProperties()
        {
            InitializeComponent();

            ListenSelection(null, null);
            NavisworksApp.MainDocumentChanged += ListenSelection;
        }

        /// <summary>
        /// Aula/Lesson 3
        /// </summary>
        /// <param name="e"></param>
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Dock = DockStyle.None;
        }

        /// <summary>
        /// Aula/Lesson 6
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListenSelection(object sender, EventArgs e)
        {
            try
            {
                //NavisworksApp.ActiveDocument.CurrentSelection.Changed += GetProperties;
            }
            catch (Exception)
            {

            }

        }

        private void GetProperties(object sender, EventArgs e)
        {
            try
            {
                tbProp.Clear();

                var lines = new List<string>();

                List<Point3D> pointsList = new List<Point3D>();

                foreach (var item in NavisworksApp.ActiveDocument.CurrentSelection.SelectedItems)
                {
                    lines.Add(item.DisplayName);
                    lines.Add("BoundingBox Point");
                    lines.Add(string.Concat("X:", item.BoundingBox().Center.X.ToString("0.000")));
                    lines.Add(string.Concat("Y:", item.BoundingBox().Center.Y.ToString("0.000")));
                    lines.Add(string.Concat("Z:", item.BoundingBox().Center.Z.ToString("0.000")));

                    lines.Add(item.BoundingBox().Min.X.ToString("0.000"));
                    lines.Add(item.BoundingBox().Min.Y.ToString("0.000"));
                    lines.Add(item.BoundingBox().Min.Z.ToString("0.000"));
                    lines.Add(item.BoundingBox().Max.X.ToString("0.000"));
                    lines.Add(item.BoundingBox().Max.Y.ToString("0.000"));
                    lines.Add(item.BoundingBox().Max.Z.ToString("0.000"));
                    lines.Add(item.BoundingBox().Size.X.ToString("0.000"));
                    lines.Add(item.BoundingBox().Size.Y.ToString("0.000"));
                    lines.Add(item.BoundingBox().Size.Z.ToString("0.000"));
                    lines.Add(Environment.NewLine);

                    var childrenService = new ChildrenService();
                    childrenService.AddChildrenPoints(item.Children, pointsList, 1);

                    var childrens = childrenService.GetDeepestChildrens(item);

                    var branches = new Dictionary<int, Shape>();
                    for (int i = 0; i < childrens.Count; i++)
                    {
                        int index = i + 1;
                        branches.Add(index, new Shape(childrens[i].DisplayName, childrens[i]));
                    }

                    var shapeService = new ShapeService();
                    shapeService.AssignShapesTypes(branches);

                    //var itemService = new ItemService();
                    //lines.Add(childrenService.AddChildrenNodesCode(item, itemService));

                    childrenService.AddChildrenProperties(item.Children, lines, 1);

                }

                tbProp.Text = string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void BtnFindPath_Click(object sender, EventArgs e)
        {
            try
            {
                GetProperties(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}