namespace DynamicSqlEditor.UI
{
    partial class DataViewForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Panel filterPanel;
        private System.Windows.Forms.DataGridView mainDataGridView;
        private System.Windows.Forms.Panel pagingPanel;
        private System.Windows.Forms.TabControl relatedDataTabControl;
        private System.Windows.Forms.TabPage detailEditorTab;
        private System.Windows.Forms.Panel detailActionPanel;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Button deleteButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button newButton;
        private System.Windows.Forms.Panel detailPanel; // Scrollable panel for fields
        private System.Windows.Forms.Panel actionButtonPanel; // For custom buttons
        private Controls.PagingControl pagingControl; // Custom Paging Control

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.topPanel = new System.Windows.Forms.Panel();
            this.mainDataGridView = new System.Windows.Forms.DataGridView();
            this.pagingPanel = new System.Windows.Forms.Panel();
            this.pagingControl = new DynamicSqlEditor.UI.Controls.PagingControl();
            this.filterPanel = new System.Windows.Forms.Panel();
            this.relatedDataTabControl = new System.Windows.Forms.TabControl();
            this.detailEditorTab = new System.Windows.Forms.TabPage();
            this.detailPanel = new System.Windows.Forms.Panel();
            this.detailActionPanel = new System.Windows.Forms.Panel();
            this.refreshButton = new System.Windows.Forms.Button();
            this.deleteButton = new System.Windows.Forms.Button();
            this.saveButton = new System.Windows.Forms.Button();
            this.newButton = new System.Windows.Forms.Button();
            this.actionButtonPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mainDataGridView)).BeginInit();
            this.pagingPanel.SuspendLayout();
            this.relatedDataTabControl.SuspendLayout();
            this.detailEditorTab.SuspendLayout();
            this.detailActionPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // splitContainer
            //
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // splitContainer.Panel1
            //
            this.splitContainer.Panel1.Controls.Add(this.topPanel);
            //
            // splitContainer.Panel2
            //
            this.splitContainer.Panel2.Controls.Add(this.relatedDataTabControl);
            this.splitContainer.Panel2.Controls.Add(this.actionButtonPanel);
            this.splitContainer.Size = new System.Drawing.Size(784, 561);
            this.splitContainer.SplitterDistance = 280;
            this.splitContainer.TabIndex = 0;
            //
            // topPanel
            //
            this.topPanel.Controls.Add(this.mainDataGridView);
            this.topPanel.Controls.Add(this.pagingPanel);
            this.topPanel.Controls.Add(this.filterPanel);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Size = new System.Drawing.Size(784, 280);
            this.topPanel.TabIndex = 0;
            //
            // mainDataGridView
            //
            this.mainDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.mainDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainDataGridView.Location = new System.Drawing.Point(0, 35); // Height of filterPanel
            this.mainDataGridView.Name = "mainDataGridView";
            this.mainDataGridView.Size = new System.Drawing.Size(784, 210); // Height of topPanel - filterPanel - pagingPanel
            this.mainDataGridView.TabIndex = 1;
            //
            // pagingPanel
            //
            this.pagingPanel.Controls.Add(this.pagingControl);
            this.pagingPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pagingPanel.Location = new System.Drawing.Point(0, 245); // Positioned at bottom
            this.pagingPanel.Name = "pagingPanel";
            this.pagingPanel.Size = new System.Drawing.Size(784, 35);
            this.pagingPanel.TabIndex = 2;
            //
            // pagingControl
            //
            this.pagingControl.AutoSize = true;
            this.pagingControl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pagingControl.CurrentPage = 1;
            this.pagingControl.Dock = System.Windows.Forms.DockStyle.Fill; // Fill the panel
            this.pagingControl.Location = new System.Drawing.Point(0, 0);
            this.pagingControl.Margin = new System.Windows.Forms.Padding(0);
            this.pagingControl.Name = "pagingControl";
            this.pagingControl.PageSize = 50;
            this.pagingControl.Size = new System.Drawing.Size(784, 35);
            this.pagingControl.TabIndex = 0;
            this.pagingControl.TotalPages = 0;
            this.pagingControl.TotalRecords = 0;
            //
            // filterPanel
            //
            this.filterPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.filterPanel.Location = new System.Drawing.Point(0, 0);
            this.filterPanel.Name = "filterPanel";
            this.filterPanel.Padding = new System.Windows.Forms.Padding(5);
            this.filterPanel.Size = new System.Drawing.Size(784, 35);
            this.filterPanel.TabIndex = 0;
            //
            // relatedDataTabControl
            //
            this.relatedDataTabControl.Controls.Add(this.detailEditorTab);
            this.relatedDataTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.relatedDataTabControl.Location = new System.Drawing.Point(0, 0);
            this.relatedDataTabControl.Name = "relatedDataTabControl";
            this.relatedDataTabControl.SelectedIndex = 0;
            this.relatedDataTabControl.Size = new System.Drawing.Size(784, 242); // Fill Panel2 above actionButtonPanel
            this.relatedDataTabControl.TabIndex = 0;
            //
            // detailEditorTab
            //
            this.detailEditorTab.Controls.Add(this.detailPanel);
            this.detailEditorTab.Controls.Add(this.detailActionPanel);
            this.detailEditorTab.Location = new System.Drawing.Point(4, 22);
            this.detailEditorTab.Name = "detailEditorTab";
            this.detailEditorTab.Padding = new System.Windows.Forms.Padding(3);
            this.detailEditorTab.Size = new System.Drawing.Size(776, 216); // Adjusted size
            this.detailEditorTab.TabIndex = 0;
            this.detailEditorTab.Text = "Details";
            this.detailEditorTab.UseVisualStyleBackColor = true;
            //
            // detailPanel
            //
            this.detailPanel.AutoScroll = true;
            this.detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detailPanel.Location = new System.Drawing.Point(3, 3);
            this.detailPanel.Name = "detailPanel";
            this.detailPanel.Padding = new System.Windows.Forms.Padding(5);
            this.detailPanel.Size = new System.Drawing.Size(770, 175); // Fill above detailActionPanel
            this.detailPanel.TabIndex = 1;
            //
            // detailActionPanel
            //
            this.detailActionPanel.Controls.Add(this.refreshButton);
            this.detailActionPanel.Controls.Add(this.deleteButton);
            this.detailActionPanel.Controls.Add(this.saveButton);
            this.detailActionPanel.Controls.Add(this.newButton);
            this.detailActionPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.detailActionPanel.Location = new System.Drawing.Point(3, 178); // Positioned at bottom of tab
            this.detailActionPanel.Name = "detailActionPanel";
            this.detailActionPanel.Size = new System.Drawing.Size(770, 35);
            this.detailActionPanel.TabIndex = 0;
            //
            // refreshButton
            //
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.Location = new System.Drawing.Point(689, 6);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(75, 23);
            this.refreshButton.TabIndex = 3;
            this.refreshButton.Text = "&Refresh";
            this.refreshButton.UseVisualStyleBackColor = true;
            //
            // deleteButton
            //
            this.deleteButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteButton.Location = new System.Drawing.Point(608, 6);
            this.deleteButton.Name = "deleteButton";
            this.deleteButton.Size = new System.Drawing.Size(75, 23);
            this.deleteButton.TabIndex = 2;
            this.deleteButton.Text = "&Delete";
            this.deleteButton.UseVisualStyleBackColor = true;
            //
            // saveButton
            //
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.Enabled = false;
            this.saveButton.Location = new System.Drawing.Point(527, 6);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "&Save";
            this.saveButton.UseVisualStyleBackColor = true;
            //
            // newButton
            //
            this.newButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.newButton.Location = new System.Drawing.Point(446, 6);
            this.newButton.Name = "newButton";
            this.newButton.Size = new System.Drawing.Size(75, 23);
            this.newButton.TabIndex = 0;
            this.newButton.Text = "&New";
            this.newButton.UseVisualStyleBackColor = true;
            //
            // actionButtonPanel
            //
            this.actionButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.actionButtonPanel.Location = new System.Drawing.Point(0, 242); // Below TabControl
            this.actionButtonPanel.Name = "actionButtonPanel";
            this.actionButtonPanel.Padding = new System.Windows.Forms.Padding(5);
            this.actionButtonPanel.Size = new System.Drawing.Size(784, 35);
            this.actionButtonPanel.TabIndex = 1;
            //
            // DataViewForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.splitContainer);
            this.Name = "DataViewForm";
            this.Text = "Data View"; // Will be set dynamically
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.topPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainDataGridView)).EndInit();
            this.pagingPanel.ResumeLayout(false);
            this.pagingPanel.PerformLayout();
            this.relatedDataTabControl.ResumeLayout(false);
            this.detailEditorTab.ResumeLayout(false);
            this.detailActionPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }
    }
}