namespace River.OneMoreAddIn.Commands
{
	partial class SearchAndReplaceSessionDialog
	{
		private System.ComponentModel.IContainer components = null;

		protected override void Dispose(bool disposing)
		{
			if (disposing && components is not null) components.Dispose();
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		private void InitializeComponent()
		{
			var resources = new System.ComponentModel.ComponentResourceManager(typeof(SearchAndReplaceSessionDialog));
			this.pageLabel = new UI.MoreLabel();
			this.statusLabel = new UI.MoreLabel();
			this.previewLabel = new UI.MoreLabel();
			this.previewBox = new UI.MoreRichTextBox();
			this.replacementLabel = new UI.MoreLabel();
			this.replacementBox = new UI.MoreTextBox();
			this.helpLabel = new UI.MoreLabel();
			this.previousButton = new UI.MoreButton();
			this.nextButton = new UI.MoreButton();
			this.refreshButton = new UI.MoreButton();
			this.wrapBox = new UI.MoreCheckBox();
			this.replaceButton = new UI.MoreButton();
			this.replaceAllButton = new UI.MoreButton();
			this.closeButton = new UI.MoreButton();
			var layout = new System.Windows.Forms.TableLayoutPanel();
			var navigation = new System.Windows.Forms.FlowLayoutPanel();
			var actions = new System.Windows.Forms.TableLayoutPanel();
			this.SuspendLayout();
			layout.SuspendLayout();

			layout.ColumnCount = 1;
			layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			layout.RowCount = 9;
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
			layout.Dock = System.Windows.Forms.DockStyle.Fill;

			this.pageLabel.Name = "pageLabel";
			this.pageLabel.AutoSize = true;
			this.pageLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pageLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 9);
			this.statusLabel.Name = "statusLabel";
			this.statusLabel.AutoSize = true;
			this.statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.statusLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 9);
			this.previewLabel.Name = "previewLabel";
			this.previewLabel.AutoSize = true;
			this.previewLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.previewBox.Name = "previewBox";
			this.previewBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.previewBox.ReadOnly = true;
			this.previewBox.HideSelection = false;
			this.previewBox.DetectUrls = false;
			this.previewBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
			this.previewBox.BackColor = System.Drawing.SystemColors.Window;
			this.previewBox.ForeColor = System.Drawing.SystemColors.WindowText;
			this.previewBox.TabIndex = 0;
			this.replacementLabel.Name = "replacementLabel";
			this.replacementLabel.AutoSize = true;
			this.replacementLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.replacementLabel.Margin = new System.Windows.Forms.Padding(3, 9, 3, 3);
			this.replacementBox.Name = "replacementBox";
			this.replacementBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.replacementBox.Multiline = true;
			this.replacementBox.ReadOnly = true;
			this.replacementBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.replacementBox.TabIndex = 1;
			this.helpLabel.Name = "helpLabel";
			this.helpLabel.AutoSize = true;
			this.helpLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.helpLabel.Margin = new System.Windows.Forms.Padding(3, 9, 3, 9);

			navigation.AutoSize = true;
			navigation.Dock = System.Windows.Forms.DockStyle.Fill;
			navigation.WrapContents = true;
			this.previousButton.Name = "previousButton";
			this.previousButton.Size = new System.Drawing.Size(135, 38);
			this.previousButton.TabIndex = 2;
			this.previousButton.Click += new System.EventHandler(this.PreviousMatch);
			this.nextButton.Name = "nextButton";
			this.nextButton.Size = new System.Drawing.Size(135, 38);
			this.nextButton.TabIndex = 3;
			this.nextButton.Click += new System.EventHandler(this.NextMatch);
			this.refreshButton.Name = "refreshButton";
			this.refreshButton.Size = new System.Drawing.Size(175, 38);
			this.refreshButton.TabIndex = 4;
			this.refreshButton.Click += new System.EventHandler(this.RefreshMatches);
			this.wrapBox.Name = "wrapBox";
			this.wrapBox.AutoSize = true;
			this.wrapBox.Checked = true;
			this.wrapBox.Margin = new System.Windows.Forms.Padding(12, 12, 3, 3);
			this.wrapBox.TabIndex = 5;
			navigation.Controls.Add(this.previousButton);
			navigation.Controls.Add(this.nextButton);
			navigation.Controls.Add(this.refreshButton);
			navigation.Controls.Add(this.wrapBox);

			actions.AutoSize = true;
			actions.Dock = System.Windows.Forms.DockStyle.Fill;
			actions.ColumnCount = 4;
			actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
			actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
			actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
			this.replaceButton.Name = "replaceButton";
			this.replaceButton.Size = new System.Drawing.Size(135, 38);
			this.replaceButton.TabIndex = 6;
			this.replaceButton.Click += new System.EventHandler(this.ReplaceOne);
			this.replaceAllButton.Name = "replaceAllButton";
			this.replaceAllButton.Size = new System.Drawing.Size(135, 38);
			this.replaceAllButton.TabIndex = 7;
			this.replaceAllButton.Click += new System.EventHandler(this.ReplaceAll);
			this.closeButton.Name = "closeButton";
			this.closeButton.Size = new System.Drawing.Size(125, 38);
			this.closeButton.TabIndex = 8;
			this.closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.closeButton.Click += new System.EventHandler(this.CloseSession);
			actions.Controls.Add(this.replaceButton, 0, 0);
			actions.Controls.Add(this.replaceAllButton, 1, 0);
			actions.Controls.Add(this.closeButton, 3, 0);

			layout.Controls.Add(this.pageLabel, 0, 0);
			layout.Controls.Add(this.statusLabel, 0, 1);
			layout.Controls.Add(this.previewLabel, 0, 2);
			layout.Controls.Add(this.previewBox, 0, 3);
			layout.Controls.Add(this.replacementLabel, 0, 4);
			layout.Controls.Add(this.replacementBox, 0, 5);
			layout.Controls.Add(this.helpLabel, 0, 6);
			layout.Controls.Add(navigation, 0, 7);
			layout.Controls.Add(actions, 0, 8);
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(780, 570);
			this.MinimumSize = new System.Drawing.Size(720, 530);
			this.Controls.Add(layout);
			this.AcceptButton = this.nextButton;
			this.CancelButton = this.closeButton;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SearchAndReplaceSessionDialog";
			this.Padding = new System.Windows.Forms.Padding(15);
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			this.Text = "Search and Replace";
			layout.ResumeLayout(false);
			layout.PerformLayout();
			this.ResumeLayout(false);
		}

		#endregion

		private UI.MoreLabel pageLabel;
		private UI.MoreLabel statusLabel;
		private UI.MoreLabel previewLabel;
		private UI.MoreRichTextBox previewBox;
		private UI.MoreLabel replacementLabel;
		private UI.MoreTextBox replacementBox;
		private UI.MoreLabel helpLabel;
		private UI.MoreButton previousButton;
		private UI.MoreButton nextButton;
		private UI.MoreButton refreshButton;
		private UI.MoreCheckBox wrapBox;
		private UI.MoreButton replaceButton;
		private UI.MoreButton replaceAllButton;
		private UI.MoreButton closeButton;
	}
}
