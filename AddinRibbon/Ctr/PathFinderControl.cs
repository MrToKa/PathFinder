using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AddinRibbon.Routing;
using AddinRibbon.Services;
using Autodesk.Navisworks.Api;

namespace AddinRibbon.Ctr
{
    public sealed class PathFinderControl : UserControl
    {
        private readonly RoutingSession session = new RoutingSession();
        private readonly PathVisualization visualization = new PathVisualization();
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly DataGridView rules = new DataGridView();
        private readonly CheckBox mv = new CheckBox { Text = "MV", AutoSize = true };
        private readonly CheckBox lv = new CheckBox { Text = "LV", AutoSize = true, Checked = true };
        private readonly CheckBox control = new CheckBox { Text = "Control", AutoSize = true, Checked = true };
        private readonly TextBox from = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox to = new TextBox { Dock = DockStyle.Fill };
        private readonly ComboBox cableType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        private readonly NumericUpDown gap = new NumericUpDown { Minimum = 0.01m, Maximum = 1.30m, DecimalPlaces = 2, Increment = 0.05m, Value = 0.25m, Width = 120 };
        private readonly NumericUpDown secondaryDistance = new NumericUpDown { Minimum = 0m, Maximum = 1000m, DecimalPlaces = 2, Increment = 0.05m, Value = 2m, Width = 120 };
        private readonly Label secondaryExplanation = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly CheckBox pause = new CheckBox { Text = "Pause automatic calculation", Checked = true, AutoSize = true };
        private readonly TextBox output = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 45, Padding = new Padding(6), Text = "Manual mode. Add routes, then enter From and To." };
        private readonly Button add = MakeButton("Add / update selection");
        private readonly Button remove = MakeButton("Remove rule");
        private readonly Button calculate = MakeButton("Calculate path");
        private readonly Button show = MakeButton("Show path");
        private readonly Button reverse = MakeButton("Reverse");
        private readonly Button restore = MakeButton("Restore view");
        private readonly Button cancel = MakeButton("Cancel");
        private readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer { Interval = 700 };
        private CancellationTokenSource operation;
        private ModelItem pickedFrom, pickedTo, resolvedFrom, resolvedTo;
        private RouteResult result;
        private bool busy, updatingRules, swapping;
        private int resultRevision;
        private int knownModelRevision;
        private RoutePoint capturedFrom, capturedTo;

