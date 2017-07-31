using System;
using System.Collections.Generic;
using SmartQuant;

namespace OpenQuant
{
	public class OrderProcessor
	{
		private SpreadSellSide strategy;
		private ExecutionCommand command;
		private Order order;
		private Instrument spreadInstrument;
		private Dictionary<Order, Leg> orders;
		private bool isDone;

		public OrderProcessor(SpreadSellSide strategy, ExecutionCommand command)
		{
			strategy.processors.AddLast(this);

			// Init OrderProcessor fields.
			this.strategy = strategy;
			this.command = command;
			order = command.Order;
			spreadInstrument = order.Instrument;
			orders = new Dictionary<Order, Leg>();

			// Send leg orders if order type is market.
			if (order.Type == OrderType.Market)
			{
				SendLegOrders();
			}
		}

		public bool IsDone
		{
			get { return isDone; }
		}

		public Order Order
		{
			get { return order; }
		}

		public void SendLegOrders()
		{
			if (order.Side == OrderSide.Sell)
			{
				orders.Add(strategy.BuyOrder(spreadInstrument.Legs[0].Instrument, order.Qty , "LongCurrent"), spreadInstrument.Legs[0]);
				orders.Add(strategy.SellOrder(spreadInstrument.Legs[1].Instrument, order.Qty, "ShortFuture"), spreadInstrument.Legs[1]);
			}
			else
			{
				orders.Add(strategy.BuyOrder(spreadInstrument.Legs[1].Instrument, order.Qty, "LongFuture"), spreadInstrument.Legs[1]);
				orders.Add(strategy.SellOrder(spreadInstrument.Legs[0].Instrument, order.Qty, "ShortCurrent"), spreadInstrument.Legs[0]);
			}

			// Send leg orders.
			foreach (Order ord in orders.Keys)
			{
				strategy.Send(ord);
			}
		}

		public void CancelLegOrders()
		{
			isDone = true;

			// Create ExecCancelled report for spread instrument.
			ExecutionReport report = new ExecutionReport();
			report.DateTime = strategy.Framework.Clock.DateTime;
			report.Order = order;
			report.Instrument = order.Instrument;
			report.OrdQty = order.Qty;
			report.ExecType = ExecType.ExecCancelled;
			report.OrdStatus = OrderStatus.Cancelled;
			report.OrdType = order.Type;
			report.Side = order.Side;
			report.CumQty = order.CumQty;
			report.LastQty = 0;
			report.LeavesQty = order.LeavesQty;
			report.LastPx = 0;
			report.AvgPx = 0;

			// Send report to SellSide strategy.
			strategy.EmitExecutionReport(report);
		}

		public void OnExecutionReport(ExecutionReport report)
		{
				
			if (orders.ContainsKey(report.Order))
			{
				double avgPrice = 0;
				bool isFilled = true;
				bool isCancelled = true;

				foreach (Order ord in orders.Keys)
				{
					avgPrice += ord.AvgPx * orders[ord].Weight;

					if (!ord.IsFilled)
						isFilled = false;

					if (!ord.IsCancelled)
						isCancelled = false;
				}

				// If leg orders are filled.
				if (isFilled)
				{
					
					// Create ExecTrade report for spread instrument.
					ExecutionReport execution = new ExecutionReport();
					execution.AvgPx = avgPrice;
					execution.Commission = report.Commission;
					execution.CumQty = report.CumQty;
					execution.DateTime = report.DateTime;
					execution.ExecType = ExecType.ExecTrade;
					execution.Instrument = spreadInstrument;
					execution.LastPx = execution.AvgPx;
					execution.LastQty = order.Qty;
					execution.LeavesQty = 0;
					execution.Order = command.Order;
					execution.OrdQty = order.Qty;
					execution.OrdStatus = OrderStatus.Filled;
					execution.OrdType = command.Order.Type;
					execution.Price = command.Order.Price;
					execution.Side = command.Order.Side;
					execution.StopPx = command.Order.StopPx;
					execution.Text = command.Order.Text;

					// Send report to SellSide strategy.
					strategy.EmitExecutionReport(execution);

					isDone = true;
				}

				// If leg orders are cancelled.
				if (isCancelled)
				{
					// Create ExecCancelled report for spread instrument.
					ExecutionReport execution = new ExecutionReport();
					execution.DateTime = report.DateTime;
					execution.Order = command.Order;
					execution.Instrument = spreadInstrument;
					execution.OrdQty = order.Qty;
					execution.ExecType = ExecType.ExecCancelled;
					execution.OrdStatus = OrderStatus.Cancelled;
					execution.OrdType = order.Type;
					execution.Side = order.Side;
					execution.CumQty = report.CumQty;
					execution.LastQty = 0;
					execution.LeavesQty = report.LeavesQty;
					execution.LastPx = 0;
					execution.AvgPx = 0;

					// Send report to SellSide strategy.
					strategy.EmitExecutionReport(execution);

					isDone = true;
				}
			}
		}

		public void OnBid(Bid bid)
		{
			// Check conditions for send leg orders.
			if (order.Side == OrderSide.Sell)
			{
				switch (order.Type)
				{
					case OrderType.Limit:
						if (bid.Price >= order.Price)
							SendLegOrders();
						break;

					case OrderType.Stop:
						if (bid.Price <= order.StopPx)
							SendLegOrders();
						break;
				}
			}
		}

		public void OnAsk(Ask ask)
		{
			// Check conditions for send leg orders.
			if (order.Side == OrderSide.Buy)
			{
				switch (order.Type)
				{
					case OrderType.Limit:
						if (ask.Price <= order.Price)
							SendLegOrders();
						break;

					case OrderType.Stop:
						if (ask.Price >= order.StopPx)
							SendLegOrders();
						break;
				}
			}
		}

		public void OnTrade(Trade trade)
		{
			// Check conditions for send leg orders.
			switch (order.Type)
			{
				case OrderType.Limit:
					switch (order.Side)
					{
						case OrderSide.Buy:
							if (trade.Price <= order.Price)
								SendLegOrders();
							break;

						case OrderSide.Sell:
							if (trade.Price >= order.Price)
								SendLegOrders();
							break;
					}
					break;

				case OrderType.Stop:
					switch (order.Side)
					{
						case OrderSide.Buy:
							if (trade.Price >= order.StopPx)
								SendLegOrders();
							break;

						case OrderSide.Sell:
							if (trade.Price <= order.StopPx)
								SendLegOrders();
							break;
					}
					break;
			}
		}
	}
}





