using System;
using System.Collections.Generic;
using SmartQuant;
using System.IO;

namespace OpenQuant
{
	public class SpreadSellSide : SellSideInstrumentStrategy
	{
		public const string barSizeCode = "BarSize";

		//private long barSize;
		private Instrument spread, futureInstrument, currentInstrument;
		private bool isAskBidReady;
		private bool isTradeReady;
		internal System.Collections.Generic.LinkedList<OrderProcessor> processors;
		
		public SpreadSellSide(Framework framework, string name)
			: base(framework, name)
		{
		}

		internal Framework Framework
		{
			get { return framework; }
		}

		protected override void OnSubscribe(Instrument instrument)
		{
			spread = instrument;
			futureInstrument = spread.Legs[1].Instrument;
			currentInstrument = spread.Legs[0].Instrument;

			// Remove instruments from strategy.
			Instruments.Clear();

			// Add legs instruments to strategy.
			foreach (Leg leg in spread.Legs)
				AddInstrument(leg.Instrument);

			processors = new System.Collections.Generic.LinkedList<OrderProcessor>();

			//	AddGroups();
		}

		protected override void OnAsk(Instrument instrument, Ask ask)
		{
			if (!isAskBidReady)
			{
				isAskBidReady = true;

				foreach (Leg leg in spread.Legs)
				{
					if (leg.Instrument.Ask == null || leg.Instrument.Bid == null)
					{
						isAskBidReady = false;

						return;
					}
				}
			}

			decimal askPrice = 0;
			int askSize = ask.Size;
  
			decimal l = (Math.Round((decimal)futureInstrument.Ask.Price, 3) - Math.Round((decimal)currentInstrument.Bid.Price, 3));
			askPrice = (l / (decimal)0.001);

			// Create new ask for spread instrument.
			EmitAsk(new Ask(ask.DateTime, 0, spread.Id, (double)askPrice, askSize));
		}
		public override void EmitAsk(Ask ask)
		{
			// Emit ask to BuySide strategy.
			base.EmitAsk(ask);

			//	System.Collections.Generic.LinkedListNode<OrderProcessor> processorNode = processors.First;

			// Send ask to order processors.
			//	while (processorNode != null)
			//	{
			//		OrderProcessor processor = processorNode.Value;
			//	processor.OnAsk(ask);
			//	processorNode = processorNode.Next;
			//}
			
		}
		protected override void OnBid(Instrument instrument, Bid bid)
		{
			if (!isAskBidReady)
			{
				isAskBidReady = true;

				foreach (Leg leg in spread.Legs)
				{
					if (leg.Instrument.Ask == null || leg.Instrument.Bid == null)
					{
						isAskBidReady = false;

						return;
					}
				}
			}

			decimal bidPrice = 0;
			int bidSize = bid.Size;

			decimal l = (Math.Round((decimal)futureInstrument.Ask.Price, 3) - Math.Round((decimal)currentInstrument.Bid.Price, 3));
			bidPrice = (l / (decimal)0.001);

			// Create new ask for spread instrument.
			EmitBid(new Bid(bid.DateTime, 0, spread.Id, (double)bidPrice, bidSize));
		}
		
		public override void EmitBid(Bid bid)
		{
			// Emit bid to BuySide strategy.
			base.EmitBid(bid);

			//	System.Collections.Generic.LinkedListNode<OrderProcessor> processorNode = processors.First;

			// Send bid to order processors.
			//	while (processorNode != null)
			//	{
			//	OrderProcessor processor = processorNode.Value;
			//	processor.OnBid(bid);
			//	processorNode = processorNode.Next;
			//	}
		}
		protected override void OnTrade(Instrument instrument, Trade trade)
		{
			if (!isTradeReady)
			{
				isTradeReady = true;

				foreach (Leg leg in spread.Legs)
				{
					if (leg.Instrument.Trade == null)
					{
						isTradeReady = false;

						return;
					}
				}
			}

			//
			decimal tradePrice = 0;
			int tradeSize = trade.Size;
		
			decimal l = (Math.Round((decimal)futureInstrument.Ask.Price, 3) - Math.Round((decimal)currentInstrument.Bid.Price, 3));
			tradePrice = (l / (decimal)0.001);
			
			// Create new trade for spread instrument.*/
			EmitTrade(new Trade(trade.DateTime, 0, spread.Id, (double)tradePrice, tradeSize));
		}
		
		public override void EmitTrade(Trade trade)
		{
			// Emit trade to BuySide strategy.
			base.EmitTrade(trade);

			//	System.Collections.Generic.LinkedListNode<OrderProcessor> processorNode = processors.First;

			// Send trade to order processors.
			//	while (processorNode != null)
			//	{
			//		OrderProcessor processor = processorNode.Value;
			//		processor.OnTrade(trade);
			//		processorNode = processorNode.Next;
			//	}
		}

		protected override void OnExecutionReport(ExecutionReport report)
		{
			System.Collections.Generic.LinkedListNode<OrderProcessor> processorNode = processors.First;
			while (processorNode != null)
			{
				OrderProcessor processor = processorNode.Value;
				processor.OnExecutionReport(report);

				if (processor.IsDone)
				{
					processors.Remove(processor);
					
				}

				processorNode = processorNode.Next;
			}
		}

		public override void OnSendCommand(ExecutionCommand command)
		{
			Order order = command.Order;
			
			// Create ExecNew report.
			ExecutionReport report = new ExecutionReport();
			report.DateTime = framework.Clock.DateTime;
			report.Order = order;
			report.Instrument = order.Instrument;
			report.OrdQty = order.Qty;
			report.ExecType = ExecType.ExecNew;
			report.OrdStatus = OrderStatus.New;
			report.OrdType = order.Type;
			report.Side = order.Side;

			// Send report to BuySide strategy.
			EmitExecutionReport(report);

			// Create new order processor.
			new OrderProcessor(this, command);
		}

		public override void OnCancelCommand(ExecutionCommand command)
		{
			// Search for needed order processor.
			foreach (OrderProcessor processor in processors)
			{
				if (processor.Order == command.Order)
				{
					// Cancel leg orders.
					processor.CancelLegOrders();

					// Remove order processor.
					if (processor.IsDone)
						processors.Remove(processor);

					return;
				}
			}
		}

		public override void OnReplaceCommand(ExecutionCommand command)
		{
			Console.WriteLine("{0} Replace command is not supported.", GetType().Name);
		}
		

    }
}














