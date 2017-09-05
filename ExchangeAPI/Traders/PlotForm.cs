/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.DataVisualization.Charting;

namespace ExchangeSharp
{
    public partial class PlotForm : Form
    {
        private List<KeyValuePair<float, float>> buyPrices;
        private List<KeyValuePair<float, float>> sellPrices;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                PlotChart.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                PlotChart.ChartAreas[0].AxisY.ScaleView.ZoomReset();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            PlotChart.Update();

            foreach (KeyValuePair<float, float> kv in buyPrices)
            {
                double x = PlotChart.ChartAreas[0].AxisX.ValueToPixelPosition(kv.Key);
                double y = PlotChart.ChartAreas[0].AxisY.ValueToPixelPosition(kv.Value);
                Label label = new Label { Text = "B" };
                label.BackColor = Color.Transparent;
                label.Font = new Font("Arial", 14.0f, FontStyle.Bold);
                label.Location = new Point((int)x, (int)y);
                label.AutoSize = true;
                PlotChart.Controls.Add(label);
            }
            foreach (KeyValuePair<float, float> kv in sellPrices)
            {
                double x = PlotChart.ChartAreas[0].AxisX.ValueToPixelPosition(kv.Key);
                double y = PlotChart.ChartAreas[0].AxisY.ValueToPixelPosition(kv.Value);
                Label label = new Label { Text = "S" };
                label.BackColor = Color.Transparent;
                label.Font = new Font("Arial", 14.0f, FontStyle.Bold);
                label.Location = new Point((int)x, (int)y);
                label.AutoSize = true;
                PlotChart.Controls.Add(label);
            }
        }

        public PlotForm()
        {
            InitializeComponent();
        }

        public void SetPlotPoints(List<List<KeyValuePair<float, float>>> points, List<KeyValuePair<float, float>> buyPrices, List<KeyValuePair<float, float>> sellPrices)
        {
            this.buyPrices = buyPrices;
            this.sellPrices = sellPrices;
            int index = 0;
            float minPrice = float.MaxValue;
            float maxPrice = float.MinValue;
            Color[] colors = new Color[] { Color.Red, Color.Blue, Color.Cyan };
            foreach (List<KeyValuePair<float, float>> list in points)
            {
                Series s = new Series("Set_" + index.ToString());
                s.XAxisType = AxisType.Secondary;
                s.YAxisType = AxisType.Primary;
                s.ChartType = SeriesChartType.Line;
                s.XValueMember = "Time";
                s.YValueMembers = "Price";
                s.Color = colors[index];
                foreach (KeyValuePair<float, float> kv in list)
                {
                    s.Points.AddXY(kv.Key, kv.Value);
                    minPrice = Math.Min(minPrice, kv.Value);
                    maxPrice = Math.Max(maxPrice, kv.Value);
                }
                PlotChart.Series.Add(s);
                index++;
            }

            PlotChart.ChartAreas[0].AxisY.Minimum = minPrice;
            PlotChart.ChartAreas[0].AxisY.Maximum = maxPrice;
            PlotChart.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            PlotChart.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            PlotChart.ChartAreas[0].CursorX.AutoScroll = true;
            PlotChart.ChartAreas[0].CursorY.AutoScroll = true;
            PlotChart.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            PlotChart.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
        }
    }
}
