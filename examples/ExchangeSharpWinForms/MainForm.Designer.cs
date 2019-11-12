namespace ExchangeSharpWinForms
{
    partial class MainForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textTickersResult = new System.Windows.Forms.TextBox();
            this.cmbExchange = new System.Windows.Forms.ComboBox();
            this.btnGetTickers = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textTickersResult
            // 
            this.textTickersResult.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textTickersResult.Font = new System.Drawing.Font("Consolas", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textTickersResult.Location = new System.Drawing.Point(13, 69);
            this.textTickersResult.Multiline = true;
            this.textTickersResult.Name = "textTickersResult";
            this.textTickersResult.ReadOnly = true;
            this.textTickersResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textTickersResult.Size = new System.Drawing.Size(1229, 648);
            this.textTickersResult.TabIndex = 1;
            this.textTickersResult.WordWrap = false;
            // 
            // cmbExchange
            // 
            this.cmbExchange.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbExchange.Font = new System.Drawing.Font("Consolas", 7.875F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmbExchange.FormattingEnabled = true;
            this.cmbExchange.Location = new System.Drawing.Point(278, 17);
            this.cmbExchange.Name = "cmbExchange";
            this.cmbExchange.Size = new System.Drawing.Size(364, 32);
            this.cmbExchange.TabIndex = 2;
            this.cmbExchange.SelectedIndexChanged += new System.EventHandler(this.cmbExchange_SelectedIndexChanged);
            // 
            // btnGetTickers
            // 
            this.btnGetTickers.Location = new System.Drawing.Point(13, 13);
            this.btnGetTickers.Name = "btnGetTickers";
            this.btnGetTickers.Size = new System.Drawing.Size(259, 49);
            this.btnGetTickers.TabIndex = 0;
            this.btnGetTickers.Text = "Get Tickers";
            this.btnGetTickers.UseVisualStyleBackColor = true;
            this.btnGetTickers.Click += new System.EventHandler(this.Button1_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1254, 729);
            this.Controls.Add(this.cmbExchange);
            this.Controls.Add(this.textTickersResult);
            this.Controls.Add(this.btnGetTickers);
            this.Name = "MainForm";
            this.Text = "ExchangeSharp Test";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox textTickersResult;
        private System.Windows.Forms.ComboBox cmbExchange;
        private System.Windows.Forms.Button btnGetTickers;
    }
}

