using System;
using SmartQuant;

namespace OpenQuant
{
	public class Realtime : Scenario
	{
		//private long barSize;

		public Realtime(Framework framework)
			: base(framework)
		{
			// Set bar size in seconds. 60 seconds is 1 minute.
			//barSize = 60;
		}

		public override void Run()
		{
			// Prepare running.
			Console.WriteLine("Prepare running in {0} mode...", StrategyManager.Mode);

			// Get spread instrument.
			Instrument spreadInsturment = InstrumentManager.Get("NG 01-15 vs NG 02-15");

			// Add spread instrument if needed.
			if (spreadInsturment == null)
			{
				spreadInsturment = new Instrument(InstrumentType.Stock, "NG 01-15 vs NG 02-15");
				InstrumentManager.Add(spreadInsturment);
			}

			// Add legs for spread instrument if needed.
			if (spreadInsturment.Legs.Count == 0)
			{
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("NG 01-15"), 1));
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("NG 02-15"), 1));
			}

			// Main strategy.
			strategy = new Strategy(framework, "SpreadTrading");

			// Create BuySide strategy and add trading instrument.
			MyStrategy buySide = new MyStrategy(framework, "BuySide");
			buySide.Instruments.Add(spreadInsturment);

			// Create SellSide strategy.
			SpreadSellSide sellSide = new SpreadSellSide(framework, "SellSide");
			//sellSide.Global[SpreadSellSide.barSizeCode] = barSize;

			// Set SellSide as data and execution provider for BuySide strategy.
			buySide.DataProvider = sellSide;
			buySide.ExecutionProvider = sellSide;

			// Add strategies to main.
			strategy.AddStrategy(buySide);
			strategy.AddStrategy(sellSide);

			// Get provider for realtime.
			Provider quantRouter = ProviderManager.Providers["QuantRouter"] as Provider;

			if (quantRouter.Status == ProviderStatus.Disconnected)
				quantRouter.Connect();

			if (StrategyManager.Mode == StrategyMode.Paper)
			{
				// Set QuantRouter as data provider.
                sellSide.DataProvider = quantRouter as IDataProvider;
			}
			else if (StrategyManager.Mode == StrategyMode.Live)
			{
				// Set QuantRouter as data and execution provider.
                sellSide.DataProvider = quantRouter as IDataProvider;
                sellSide.ExecutionProvider = quantRouter as IExecutionProvider;
			}

			// Set null for event filter.
			EventManager.Filter = null;

			// Add 1 minute bars (60 seconds) for spread instrument.
            BarFactory.Clear();
		//	BarFactory.Add(spreadInsturment, BarType.Time, barSize);

			// Run.
			Console.WriteLine("Run in {0} mode.", StrategyManager.Mode);
			StartStrategy(StrategyManager.Mode);
		}
	}
}



