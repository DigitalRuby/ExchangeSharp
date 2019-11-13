using System;
using System.Text;
using System.Windows.Forms;
using ExchangeSharp;

namespace ExchangeSharpWinForms
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private async void FetchTickers()
        {
            if (!Created || string.IsNullOrWhiteSpace(cmbExchange.SelectedItem as string))
            {
                return;
            }

            this.UseWaitCursor = true;
            try
            {
                var api = ExchangeAPI.GetExchangeAPI(cmbExchange.SelectedItem as string);
                var tickers = await api.GetTickersAsync();
                StringBuilder b = new StringBuilder();
                foreach (var ticker in tickers)
                {
                    b.AppendFormat("{0,-12}{1}\r\n", ticker.Key, ticker.Value);
                }
                textTickersResult.Text = b.ToString();
            }
            catch (Exception ex)
            {
                textTickersResult.Text = ex.ToString();
            }
            finally
            {
                Invoke(new Action(() => this.UseWaitCursor = false));
            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            foreach (var exchange in ExchangeAPI.GetExchangeAPIs())
            {
                cmbExchange.Items.Add(exchange.Name);
            }
            cmbExchange.SelectedIndex = 0;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            FetchTickers();
        }

        private void cmbExchange_SelectedIndexChanged(object sender, EventArgs e)
        {
            FetchTickers();
        }
    }
}
