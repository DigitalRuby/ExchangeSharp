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
            PlotChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(PlotChart)).BeginInit();
            SuspendLayout();
            // 
            // PlotChart
            // 
            PlotChart.Dock = System.Windows.Forms.DockStyle.Fill;
            PlotChart.Location = new System.Drawing.Point(0, 0);
            PlotChart.Margin = new System.Windows.Forms.Padding(2);
            PlotChart.Name = "PlotChart";
            PlotChart.Size = new System.Drawing.Size(658, 412);
            PlotChart.TabIndex = 0;
            PlotChart.Text = "chart1";
            // 
            // PlotForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(658, 412);
            Controls.Add(PlotChart);
            Margin = new System.Windows.Forms.Padding(2);
            Name = "PlotForm";
            Text = "Plot of Trade Data";
            ((System.ComponentModel.ISupportInitialize)(PlotChart)).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataVisualization.Charting.Chart PlotChart;
    }
}

#endif