        public PathFinderControl()
        {
            SuspendLayout();
            Name = "PathFinderControl";
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = false;
            Size = new Size(620, 650);
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9);
            BuildRoutesTab();
            BuildPathTab();
            Controls.Add(tabs);
            Controls.Add(status);
            session.Changed += SessionChanged;
            knownModelRevision = session.ModelRevision;
            from.TextChanged += InputChanged;
            to.TextChanged += InputChanged;
            cableType.SelectedIndexChanged += InputChanged;
            gap.ValueChanged += InputChanged;
            secondaryDistance.ValueChanged += SecondaryDistanceChanged;
            pause.CheckedChanged += (s, e) => { debounce.Stop(); if (!pause.Checked) ScheduleCalculation(); };
            debounce.Tick += async (s, e) => { debounce.Stop(); if (!pause.Checked) await CalculateAsync(); };
            add.Click += async (s, e) => await AssignAsync();
            remove.Click += (s, e) => RemoveRules();
            calculate.Click += async (s, e) => await CalculateAsync();
            show.Click += (s, e) => ShowPath();
            restore.Click += (s, e) => RestoreView();
            reverse.Click += (s, e) => ReversePath();
            cancel.Click += (s, e) => operation?.Cancel();
            UpdateButtons();
            ResumeLayout(true);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            // The native dock host resets Dock while attaching the returned control.
            // Apply it after attachment so the pane follows the host client area.
            if (Parent != null && !IsDisposed) Dock = DockStyle.Fill;
        }

        private static Button MakeButton(string text) { return new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(3) }; }
        private static FlowLayoutPanel Flow() { return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(3) }; }
        private static FlowLayoutPanel LabeledOption(string caption, Control input)
        {
            var pair = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 8, 0) };
            pair.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left });
            pair.Controls.Add(input);
            return pair;
        }
        private static TableLayoutPanel CreateLayout(int rows)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.SizeChanged += (s, e) =>
            {
                int width = Math.Max(1, table.ClientSize.Width - table.Padding.Horizontal - 8);
                foreach (Control child in table.Controls)
                    if (child is Label || child is FlowLayoutPanel) child.MaximumSize = new Size(width, 0);
            };
            for (int i = 0; i < rows - 1; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return table;
        }
        private void BuildRoutesTab()
        {
            var page = new TabPage("Routes"); var layout = CreateLayout(4);
            layout.Controls.Add(new Label { Text = "Select route objects in the Selection Tree, choose allowed cable types, then add them. All deepest geometry children are included.", AutoSize = true, MaximumSize = new Size(900, 0), Dock = DockStyle.Fill }, 0, 0);
            var commands = Flow(); commands.Controls.AddRange(new Control[] { mv, lv, control, add, remove });
            layout.Controls.Add(commands, 0, 1);
            layout.Controls.Add(new Label { Text = "Edit MV / LV / Control per rule below. For overlapping objects the most recently added rule wins. Rules clear when model geometry changes.", AutoSize = true, Dock = DockStyle.Fill }, 0, 2);
            rules.Dock = DockStyle.Fill; rules.AllowUserToAddRows = false; rules.AllowUserToDeleteRows = false;
            rules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; rules.RowHeadersVisible = false;
            rules.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            rules.Columns.Add(new DataGridViewTextBoxColumn { Name = "Object", HeaderText = "Route object", ReadOnly = true, FillWeight = 220 });
            rules.Columns.Add(new DataGridViewTextBoxColumn { Name = "Leaves", HeaderText = "Leaves", ReadOnly = true, FillWeight = 50 });
            foreach (string type in new[] { "MV", "LV", "Control" }) rules.Columns.Add(new DataGridViewCheckBoxColumn { Name = type, HeaderText = type, FillWeight = 50 });
            rules.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            rules.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            rules.Columns[0].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            foreach (DataGridViewColumn column in rules.Columns)
            {
                column.MinimumWidth = TextRenderer.MeasureText(column.HeaderText, Font).Width + 26;
                if (column.Index > 0) column.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            }
            rules.CurrentCellDirtyStateChanged += (s, e) => { if (rules.IsCurrentCellDirty) rules.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            rules.CellValueChanged += RuleEdited;
            layout.Controls.Add(rules, 0, 3); page.Controls.Add(layout); tabs.TabPages.Add(page);
        }
        private void BuildPathTab()
        {
            var page = new TabPage("Path"); var layout = CreateLayout(6);
            var endpoints = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, AutoSize = true };
            endpoints.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); endpoints.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); endpoints.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var useFrom = MakeButton("Use selection"); var useTo = MakeButton("Use selection");
            endpoints.Controls.Add(new Label { Text = "From", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); endpoints.Controls.Add(from, 1, 0); endpoints.Controls.Add(useFrom, 2, 0);
            endpoints.Controls.Add(new Label { Text = "To", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); endpoints.Controls.Add(to, 1, 1); endpoints.Controls.Add(useTo, 2, 1);
            useFrom.Click += (s, e) => Pick(true); useTo.Click += (s, e) => Pick(false);
            layout.Controls.Add(endpoints, 0, 0);
            cableType.Items.AddRange(new object[] { "MV", "LV", "Control" }); cableType.SelectedIndex = 1;
            var options = Flow(); options.Controls.AddRange(new Control[] { LabeledOption("Cable type", cableType), LabeledOption("Connection gap (m)", gap), LabeledOption("SECONDARY distance (m)", secondaryDistance) });
            layout.Controls.Add(options, 0, 1);
            layout.Controls.Add(pause, 0, 2);
            var commands = Flow(); commands.Controls.AddRange(new Control[] { calculate, show, reverse, restore, cancel }); layout.Controls.Add(commands, 0, 3);
            UpdateSecondaryExplanation();
            layout.Controls.Add(secondaryExplanation, 0, 4);
            layout.Controls.Add(output, 0, 5); page.Controls.Add(layout); tabs.TabPages.Add(page);
        }

        private void UpdateSecondaryExplanation()
        {
            secondaryExplanation.Text = "SECONDARY: endpoint centre is farther than " + secondaryDistance.Value.ToString("F2")
                + " m from an allowed route centreline. Lengths are approximate.";
        }

        private void SecondaryDistanceChanged(object sender, EventArgs e)
        {
            // DecimalPlaces formats text but does not round a manually entered value.
            decimal rounded = decimal.Round(secondaryDistance.Value, 2, MidpointRounding.AwayFromZero);
            if (secondaryDistance.Value != rounded) { secondaryDistance.Value = rounded; return; }
            UpdateSecondaryExplanation();
            InputChanged(sender, e);
        }

        private void RefreshRules()
        {
            updatingRules = true;
            try
            {
                rules.Rows.Clear();
                foreach (var a in session.Assignments)
                {
                    int row = rules.Rows.Add(a.Name, a.LeafCount, (a.Categories & CableCategory.MV) != 0, (a.Categories & CableCategory.LV) != 0, (a.Categories & CableCategory.Control) != 0);
                    rules.Rows[row].Tag = a;
                }
            }
            finally { updatingRules = false; }
        }
        private void RuleEdited(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingRules || e.RowIndex < 0 || e.ColumnIndex < 2) return;
            var row = rules.Rows[e.RowIndex]; var assignment = row.Tag as RouteAssignment;
            if (assignment == null) return;
            assignment.Categories = (Convert.ToBoolean(row.Cells[2].Value) ? CableCategory.MV : CableCategory.None)
                | (Convert.ToBoolean(row.Cells[3].Value) ? CableCategory.LV : CableCategory.None)
                | (Convert.ToBoolean(row.Cells[4].Value) ? CableCategory.Control : CableCategory.None);
            session.NotifyChanged();
        }
        private void RemoveRules()
        {
            foreach (DataGridViewRow row in rules.SelectedRows) session.Assignments.Remove((RouteAssignment)row.Tag);
            session.NotifyChanged();
        }
        private async Task AssignAsync()
        {
            if (busy) return;
            StartOperation("Reading selected route children...");
            try
            {
                var category = (mv.Checked ? CableCategory.MV : CableCategory.None) | (lv.Checked ? CableCategory.LV : CableCategory.None) | (control.Checked ? CableCategory.Control : CableCategory.None);
                int leaves = await session.AssignSelectionAsync(category, operation.Token);
                if (!IsDisposed) status.Text = "Added / updated " + leaves + " geometry leaves. Ready to calculate.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Route capture cancelled."; }
            catch (Exception e) { if (!IsDisposed) status.Text = e.Message; }
            finally { FinishOperation(); if (!IsDisposed) ScheduleCalculation(); }
        }
        private void Pick(bool isFrom)
        {
            if (busy) return;
            var selected = session.Document?.CurrentSelection.SelectedItems;
            if (selected == null || selected.Count != 1) { status.Text = "Select exactly one From / To object in the Selection Tree."; return; }
            var item = selected.First();
            if (!RoutingSession.IsVisibleEndpoint(item)) { status.Text = "Select a visible From / To object with geometry."; return; }
            string name = string.IsNullOrWhiteSpace(item.DisplayName) ? RoutingSession.SelectedUnnamedObjectLabel : item.DisplayName;
            if (isFrom) { from.Text = name; pickedFrom = item; }
            else { to.Text = name; pickedTo = item; }
            InvalidateResult(); ScheduleCalculation();
        }
        private void InputChanged(object sender, EventArgs e)
        {
            if (swapping) return;
            if (sender == from) pickedFrom = null;
            if (sender == to) pickedTo = null;
            InvalidateResult(); ScheduleCalculation();
        }
        private void ScheduleCalculation()
        {
            debounce.Stop();
            if (!pause.Checked && !busy && session.Assignments.Count > 0 && !string.IsNullOrWhiteSpace(from.Text) && !string.IsNullOrWhiteSpace(to.Text)) debounce.Start();
        }
        private void SessionChanged(object sender, EventArgs e)
        {
            // Assignment commits also raise this event; cancel only pending work on a model/rule change.
            if (busy) operation?.Cancel();
            if (knownModelRevision != session.ModelRevision)
            {
                pickedFrom = pickedTo = null;
                knownModelRevision = session.ModelRevision;
            }
            InvalidateResult(); RefreshRules();
            status.Text = session.Assignments.Count == 0 ? "Model changed. Add route objects again." : "Route rules or visibility changed. Calculate the path using visible objects.";
            ScheduleCalculation();
        }
        private void InvalidateResult()
        {
            result = null; resolvedFrom = resolvedTo = null; output.Clear();
            RestoreView(); UpdateButtons();
        }

        private async Task CalculateAsync()
        {
            if (busy) return;
            debounce.Stop(); InvalidateResult(); StartOperation("Resolving objects and reading route geometry...");
            int revision = session.Revision;
            try
            {
                var token = operation.Token;
                var fromItem = await session.ResolveAsync(from.Text, pickedFrom, token);
                var toItem = await session.ResolveAsync(to.Text, pickedTo, token);
                var trays = await session.CaptureSegmentsAsync(token);
                var fromPoint = session.CenterInMeters(fromItem); var toPoint = session.CenterInMeters(toItem);
                var category = (CableCategory)Enum.Parse(typeof(CableCategory), (string)cableType.SelectedItem);
                var options = new RoutingOptions { ConnectionToleranceMeters = (double)gap.Value, SecondaryDistanceMeters = (double)secondaryDistance.Value };
                status.Text = "Calculating path through " + trays.Count + " geometry leaves...";
                var calculated = await Task.Run(() => new RouteCalculator().Calculate(trays, fromPoint, toPoint, category, options, token), token);
                token.ThrowIfCancellationRequested();
                if (revision != session.Revision || IsDisposed) return;
                result = calculated; resolvedFrom = fromItem; resolvedTo = toItem; resultRevision = revision;
                capturedFrom = fromPoint; capturedTo = toPoint;
                RenderResult();
                status.Text = result.Success ? "Path calculated. Show path applies colours and 95% transparency." : result.Message;
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Calculation cancelled."; }
            catch (Exception e) { if (!IsDisposed) { output.Text = e.Message; status.Text = e.Message; } }
            finally { FinishOperation(); }
        }
        private void RenderResult()
        {
            if (result == null) return;
            output.Text = result.Success
                ? result.RouteText + Environment.NewLine + Environment.NewLine + "Approximate length: " + result.LengthMeters.ToString("F3") + " m" + Environment.NewLine
                    + "From to allowed route: " + result.FromDistanceMeters.ToString("F3") + " m" + Environment.NewLine
                    + "To to allowed route: " + result.ToDistanceMeters.ToString("F3") + " m" + Environment.NewLine
                    + "Connection gaps: " + result.ConnectionGapCount + Environment.NewLine
                    + "Connection gap length: " + result.ConnectionGapLengthMeters.ToString("F3") + " m" + Environment.NewLine
                    + "Geometry leaves used: " + result.SegmentIds.Distinct().Count()
                    + (session.FallbackBends > 0 ? Environment.NewLine + "Geometry review: " + session.FallbackBends + " bend(s) in the selected network use a straight approximation." : "")
                : result.Message;
        }
        private void ShowPath()
        {
            if (result == null || !result.Success || resultRevision != session.Revision || busy) return;
            try
            {
                if (!session.AreCapturedSegmentsCurrent() || !RoutingSession.IsVisible(resolvedFrom) || !RoutingSession.IsVisible(resolvedTo)
                    || session.CenterInMeters(resolvedFrom).DistanceTo(capturedFrom) > 1e-9
                    || session.CenterInMeters(resolvedTo).DistanceTo(capturedTo) > 1e-9)
                {
                    InvalidateResult();
                    status.Text = "Object geometry changed. Calculate the path again.";
                    return;
                }
                visualization.Show(session.Document, result.SegmentIds.Select(id => session.SegmentItems[id]), resolvedFrom, resolvedTo);
                RoutePathOverlay.Show(session.Document, result.PathPoints);
                status.Text = "Cable line: yellow. From: green. To: orange. Trays: blue. Everything else: 95% transparency.";
            }
            catch (Exception e) { RestoreView(); status.Text = e.Message; }
            UpdateButtons();
        }
        private void RestoreView()
        {
            RoutePathOverlay.Clear();
            try { visualization.Restore(); }
            catch (Exception e) { status.Text = "Could not restore view: " + e.Message; }
            UpdateButtons();
        }
        private void ReversePath()
        {
            if (busy) return;
            bool wasShown = visualization.IsShown;
            swapping = true;
            try
            {
                string text = from.Text; from.Text = to.Text; to.Text = text;
                var picked = pickedFrom; pickedFrom = pickedTo; pickedTo = picked;
                var resolved = resolvedFrom; resolvedFrom = resolvedTo; resolvedTo = resolved;
                var point = capturedFrom; capturedFrom = capturedTo; capturedTo = point;
                if (result != null && result.Success) { result = result.Reverse(); RenderResult(); }
            }
            finally { swapping = false; }
            if (wasShown) ShowPath();
            else if (result == null) ScheduleCalculation();
        }
        private void StartOperation(string message)
        {
            operation = new CancellationTokenSource(); busy = true; status.Text = message; UpdateButtons();
        }
        private void FinishOperation()
        {
            operation?.Dispose(); operation = null; busy = false;
            if (!IsDisposed) UpdateButtons();
        }
        private void UpdateButtons()
        {
            add.Enabled = remove.Enabled = calculate.Enabled = reverse.Enabled = !busy;
            from.Enabled = to.Enabled = cableType.Enabled = gap.Enabled = secondaryDistance.Enabled = rules.Enabled = !busy;
            cancel.Enabled = busy; show.Enabled = !busy && result != null && result.Success;
            restore.Enabled = !busy && visualization.IsShown;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                debounce.Stop(); debounce.Dispose(); operation?.Cancel();
                session.Changed -= SessionChanged; session.Dispose(); RoutePathOverlay.Clear(); visualization.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
