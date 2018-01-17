#if HAS_WINDOWS_FORMS

namespace ExchangeSharp
{
    partial class PlotForm
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
            this.PlotChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(this.PlotChart)).BeginInit();
            this.SuspendLayout();
            // 
            // PlotChart
            // 
            this.PlotChart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PlotChart.Location = new System.Drawing.Point(0, 0);
            this.PlotChart.Margin = new System.Windows.Forms.Padding(2);
            this.PlotChart.Name = "PlotChart";
            this.PlotChart.Size = new System.Drawing.Size(658, 412);
            this.PlotChart.TabIndex = 0;
            this.PlotChart.Text = "chart1";
            // 
            // PlotForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(658, 412);
            this.Controls.Add(this.PlotChart);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "PlotForm";
            this.Text = "Plot of Trade Data";
            ((System.ComponentModel.ISupportInitialize)(this.PlotChart)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart PlotChart;
    }
}

#endif
