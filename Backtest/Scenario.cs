using System;
using SmartQuant;
using SmartQuant.Statistics;

namespace OpenQuant
{
	public class Backtest : Scenario
	{
		private long barSize = 60;

		public Backtest(Framework framework)
			: base(framework)
		{
		}

		public override void Run()
		{

			Instrument spreadInsturment = InstrumentManager.Get("NGF 01-15 vs NGG 02-15");

			// Add spread instrument if needed.
			if (spreadInsturment == null)
			{
				spreadInsturment = new Instrument(InstrumentType.Synthetic, "NGF 01-15 vs NGG 02-15");

				InstrumentManager.Add(spreadInsturment);
			}
			

			spreadInsturment.Legs.Clear();
			

			// Add legs for spread instrument if needed.
			if (spreadInsturment.Legs.Count == 0)
			{
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("NGF17after"),1));
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("NGG17after"),1));
			}
			

			// Main strategy.
			strategy = new Strategy(framework, "SpreadTrading");

			// Create BuySide strategy and add trading instrument.
			MyStrategy buySide = new MyStrategy(framework, "BuySide");
			buySide.Instruments.Add(spreadInsturment);

			// Create SellSide strategy.
			SpreadSellSide sellSide = new SpreadSellSide(framework, "SellSide");
			sellSide.Global[SpreadSellSide.barSizeCode] = barSize;

			// Set SellSide as data and execution provider for BuySide strategy.
			buySide.DataProvider = sellSide;
			buySide.ExecutionProvider = sellSide;

			// Add strategies to main.
			strategy.AddStrategy(buySide);
			strategy.AddStrategy(sellSide);

			// Set DataSimulator's dates.
			DataSimulator.DateTime1 = new DateTime(2014, 11, 23);// 1 day before real start
			DataSimulator.DateTime2 = new DateTime(2014, 11, 29);// 1 day after real end

            BarFactory.Clear();

			// Run.
			StartStrategy();
		}
	}
}























