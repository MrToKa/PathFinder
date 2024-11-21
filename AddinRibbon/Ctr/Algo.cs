using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using NavisworksApp = Autodesk.Navisworks.Api.Application;
using Autodesk.Navisworks.Api.Plugins;
using static System.Windows.Forms.LinkLabel;
using AddinRibbon.Dijkstra;
using AddinRibbon.TraysData;


namespace AddinRibbon.Ctr
{
    /// <summary>
    /// Aula/Lesson 6
    /// </summary>
    /// 

    [DockPanePlugin(800, 1500, AutoScroll = true, MinimumHeight = 100, MinimumWidth = 200)]

    public partial class Algo : UserControl
    {

        public Algo()
        {
            InitializeComponent();

            ListenSelection(null, null);
            NavisworksApp.MainDocumentChanged += ListenSelection;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            Dock = DockStyle.None;
        }

        private void ListenSelection(object sender, EventArgs e)
        {
            try
            {
                NavisworksApp.ActiveDocument.CurrentSelection.Changed += GetProperties;
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

                Graph graph = new Graph();

                var sections = GetSections();

                foreach ( var section in sections ) {
                    foreach (var node in section.Value)
                    {
                        graph.AddNode(node);
                    }
                }

                var navApp = NavisworksApp.ActiveDocument;

                string fromInputField = tbFromElement.Text;
                string toInputField = tbToElement.Text;

                var fromModelItem = GetModelItem(fromInputField);
                var toModelItem = GetModelItem(toInputField);

                // Create Begin and End Nodes from fromModelItem and toModelItem
                Node beginNode = new Node { SectionName = "Begin", X = fromModelItem.BoundingBox().Center.X, Y = fromModelItem.BoundingBox().Center.Y, Z = fromModelItem.BoundingBox().Center.Z };
                Node endNode = new Node { SectionName = "End", X = toModelItem.BoundingBox().Center.X, Y = toModelItem.BoundingBox().Center.Y, Z = toModelItem.BoundingBox().Center.Z };

                graph.AddNode(beginNode);
                graph.AddNode(endNode);

                // Add edges within branches
                foreach (var branchNodes in sections.Values)
                {
                    for (int i = 0; i < branchNodes.Count - 1; i++)
                    {
                        graph.AddBidirectionalEdge(branchNodes[i], branchNodes[i + 1]);
                    }
                }

                // Dynamically connect Begin node to the closest node in the branches
                Node closestToBegin = FindClosestNode(beginNode, graph.Nodes.Where(n => n.SectionName != "Begin" && n.SectionName != "End").ToList());
                graph.AddBidirectionalEdge(beginNode, closestToBegin);

                // Dynamically connect End node to the closest node in the branches
                Node closestToEnd = FindClosestNode(endNode, graph.Nodes.Where(n => n.SectionName != "Begin" && n.SectionName != "End").ToList());
                graph.AddBidirectionalEdge(endNode, closestToEnd);

                // Dynamically connect branches to each other by connecting closest nodes between branches
                var branchNames = sections.Keys.ToList();
                for (int i = 0; i < branchNames.Count; i++)
                {
                    for (int j = i + 1; j < branchNames.Count; j++)
                    {
                        var branchA = sections[branchNames[i]];
                        var branchB = sections[branchNames[j]];
                        ConnectClosestNodesBetweenBranches(graph, branchA, branchB);
                    }
                }

                // Run Dijkstra's algorithm
                var (path, totalDistance) = Dijkstra.Dijkstra.FindShortestPath(graph, beginNode, endNode);

                // Output the results
                if (path != null)
                {
                    lines.Add($"Shortest path distance: {totalDistance:F3} meters.");
                    lines.Add("Path:");

                    foreach (var node in path)
                    {
                        lines.Add($"Branch: {node.SectionName} Node ID: {node.Id}, Coordinates: ({node.X:F3}, {node.Y:F3}, {node.Z:F3})");
                    }

                    // Extract unique branch names
                    var uniqueBranches = path
                        .Select(n => n.SectionName)
                        .Where(name => name != null && name != "Begin" && name != "End")
                        .Distinct();

                    lines.Add("Branches involved in the path:");
                    foreach (var branch in uniqueBranches)
                    {
                        lines.Add(branch);
                    }
                }
                else
                {
                    lines.Add("No path found between the specified nodes.");
                }

                textBoxFrom.Text = $"X: {beginNode.X:f3}, Y: {beginNode.Y:F3}, Z: {beginNode.Z:F3}";
                textBoxTo.Text = $"X: {endNode.X:f3}, Y: {endNode.Y:F3}, Z: {endNode.Z:F3}";

                tbProp.Text = string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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


        private void UcProperties_Load(object sender, EventArgs e)
        {

        }

        private void cbPause_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnFindPath_Click(object sender, EventArgs e)
        {
            if (cbPause.Checked) return;

            try
            {
                GetProperties(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private ModelItem GetModelItem(string inputField)
        {
            ModelItemCollection searchResults = new ModelItemCollection();

            foreach (ModelItem modelItem in NavisworksApp.ActiveDocument.Models.CreateCollectionFromRootItems().DescendantsAndSelf)
            {
                if (modelItem.DisplayName == inputField && !modelItem.AncestorsAndSelf.Any(p => p.IsHidden))
                {
                    searchResults.Add(modelItem);
                }
            }

            ModelItem searchResult = searchResults.FirstOrDefault(m => m.DisplayName == inputField);

            return searchResult;
        }

        private static Node FindClosestNode(Node targetNode, List<Node> nodes)
        {
            Node closestNode = null;
            double minDistance = double.PositiveInfinity;

            foreach (var node in nodes)
            {
                double distance = CalculateDistance(targetNode, node);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestNode = node;
                }
            }

            return closestNode;
        }

        private static void ConnectClosestNodesBetweenBranches(Graph graph, List<Node> branchA, List<Node> branchB)
        {
            Node closestNodeA = null;
            Node closestNodeB = null;
            double minDistance = double.PositiveInfinity;

            foreach (var nodeA in branchA)
            {
                foreach (var nodeB in branchB)
                {
                    double distance = CalculateDistance(nodeA, nodeB);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestNodeA = nodeA;
                        closestNodeB = nodeB;
                    }
                }
            }

            if (closestNodeA != null && closestNodeB != null)
            {
                graph.AddBidirectionalEdge(closestNodeA, closestNodeB);
            }
        }

        private static double CalculateDistance(Node a, Node b)
        {
            return Math.Sqrt(
                Math.Pow(a.X - b.X, 2) +
                Math.Pow(a.Y - b.Y, 2) +
                Math.Pow(a.Z - b.Z, 2));
        }


        private Dictionary<string, List<Node>> _cachedSections;

        private Dictionary<string, List<Node>> GetSections()
        {
            if (_cachedSections != null)
            {
                return _cachedSections;
            }

            var sections = new Dictionary<string, List<Node>>();
            var bcBranches = TraysData.TraysData.GetBCBranches();
            //var bBranches = TraysData.TraysData.GetBBranches();
            var cBranches = TraysData.TraysData.GetCBranches();
            //var dfbBranches = TraysData.TraysData.GetDFBBranches();
            var dfcBranches = TraysData.TraysData.GetDFCBranches();

            // Add all nodes to the sections
            sections["BC"] = bcBranches;
            sections["C"] = cBranches;
            sections["DFC"] = dfcBranches;
            //sections["B"] = bcBranches;
            //sections["DFB"] = dfcBranches;

            _cachedSections = sections;
            return sections;
        }
    }
}