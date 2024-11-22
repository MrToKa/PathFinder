namespace AddinRibbon.Ctr
{
    partial class UcProperties
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
            this.btnFindPath = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // tbProp
            // 
            this.tbProp.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tbProp.Location = new System.Drawing.Point(10, 73);
            this.tbProp.Margin = new System.Windows.Forms.Padding(0);
            this.tbProp.Multiline = true;
            this.tbProp.Name = "tbProp";
            this.tbProp.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbProp.Size = new System.Drawing.Size(710, 1038);
            this.tbProp.TabIndex = 0;
            // 
            // btnFindPath
            // 
            this.btnFindPath.Location = new System.Drawing.Point(10, 13);
            this.btnFindPath.Name = "btnFindPath";
            this.btnFindPath.Size = new System.Drawing.Size(710, 44);
            this.btnFindPath.TabIndex = 11;
            this.btnFindPath.Text = "Get Data";
            this.btnFindPath.UseVisualStyleBackColor = true;
            this.btnFindPath.Click += new System.EventHandler(this.BtnFindPath_Click);
            // 
            // UcProperties
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.btnFindPath);
            this.Controls.Add(this.tbProp);
            this.Margin = new System.Windows.Forms.Padding(10);
            this.MaximumSize = new System.Drawing.Size(800, 1500);
            this.MinimumSize = new System.Drawing.Size(800, 1500);
            this.Name = "UcProperties";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.Size = new System.Drawing.Size(800, 1500);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbProp;
        private System.Windows.Forms.Button btnFindPath;
    }
}
