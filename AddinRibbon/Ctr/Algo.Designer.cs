namespace AddinRibbon.Ctr
{
    partial class Algo
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tbProp = new System.Windows.Forms.TextBox();
            this.tbToElement = new System.Windows.Forms.TextBox();
            this.lbCatName = new System.Windows.Forms.Label();
            this.tbFromElement = new System.Windows.Forms.TextBox();
            this.cbPause = new System.Windows.Forms.CheckBox();
            this.lbPropName = new System.Windows.Forms.Label();
            this.textBoxFrom = new System.Windows.Forms.TextBox();
            this.textBoxTo = new System.Windows.Forms.TextBox();
            this.btnFindPath = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // tbProp
            // 
            this.tbProp.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbProp.Location = new System.Drawing.Point(49, 313);
            this.tbProp.Margin = new System.Windows.Forms.Padding(0);
            this.tbProp.Multiline = true;
            this.tbProp.Name = "tbProp";
            this.tbProp.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbProp.Size = new System.Drawing.Size(670, 797);
            this.tbProp.TabIndex = 0;
            // 
            // tbToElement
            // 
            this.tbToElement.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tbToElement.Location = new System.Drawing.Point(259, 146);
            this.tbToElement.Margin = new System.Windows.Forms.Padding(0);
            this.tbToElement.Name = "tbToElement";
            this.tbToElement.Size = new System.Drawing.Size(460, 31);
            this.tbToElement.TabIndex = 4;
            // 
            // lbCatName
            // 
            this.lbCatName.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lbCatName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lbCatName.Location = new System.Drawing.Point(49, 58);
            this.lbCatName.Margin = new System.Windows.Forms.Padding(0);
            this.lbCatName.Name = "lbCatName";
            this.lbCatName.Size = new System.Drawing.Size(196, 31);
            this.lbCatName.TabIndex = 7;
            this.lbCatName.Text = "FROM Location";
            this.lbCatName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tbFromElement
            // 
            this.tbFromElement.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tbFromElement.Location = new System.Drawing.Point(259, 58);
            this.tbFromElement.Margin = new System.Windows.Forms.Padding(0);
            this.tbFromElement.Name = "tbFromElement";
            this.tbFromElement.Size = new System.Drawing.Size(460, 31);
            this.tbFromElement.TabIndex = 6;
            // 
            // cbPause
            // 
            this.cbPause.AutoSize = true;
            this.cbPause.Checked = true;
            this.cbPause.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbPause.Dock = System.Windows.Forms.DockStyle.Top;
            this.cbPause.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbPause.Location = new System.Drawing.Point(10, 10);
            this.cbPause.Margin = new System.Windows.Forms.Padding(0);
            this.cbPause.Name = "cbPause";
            this.cbPause.Size = new System.Drawing.Size(780, 29);
            this.cbPause.TabIndex = 8;
            this.cbPause.Text = "Pause";
            this.cbPause.UseVisualStyleBackColor = true;
            // 
            // lbPropName
            // 
            this.lbPropName.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lbPropName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lbPropName.Location = new System.Drawing.Point(49, 146);
            this.lbPropName.Margin = new System.Windows.Forms.Padding(0);
            this.lbPropName.Name = "lbPropName";
            this.lbPropName.Size = new System.Drawing.Size(196, 31);
            this.lbPropName.TabIndex = 5;
            this.lbPropName.Text = "TO Location";
            this.lbPropName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textBoxFrom
            // 
            this.textBoxFrom.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxFrom.Location = new System.Drawing.Point(49, 100);
            this.textBoxFrom.Margin = new System.Windows.Forms.Padding(0);
            this.textBoxFrom.Name = "textBoxFrom";
            this.textBoxFrom.ReadOnly = true;
            this.textBoxFrom.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxFrom.Size = new System.Drawing.Size(670, 31);
            this.textBoxFrom.TabIndex = 9;
            // 
            // textBoxTo
            // 
            this.textBoxTo.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxTo.Location = new System.Drawing.Point(49, 191);
            this.textBoxTo.Margin = new System.Windows.Forms.Padding(0);
            this.textBoxTo.Name = "textBoxTo";
            this.textBoxTo.ReadOnly = true;
            this.textBoxTo.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxTo.Size = new System.Drawing.Size(670, 31);
            this.textBoxTo.TabIndex = 10;
            // 
            // btnFindPath
            // 
            this.btnFindPath.Location = new System.Drawing.Point(49, 239);
            this.btnFindPath.Name = "btnFindPath";
            this.btnFindPath.Size = new System.Drawing.Size(670, 44);
            this.btnFindPath.TabIndex = 11;
            this.btnFindPath.Text = "Find path!";
            this.btnFindPath.UseVisualStyleBackColor = true;
            this.btnFindPath.Click += new System.EventHandler(this.btnFindPath_Click);
            // 
            // Algo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.btnFindPath);
            this.Controls.Add(this.textBoxTo);
            this.Controls.Add(this.textBoxFrom);
            this.Controls.Add(this.cbPause);
            this.Controls.Add(this.lbCatName);
            this.Controls.Add(this.tbFromElement);
            this.Controls.Add(this.lbPropName);
            this.Controls.Add(this.tbToElement);
            this.Controls.Add(this.tbProp);
            this.Margin = new System.Windows.Forms.Padding(10);
            this.MaximumSize = new System.Drawing.Size(800, 1500);
            this.MinimumSize = new System.Drawing.Size(800, 1500);
            this.Name = "Algo";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.Size = new System.Drawing.Size(800, 1500);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbProp;
        private System.Windows.Forms.TextBox tbToElement;
        private System.Windows.Forms.Label lbCatName;
        private System.Windows.Forms.TextBox tbFromElement;
        private System.Windows.Forms.CheckBox cbPause;
        private System.Windows.Forms.Label lbPropName;
        private System.Windows.Forms.TextBox textBoxFrom;
        private System.Windows.Forms.TextBox textBoxTo;
        private System.Windows.Forms.Button btnFindPath;
    }
}
