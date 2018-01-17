/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if HAS_WINDOWS_FORMS

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
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            KeyPreview = true;
            PlotChart.Update();
            if (buyPrices != null)
            {
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
            }
            if (sellPrices != null)
            {
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
        }

        public PlotForm()
        {
            InitializeComponent();
        }

        public void SetPlotPoints(List<List<KeyValuePair<float, float>>> points, List<KeyValuePair<float, float>> buyPrices = null, List<KeyValuePair<float, float>> sellPrices = null)
        {
            // clear the chart
            PlotChart.Series.Clear();
            PlotChart.ChartAreas.Clear();
            PlotChart.ChartAreas.Add(new ChartArea());

            this.buyPrices = buyPrices;
            this.sellPrices = sellPrices;
            int index = 0;
            float minPrice = float.MaxValue;
            float maxPrice = float.MinValue;
            Color[] colors = new Color[] { Color.Red, Color.Blue, Color.Cyan };
            ChartArea chartArea = null;
            foreach (List<KeyValuePair<float, float>> list in points)
            {
                Series s = PlotChart.Series.Add("Set_" + index.ToString());
                s.ChartType = SeriesChartType.Line;
                s.XValueMember = "Time";
                s.YValueMembers = "Price";
                s.Color = colors[index];
                s.XValueType = ChartValueType.Int32;
                s.YValueType = ChartValueType.Single;
                foreach (KeyValuePair<float, float> kv in list)
                {
                    s.Points.AddXY(kv.Key, kv.Value);
                    minPrice = Math.Min(minPrice, kv.Value);
                    maxPrice = Math.Max(maxPrice, kv.Value);
                }
                chartArea = chartArea ?? PlotChart.ChartAreas[s.ChartArea];
                index++;
            }

            chartArea.AxisX.Minimum = points[0][0].Key;
            chartArea.AxisX.Maximum = points[0][points[0].Count - 1].Key;
            chartArea.AxisY.Minimum = minPrice;
            chartArea.AxisY.Maximum = maxPrice;

            // enable autoscroll
            chartArea.AxisX.ScaleView.Zoomable = true;
            chartArea.AxisY.ScaleView.Zoomable = false;
            chartArea.CursorX.AutoScroll = true;
            chartArea.CursorY.AutoScroll = false;
            chartArea.CursorX.IsUserSelectionEnabled = true;
            chartArea.CursorY.IsUserSelectionEnabled = false;

            int blockSize = points[0].Count;
            chartArea.CursorX.AutoScroll = true;
            chartArea.CursorY.AutoScroll = false;

            // let's zoom to [0,blockSize] (e.g. [0,100])
            chartArea.AxisX.ScaleView.Zoomable = true;
            chartArea.AxisX.ScaleView.SizeType = DateTimeIntervalType.Number;
            int position = 0;
            int size = blockSize;
            chartArea.AxisX.ScaleView.Zoom(position, size);

            // disable zoom-reset button (only scrollbar's arrows are available)
            chartArea.AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

            // set scrollbar small change to blockSize (e.g. 100)
            chartArea.AxisX.ScaleView.SmallScrollSize = blockSize;

            chartArea.AxisX.ScaleView.ZoomReset();
        }
    }

    public static class PlotFormExtensions
    {
        public static void ShowPlotForm(this Trader trader)
        {
            PlotForm form = new PlotForm();
            form.WindowState = FormWindowState.Maximized;
            form.SetPlotPoints(trader.PlotPoints, trader.BuyPrices, trader.SellPrices);
            form.ShowDialog();
        }
    }
}

#endif
