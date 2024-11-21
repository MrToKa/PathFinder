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
using System.IO;


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
                //NavisworksApp.ActiveDocument.CurrentSelection.Changed += GetProperties;
            }
            catch (Exception)
            {

            }

        }

        //private void GetProperties(object sender, EventArgs e)
        //{
        //    if (cbPause.Checked) return;

        //    try
        //    {
        //        tbProp.Clear();

        //        var lines = new List<string>();

        //        double physicalConnectionThreshold = 0.25; // Meters
        //        double maxConnectionThreshold = 1.3;       // Meters
        //        double jumpPenalty = 1.0;                  // Penalty in meters (adjust as needed)


        //        Graph graph = new Graph();

        //        var sections = GetSections();

        //        foreach ( var section in sections ) {
        //            foreach (var node in section.Value)
        //            {
        //                graph.AddNode(node);
        //            }
        //        }

        //        var navApp = NavisworksApp.ActiveDocument;

        //        string fromInputField = tbFromElement.Text;
        //        string toInputField = tbToElement.Text;

        //        var fromModelItem = GetModelItem(fromInputField);
        //        var toModelItem = GetModelItem(toInputField);

        //        // Create Begin and End Nodes from fromModelItem and toModelItem
        //        Node beginNode = new Node 
        //        { TrayName = "Begin", 
        //            X = fromModelItem.BoundingBox().Center.X,
        //            Y = fromModelItem.BoundingBox().Center.Y, 
        //            Z = fromModelItem.BoundingBox().Center.Z };

        //        Node endNode = new Node { TrayName = "End", 
        //            X = toModelItem.BoundingBox().Center.X, 
        //            Y = toModelItem.BoundingBox().Center.Y, 
        //            Z = toModelItem.BoundingBox().Center.Z };

        //        graph.AddNode(beginNode);
        //        graph.AddNode(endNode);

        //        // Add edges within branches
        //        foreach (var trayNodes in sections.Values)
        //        {
        //            for (int i = 0; i < trayNodes.Count - 1; i++)
        //            {
        //                graph.AddBidirectionalEdge(trayNodes[i], trayNodes[i + 1]);
        //            }
        //        }

        //        // Find physically connected trays
        //        var trayConnections = FindPhysicallyConnectedTrays(sections, physicalConnectionThreshold);

        //        // Connect nodes between physically connected trays
        //        foreach (var trayName in trayConnections.Keys)
        //        {
        //            var trayA = sections[trayName];
        //            foreach (var connectedTrayName in trayConnections[trayName])
        //            {
        //                var trayB = sections[connectedTrayName];
        //                ConnectNodesBetweenBranchesWithinThreshold(graph, trayA, trayB, connectionThreshold);
        //            }
        //        }

        //        bool beginSecondaryTray = false;
        //        bool endSecondaryTray = false;

        //        // Dynamically connect Begin node to the closest node in the branches
        //        Node closestToBegin = FindClosestNode(beginNode, graph.Nodes.Where(n => n.TrayName != "Begin" && n.TrayName != "End").ToList());

        //        double beginDistance = CalculateDistance(beginNode, closestToBegin);
        //        if (beginDistance > 2)
        //        {
        //            beginSecondaryTray = true;
        //        }
        //        graph.AddBidirectionalEdge(beginNode, closestToBegin);

        //        // Dynamically connect End node to the closest node in the branches
        //        Node closestToEnd = FindClosestNode(endNode, graph.Nodes.Where(n => n.TrayName != "Begin" && n.TrayName != "End").ToList());
        //        double endDistance = CalculateDistance(endNode, closestToEnd);
        //        if (endDistance > 2) 
        //        {
        //            endSecondaryTray = true;
        //        }
        //        graph.AddBidirectionalEdge(endNode, closestToEnd);

        //        // Dynamically connect branches to each other by connecting closest nodes between branches
        //        // Connect branches to each other by connecting nodes within the threshold distance
        //        //var branchNames = sections.Keys.ToList();
        //        //for (int i = 0; i < branchNames.Count; i++)
        //        //{
        //        //    for (int j = i + 1; j < branchNames.Count; j++)
        //        //    {
        //        //        var branchA = sections[branchNames[i]];
        //        //        var branchB = sections[branchNames[j]];
        //        //        ConnectNodesBetweenBranchesWithinThreshold(graph, branchA, branchB, connectionThreshold);
        //        //    }
        //        //}

        //        //foreach (var trayName in trayConnections.Keys)
        //        //{
        //        //    var trayA = sections[trayName];
        //        //    foreach (var connectedTrayName in trayConnections[trayName])
        //        //    {
        //        //        var trayB = sections[connectedTrayName];
        //        //        ConnectNodesBetweenBranchesWithinThreshold(graph, trayA, trayB, connectionThreshold);
        //        //    }
        //        //}


        //        // Run Dijkstra's algorithm
        //        var (path, totalDistance) = Dijkstra.Dijkstra.FindShortestPath(graph, beginNode, endNode);

        //        // Output the results
        //        if (path != null)
        //        {
        //            lines.Add($"Shortest path distance: {totalDistance:F3} meters.");
        //            lines.Add("Path:");

        //            foreach (var node in path)
        //            {
        //                lines.Add($"Branch: {node.TrayName} Node ID: {node.Id}, Coordinates: ({node.X:F3}, {node.Y:F3}, {node.Z:F3})");
        //            }

        //            // Extract unique branch names
        //            var uniqueBranches = path
        //                .Select(n => n.TrayName)
        //                .Where(name => name != null && name != "Begin" && name != "End")
        //                .Distinct();                   

        //            lines.Add("Branches involved in the path:");
        //            if (beginSecondaryTray)
        //            {
        //                lines.Add("/SECONDARY");
        //            }
        //            foreach (var branch in uniqueBranches)
        //            {
        //                lines.Add(branch);
        //            }
        //            if (endSecondaryTray)
        //            {
        //                lines.Add("/SECONDARY");
        //            }
        //        }
        //        else
        //        {
        //            lines.Add("No path found between the specified nodes.");
        //        }

        //        textBoxFrom.Text = $"X: {beginNode.X:f3}, Y: {beginNode.Y:F3}, Z: {beginNode.Z:F3}";
        //        textBoxTo.Text = $"X: {endNode.X:f3}, Y: {endNode.Y:F3}, Z: {endNode.Z:F3}";

        //        tbProp.Text = string.Join(Environment.NewLine, lines);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }
        //}

        private void GetProperties(object sender, EventArgs e)
        {
            if (cbPause.Checked) return;

            try
            {
                tbProp.Clear();

                var lines = new List<string>();

                // Define thresholds and penalties
                double physicalConnectionThreshold = 0.25; // Meters
                double maxConnectionThreshold = 1.3;       // Meters
                double maxTrayConnectionDistance = 1.3;    // Meters
                double jumpPenalty = 0.0;                  // Penalty in meters (adjust as needed)

                Graph graph = new Graph();

                var sections = GetSections();

                foreach (var section in sections)
                {
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
                Node beginNode = new Node
                {
                    TrayName = "Begin",
                    X = fromModelItem.BoundingBox().Center.X,
                    Y = fromModelItem.BoundingBox().Center.Y,
                    Z = fromModelItem.BoundingBox().Center.Z
                };
                Node endNode = new Node
                {
                    TrayName = "End",
                    X = toModelItem.BoundingBox().Center.X,
                    Y = toModelItem.BoundingBox().Center.Y,
                    Z = toModelItem.BoundingBox().Center.Z
                };

                graph.AddNode(beginNode);
                graph.AddNode(endNode);

                // Connect nodes within trays
                foreach (var trayNodes in sections.Values)
                {
                    ConnectNodesWithinTray(graph, trayNodes, maxTrayConnectionDistance);
                }

                // Connect nodes between different trays
                var trayNames = sections.Keys.ToList();
                for (int i = 0; i < trayNames.Count; i++)
                {
                    var trayA = sections[trayNames[i]];
                    for (int j = i + 1; j < trayNames.Count; j++)
                    {
                        var trayB = sections[trayNames[j]];
                        ConnectNodesBetweenTrays(graph, trayA, trayB, physicalConnectionThreshold, maxConnectionThreshold, jumpPenalty);
                    }
                }

                bool beginSecondaryTray = false;
                bool endSecondaryTray = false;

                // Connect Begin node
                Node closestToBegin = FindClosestNode(beginNode, graph.Nodes.Where(n => n.TrayName != "Begin" && n.TrayName != "End").ToList());
                double beginDistance = CalculateDistance(beginNode, closestToBegin);
                if (beginDistance > 2)
                {
                    beginSecondaryTray = true;
                }
                graph.AddBidirectionalEdge(beginNode, closestToBegin, beginDistance);

                // Connect End node
                Node closestToEnd = FindClosestNode(endNode, graph.Nodes.Where(n => n.TrayName != "Begin" && n.TrayName != "End").ToList());
                double endDistance = CalculateDistance(endNode, closestToEnd);
                if (endDistance > 2)
                {
                    endSecondaryTray = true;
                }
                graph.AddBidirectionalEdge(endNode, closestToEnd, endDistance);

                // save the graph to a file
                using (StreamWriter writer = new StreamWriter("Graph.txt"))
                {
                    foreach (var node in graph.Nodes)
                    {
                        writer.WriteLine($"Node ID: {node.Id}, Tray: {node.TrayName}, Coordinates: ({node.X:F3}, {node.Y:F3}, {node.Z:F3})");
                        foreach (var edge in node.Edges)
                        {
                            writer.WriteLine($"  Edge to Node ID: {edge.TargetNode.Id}, Weight: {edge.Weight:F3}");
                        }
                    }
                }


                //Run Dijkstra's algorithm
                var (path, totalDistance) = Dijkstra.Dijkstra.FindShortestPath(graph, beginNode, endNode);

                // Output the results
                if (path != null)
                {
                    lines.Add($"Shortest path distance: {totalDistance:F3} meters.");
                    lines.Add("Path:");

                    foreach (var node in path)
                    {
                        lines.Add($"Tray: {node.TrayName} Node ID: {node.Id}, Coordinates: ({node.X:F3}, {node.Y:F3}, {node.Z:F3})");
                    }

                    var uniqueTrays = path
                        .Select(n => n.TrayName)
                        .Where(name => name != null && name != "Begin" && name != "End")
                        .Distinct();

                    lines.Add("Branches involved in the path:");
                    if (beginSecondaryTray)
                    {
                        lines.Add("/SECONDARY");
                    }
                    foreach (var branch in uniqueTrays)
                    {
                        lines.Add(branch);
                    }
                    if (endSecondaryTray)
                    {
                        lines.Add("/SECONDARY");
                    }
                }
                else
                {
                    lines.Add("No path found between the specified nodes.");
                }

                textBoxFrom.Text = $"X: {beginNode.X:F3}, Y: {beginNode.Y:F3}, Z: {beginNode.Z:F3}";
                textBoxTo.Text = $"X: {endNode.X:F3}, Y: {endNode.Y:F3}, Z: {endNode.Z:F3}";

                tbProp.Text = string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
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
            var bcBranches = TraysData.TraysData.GetBCTrays();
            //var bBranches = TraysData.TraysData.GetBTrays();
            var cBranches = TraysData.TraysData.GetCTrays();
            //var dfbBranches = TraysData.TraysData.GetDFBTrays();
            var dfcBranches = TraysData.TraysData.GetDFCTrays();

            // Add all nodes to the sections
            sections["BC"] = bcBranches;
            sections["C"] = cBranches;
            sections["DFC"] = dfcBranches;
            //sections["B"] = bcBranches;
            //sections["DFB"] = dfcBranches;

            _cachedSections = sections;
            return sections;
        }

        private static void ConnectNodesWithinTray(Graph graph, List<Node> trayNodes, double maxDistance)
        {
            for (int i = 0; i < trayNodes.Count; i++)
            {
                for (int j = i + 1; j < trayNodes.Count; j++)
                {
                    double distance = CalculateDistance(trayNodes[i], trayNodes[j]);
                    if (distance <= maxDistance)
                    {
                        graph.AddBidirectionalEdge(trayNodes[i], trayNodes[j], distance);
                    }
                }
            }
        }

        private static void ConnectNodesBetweenTrays(Graph graph, List<Node> trayA, List<Node> trayB, double physicalThreshold, double maxThreshold, double jumpPenalty)
        {
            using (StreamWriter writer = new StreamWriter("Connectons.txt", true))
            {
                foreach (var nodeA in trayA)
                {
                    foreach (var nodeB in trayB)
                    {
                        double distance = CalculateDistance(nodeA, nodeB);
                        if (distance <= physicalThreshold)
                        {
                            // Physically connected
                            graph.AddBidirectionalEdge(nodeA, nodeB, distance);
                            string logMessage = $"Physically connected: {nodeA.TrayName} (ID {nodeA.Id}) <--> {nodeB.TrayName} (ID {nodeB.Id}), Distance: {distance:F3} meters";
                            writer.WriteLine(logMessage);
                        }
                        else if (distance <= maxThreshold)
                        {
                            // Jump between trays
                            double adjustedWeight = distance + jumpPenalty;
                            graph.AddBidirectionalEdge(nodeA, nodeB, adjustedWeight);
                            string logMessage = $"Jump connection: {nodeA.TrayName} (ID {nodeA.Id}) <--> {nodeB.TrayName} (ID {nodeB.Id}), Distance: {distance:F3} meters, Adjusted Weight: {adjustedWeight:F3}";
                            writer.WriteLine(logMessage);
                        }
                        // Do not connect if distance > maxThreshold
                    }
                }
            }
        }
    }
}