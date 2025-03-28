using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DynamicSqlEditor.UI.Controls
{
    public partial class PagingControl : UserControl
    {
        public event EventHandler FirstPageClicked;
        public event EventHandler PreviousPageClicked;
        public event EventHandler NextPageClicked;
        public event EventHandler LastPageClicked;
        public event EventHandler<int> PageSizeChanged;

        private int _currentPage = 1;
        private int _totalPages = 0;
        private int _totalRecords = 0;
        private int _pageSize = 50;

        public PagingControl()
        {
            InitializeComponent();
            pageSizeComboBox.Items.AddRange(new object[] { 10, 25, 50, 100, 250, 500 });
            pageSizeComboBox.SelectedItem = _pageSize; // Set initial selection
            UpdateDisplay();
        }

        [DefaultValue(1)]
        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; UpdateDisplay(); }
        }

        [DefaultValue(0)]
        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; UpdateDisplay(); }
        }

        [DefaultValue(0)]
        public int TotalRecords
        {
            get => _totalRecords;
            set { _totalRecords = value; UpdateDisplay(); }
        }

         [DefaultValue(50)]
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    // Update ComboBox selection if value changes programmatically
                    if (!pageSizeComboBox.Items.Contains(_pageSize))
                    {
                        // Optionally add it if not present, or select closest?
                        // For now, just ensure it's reflected internally.
                    }
                     else
                    {
                        pageSizeComboBox.SelectedItem = _pageSize;
                    }
                    UpdateDisplay();
                    // Note: PageSizeChanged event is fired by ComboBox event handler
                }
            }
        }

        public void UpdatePagingInfo(int currentPage, int totalPages, int totalRecords, int pageSize)
        {
            _currentPage = currentPage;
            _totalPages = totalPages;
            _totalRecords = totalRecords;
            _pageSize = pageSize;

            // Update ComboBox selection without firing event
            pageSizeComboBox.SelectedIndexChanged -= PageSizeComboBox_SelectedIndexChanged;
            if (!pageSizeComboBox.Items.Contains(_pageSize))
            {
                 // Handle case where current page size isn't in the list (e.g., from config)
                 // Maybe add it temporarily? Or just display it? For now, do nothing.
                 // pageSizeComboBox.Text = _pageSize.ToString(); // Display if not in list
            }
            else
            {
                 pageSizeComboBox.SelectedItem = _pageSize;
            }
            pageSizeComboBox.SelectedIndexChanged += PageSizeComboBox_SelectedIndexChanged;

            UpdateDisplay();
        }


        private void UpdateDisplay()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateDisplay));
                return;
            }

            pageInfoLabel.Text = $"Page {_currentPage} of {_totalPages} ({_totalRecords} records)";
            btnFirst.Enabled = _currentPage > 1;
            btnPrevious.Enabled = _currentPage > 1;
            btnNext.Enabled = _currentPage < _totalPages;
            btnLast.Enabled = _currentPage < _totalPages;
        }

        private void BtnFirst_Click(object sender, EventArgs e) => FirstPageClicked?.Invoke(this, EventArgs.Empty);
        private void BtnPrevious_Click(object sender, EventArgs e) => PreviousPageClicked?.Invoke(this, EventArgs.Empty);
        private void BtnNext_Click(object sender, EventArgs e) => NextPageClicked?.Invoke(this, EventArgs.Empty);
        private void BtnLast_Click(object sender, EventArgs e) => LastPageClicked?.Invoke(this, EventArgs.Empty);

        private void PageSizeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (pageSizeComboBox.SelectedItem != null && int.TryParse(pageSizeComboBox.SelectedItem.ToString(), out int newSize))
            {
                if (_pageSize != newSize)
                {
                    _pageSize = newSize;
                    PageSizeChanged?.Invoke(this, newSize);
                    // UpdateDisplay will be called after data reloads
                }
            }
        }

        #region Designer Code
        private System.Windows.Forms.Button btnFirst;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Label pageInfoLabel;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnLast;
        private System.Windows.Forms.ComboBox pageSizeComboBox;
        private System.Windows.Forms.Label pageSizeLabel;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;

        private void InitializeComponent()
        {
            this.btnFirst = new System.Windows.Forms.Button();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.pageInfoLabel = new System.Windows.Forms.Label();
            this.btnNext = new System.Windows.Forms.Button();
            this.btnLast = new System.Windows.Forms.Button();
            this.pageSizeComboBox = new System.Windows.Forms.ComboBox();
            this.pageSizeLabel = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            //
            // btnFirst
            //
            this.btnFirst.Location = new System.Drawing.Point(3, 3);
            this.btnFirst.Name = "btnFirst";
            this.btnFirst.Size = new System.Drawing.Size(30, 23);
            this.btnFirst.TabIndex = 0;
            this.btnFirst.Text = "|<";
            this.btnFirst.UseVisualStyleBackColor = true;
            this.btnFirst.Click += new System.EventHandler(this.BtnFirst_Click);
            //
            // btnPrevious
            //
            this.btnPrevious.Location = new System.Drawing.Point(39, 3);
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.Size = new System.Drawing.Size(30, 23);
            this.btnPrevious.TabIndex = 1;
            this.btnPrevious.Text = "<";
            this.btnPrevious.UseVisualStyleBackColor = true;
            this.btnPrevious.Click += new System.EventHandler(this.BtnPrevious_Click);
            //
            // pageInfoLabel
            //
            this.pageInfoLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pageInfoLabel.AutoSize = true;
            this.pageInfoLabel.Location = new System.Drawing.Point(75, 8);
            this.pageInfoLabel.Name = "pageInfoLabel";
            this.pageInfoLabel.Size = new System.Drawing.Size(115, 13);
            this.pageInfoLabel.TabIndex = 2;
            this.pageInfoLabel.Text = "Page 1 of 1 (0 records)";
            this.pageInfoLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnNext
            //
            this.btnNext.Location = new System.Drawing.Point(196, 3);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(30, 23);
            this.btnNext.TabIndex = 3;
            this.btnNext.Text = ">";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += new System.EventHandler(this.BtnNext_Click);
            //
            // btnLast
            //
            this.btnLast.Location = new System.Drawing.Point(232, 3);
            this.btnLast.Name = "btnLast";
            this.btnLast.Size = new System.Drawing.Size(30, 23);
            this.btnLast.TabIndex = 4;
            this.btnLast.Text = ">|";
            this.btnLast.UseVisualStyleBackColor = true;
            this.btnLast.Click += new System.EventHandler(this.BtnLast_Click);
            //
            // pageSizeComboBox
            //
            this.pageSizeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pageSizeComboBox.FormattingEnabled = true;
            this.pageSizeComboBox.Location = new System.Drawing.Point(333, 4); // Adjusted margin
            this.pageSizeComboBox.Margin = new System.Windows.Forms.Padding(10, 4, 3, 3); // Add left margin
            this.pageSizeComboBox.Name = "pageSizeComboBox";
            this.pageSizeComboBox.Size = new System.Drawing.Size(60, 21);
            this.pageSizeComboBox.TabIndex = 6;
            this.pageSizeComboBox.SelectedIndexChanged += new System.EventHandler(this.PageSizeComboBox_SelectedIndexChanged);
            //
            // pageSizeLabel
            //
            this.pageSizeLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.pageSizeLabel.AutoSize = true;
            this.pageSizeLabel.Location = new System.Drawing.Point(275, 8); // Adjusted margin
            this.pageSizeLabel.Margin = new System.Windows.Forms.Padding(10, 0, 3, 0); // Add left margin
            this.pageSizeLabel.Name = "pageSizeLabel";
            this.pageSizeLabel.Size = new System.Drawing.Size(58, 13);
            this.pageSizeLabel.TabIndex = 5;
            this.pageSizeLabel.Text = "Page Size:";
            //
            // flowLayoutPanel1
            //
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanel1.Controls.Add(this.btnFirst);
            this.flowLayoutPanel1.Controls.Add(this.btnPrevious);
            this.flowLayoutPanel1.Controls.Add(this.pageInfoLabel);
            this.flowLayoutPanel1.Controls.Add(this.btnNext);
            this.flowLayoutPanel1.Controls.Add(this.btnLast);
            this.flowLayoutPanel1.Controls.Add(this.pageSizeLabel);
            this.flowLayoutPanel1.Controls.Add(this.pageSizeComboBox);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill; // Fill the UserControl
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(400, 29); // Example size, will adjust
            this.flowLayoutPanel1.TabIndex = 7;
            this.flowLayoutPanel1.WrapContents = false; // Prevent wrapping
            //
            // PagingControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "PagingControl";
            this.Size = new System.Drawing.Size(400, 29); // Example size
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion
    }
}