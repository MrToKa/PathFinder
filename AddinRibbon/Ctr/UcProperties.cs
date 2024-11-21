﻿using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisworksApp = Autodesk.Navisworks.Api.Application;
using Autodesk.Navisworks.Api.Plugins;
using static System.Windows.Forms.LinkLabel;
using AddinRibbon.Dijkstra;

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
            if (cbPause.Checked) return;

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

                    //AddChildrenProperties(item.Children, lines, 1);
                    //add children points to list
                    AddChildrenPoints(item.Children, pointsList, 1);
                    lines.Add(Environment.NewLine);

                    AddChildrenNodesCode(lines, item);

                }

                tbProp.Text = string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private List<Point3D> CalculateMedianPoints(BoundingBox3D boundingBox)
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

        private void AddChildrenNodesCode(List<string> lines, ModelItem item)
        {
            foreach (var child in item.Children)
            {
                if (!child.Children.Any())
                {
                    //Check if the child.DisplayName is null or empty string
                    if (string.IsNullOrEmpty(child.DisplayName))
                    {
                        continue;
                    }
                    AddNodeCode(lines, child, CalculateMedianPoints(child.BoundingBox()));
                }
                else
                {
                    AddChildrenNodesCode(lines, child);
                }
            }
        }

        private void AddChildrenPoints(IEnumerable<ModelItem> children, List<Point3D> pointsList, int level)
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

        private void AddChildrenProperties(IEnumerable<ModelItem> children, List<string> lines, int level)
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

        private void btnFindPath_Click(object sender, EventArgs e)
        {
            if (cbPause.Checked) return;

            try
            {
                GetProperties(null, null);

                //var navApp = NavisworksApp.ActiveDocument;

                //string fromInputField = tbFromElement.Text;
                //string toInputField = tbToElement.Text;

                //ModelItemCollection searchResults = new ModelItemCollection();

                //foreach (ModelItem modelItem in NavisworksApp.ActiveDocument.Models.CreateCollectionFromRootItems().DescendantsAndSelf)
                //{
                //    if ((modelItem.DisplayName == fromInputField || modelItem.DisplayName == toInputField) && !modelItem.AncestorsAndSelf.Any(p => p.IsHidden))
                //    {
                //        searchResults.Add(modelItem);
                //    }
                //}

                //ModelItem searchResultfromInputField = searchResults.FirstOrDefault(m => m.DisplayName == fromInputField);
                //ModelItem searchResulttoInputField = searchResults.FirstOrDefault(m => m.DisplayName == toInputField);

                //string fromLines = string.Empty;
                //fromLines += "X: " + searchResultfromInputField.BoundingBox().Center.X.ToString("0.000");
                //fromLines += " Y: " + searchResultfromInputField.BoundingBox().Center.Y.ToString("0.000");
                //fromLines += " Z: " + searchResultfromInputField.BoundingBox().Center.Z.ToString("0.000");

                //textBoxFrom.Text = fromLines;

                //string toLines = string.Empty;
                //toLines += "X: " + searchResulttoInputField.BoundingBox().Center.X.ToString("0.000");
                //toLines += " Y: " + searchResulttoInputField.BoundingBox().Center.Y.ToString("0.000");
                //toLines += " Z: " + searchResulttoInputField.BoundingBox().Center.Z.ToString("0.000");

                //textBoxTo.Text = toLines;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void AddNodeCode(List<string> lines, ModelItem item, List<Point3D> medianPoints)
        {
            lines.Add("Node Code:");
            lines.Add($"{item.DisplayName}");
            lines.Add("--------------------");
            lines.Add($"var {item.DisplayName.Substring(item.DisplayName.Length - 4).ToLower()}nodes = new List<Node>");
            lines.Add("{");
            foreach (var point in medianPoints)
            {
                lines.Add($"new Node {{ TrayName = \"{item.DisplayName.Substring(item.DisplayName.Length - 4)}\", X = {point.X.ToString("0.000")}, Y = {point.Y.ToString("0.000")}, Z = {point.Z.ToString("0.000")} }},");
            }
            lines.Add("};");
            lines.Add($"trays[\"{item.DisplayName.Substring(item.DisplayName.Length - 4)}\"] = {item.DisplayName.Substring(item.DisplayName.Length - 4).ToLower()}nodes;");
            lines.Add("--------------------");
        }
    }
}