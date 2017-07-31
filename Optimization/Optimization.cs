using System;

using SmartQuant;
using SmartQuant.Optimization;


namespace OpenQuant
{
	public class Optimization : Scenario
	{
		private long barSize = 60;
		
		public Optimization(Framework framework)
			: base(framework)
		{
		}

		public override void Run()
		{
			MulticoreOptimizer optimizer = new MulticoreOptimizer(this.framework);
			
			
			OptimizationUniverse universe = new OptimizationUniverse();

			for (decimal Delta = 1; Delta < 8; Delta++)
				{
					OptimizationParameterSet parameter = new OptimizationParameterSet();

					parameter.Add("Delta", Delta);

					universe.Add(parameter);
				}
			Instrument spreadInsturment = InstrumentManager.Get("*NG 01-15 vs *NG 02-15");

			// Add spread instrument if needed.
			if (spreadInsturment == null)
			{
				spreadInsturment = new Instrument(InstrumentType.Future, "*NG 01-15 vs *NG 02-15");

				InstrumentManager.Add(spreadInsturment);
			}

			spreadInsturment.Legs.Clear();

			// Add legs for spread instrument if needed.
			if (spreadInsturment.Legs.Count == 0)
			{
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("*NG 01-15"),1));
				spreadInsturment.Legs.Add(new Leg(InstrumentManager.Get("*NG 02-15"),1));
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

			// Set DataSimulator's dates.
			DataSimulator.DateTime1 = new DateTime(2014, 11, 18);// 1 day before real start
			DataSimulator.DateTime2 = new DateTime(2014, 12, 30);// 1 day after real end

			InstrumentList instruments = new InstrumentList();

			instruments.Add(spreadInsturment);

			strategy.AddInstruments(instruments);

			//You can choose an optimization method from the "Optimizers" window
			//and observe the optimization results in the "Optimization Results" window.

			//Optimization can use either of two ways to declare parameters:

			//1. Optimization using [OptimizationParameter] atrributes from Strategy
			
			//Optimize(strategy);

			//2. Optimization via OptimizationUniverse
			optimizer.Optimize(strategy,universe);					
		}
	}
}



