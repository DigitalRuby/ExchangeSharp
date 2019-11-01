using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;
using ExchangeSharpConsole.Options.Interfaces;

namespace ExchangeSharpConsole.Options
{
	[Verb("buy", HelpText = "Adds a buy order to a given exchange.\n" +
	                        "This sub-command will perform an action that can lead to loss of funds.\n" +
	                        "Be sure to test it first with a dry-run.")]
	public class BuyOption : BaseOption,
		IOptionPerExchange, IOptionWithDryRun,
		IOptionWithKey, IOptionWithInterval, IOptionWithWait, IOptionWithOrderInfo
	{
		public override async Task RunCommand()
		{
			await AddOrder(true);
		}

		protected async Task AddOrder(bool isBuyOrder)
		{
			using var api = GetExchangeInstance(ExchangeName);

			var exchangeOrderRequest = GetExchangeOrderRequest(isBuyOrder, api);

			if (IsDryRun)
			{
				DumpRequest(exchangeOrderRequest);
				return;
			}

			api.LoadAPIKeys(KeyPath);
			var result = await api.PlaceOrderAsync(exchangeOrderRequest);

			if (Wait)
			{
				await WaitForOrder(result, api)
					.ConfigureAwait(false);
			}
			else
			{
				DumpResponse(result);
			}
		}

		private async Task WaitForOrder(ExchangeOrderResult order, IExchangeAPI api)
		{
			while (order.Result == ExchangeAPIOrderResult.Pending)
			{
				Console.Clear();
				Console.WriteLine(order);

				await Task.Delay(IntervalMs)
					.ConfigureAwait(false);

				order = await api.GetOrderDetailsAsync(order.OrderId, order.MarketSymbol);
			}

			Console.Clear();
			Console.WriteLine(order);
			Console.WriteLine($"Your order changed the status to \"{order.Result}\"");
		}

		private ExchangeOrderRequest GetExchangeOrderRequest(bool isBuyOrder, IExchangeAPI api)
		{
			var exchangeOrderRequest = new ExchangeOrderRequest
			{
				Amount = Amount,
				Price = Price,
				IsBuy = isBuyOrder,
				IsMargin = IsMargin,
				MarketSymbol = api.NormalizeMarketSymbol(MarketSymbol),
				OrderType = OrderType,
				StopPrice = StopPrice,
				ShouldRoundAmount = ShouldRoundAmount
			};

			Amount = exchangeOrderRequest.RoundAmount();

			return exchangeOrderRequest;
		}

		private void DumpResponse(ExchangeOrderResult orderResult)
		{
			Console.WriteLine($"Order Id:      {orderResult.OrderId}");
			Console.WriteLine($"Trade Id:      {orderResult.TradeId}");
			Console.WriteLine($"Order Date:    {orderResult.OrderDate:R}");
			Console.WriteLine($"Fill Date:     {orderResult.FillDate:R}");
			Console.WriteLine($"Type:          {(orderResult.IsBuy ? "Bid" : "Ask")}");
			Console.WriteLine($"Market symbol: {orderResult.MarketSymbol}");
			Console.WriteLine($"Status:        {orderResult.Result}");
			Console.WriteLine($"Price:         {orderResult.Price:N}");
			Console.WriteLine($"Amount:        {orderResult.Amount:N}");
			Console.WriteLine($"Amount Filled: {orderResult.AmountFilled:N}");
			Console.WriteLine($"Fees:          {orderResult.Fees:N}");
			Console.WriteLine($"Fees currency: {orderResult.FeesCurrency}");
			Console.WriteLine($"Message:       {orderResult.Message}");
			Console.WriteLine($"Average Price: {orderResult.AveragePrice:N}");
		}

		private void DumpRequest(ExchangeOrderRequest orderRequest)
		{
			Console.WriteLine($"Exchange:          {ExchangeName}");
			Console.WriteLine($"KeyPath:           {KeyPath}");
			Console.WriteLine("---");
			Console.WriteLine($"Price:             {orderRequest.Price:N}");
			Console.WriteLine($"Amount:            {orderRequest.Amount:N}");
			Console.WriteLine($"StopPrice:         {orderRequest.StopPrice:N}");
			Console.WriteLine($"OrderType:         {orderRequest.OrderType}");
			Console.WriteLine($"IsMargin:          {(orderRequest.IsMargin ? "Y" : "N")}");
			Console.WriteLine($"ShouldRoundAmount: {(orderRequest.ShouldRoundAmount ? "Y" : "N")}");
			Console.WriteLine($"MarketSymbol:      {orderRequest.MarketSymbol}");
		}

		public string ExchangeName { get; set; }

		public bool Wait { get; set; }

		public bool IsDryRun { get; set; }

		public string KeyPath { get; set; }

		public decimal Price { get; set; }

		public decimal Amount { get; set; }

		public decimal StopPrice { get; set; }

		public OrderType OrderType { get; set; }

		public bool IsMargin { get; set; }

		public bool ShouldRoundAmount { get; set; }

		public string MarketSymbol { get; set; }

		public int IntervalMs { get; set; }
	}
}
