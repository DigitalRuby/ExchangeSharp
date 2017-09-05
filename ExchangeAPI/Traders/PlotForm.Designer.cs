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
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            this.PlotChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(this.PlotChart)).BeginInit();
            this.SuspendLayout();
            // 
            // PlotChart
            // 
            chartArea1.Name = "ChartArea1";
            this.PlotChart.ChartAreas.Add(chartArea1);
            this.PlotChart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PlotChart.Location = new System.Drawing.Point(0, 0);
            this.PlotChart.Name = "PlotChart";
            this.PlotChart.Size = new System.Drawing.Size(1317, 792);
            this.PlotChart.TabIndex = 0;
            this.PlotChart.Text = "chart1";
            // 
            // PlotForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1317, 792);
            this.Controls.Add(this.PlotChart);
            this.Name = "PlotForm";
            this.Text = "Plot of Trade Data";
            ((System.ComponentModel.ISupportInitialize)(this.PlotChart)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart PlotChart;
    }
}