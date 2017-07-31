using System;
using SmartQuant;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using SmartQuant.Optimization;
using System.IO;

namespace OpenQuant
{
	public class MyStrategy : InstrumentStrategy
	{
		private int tradeNumber = 1, tickCounter = 0, waitToSendOrder = 0, currentLot = 0, index = 0 ,lastLotNumber = 0;
		private DateTime last, lastExecutionTime, lastTickTime, prevTimeForExceution;
		private List<decimal> MA;
		private List<decimal> MA_Average;
		private List<DelayedExecution> delayedExecutions;
		private decimal MAaverage = 0, sumMA, deviation, tickSize, martin = 1;
		private decimal virtualBid, virtualAsk, lastEnter;
		private decimal lastFutureBid, lastFutureAsk, lastCurrentBid, lastCurrentAsk, roundMA;
		private bool ok, tickReady, firstTime = true, inPosition = false, bidAskReady = false, ready = false;
		private TimeSpan startBreak, endBreak;
		private Instrument currentInstrument, futureInstrument;

		public class DelayedExecution
		{
			public DateTime ExecutionTime { get; set; }
			public int OrderType { get; set; }

			public bool Filled { get; set; }
			public int Lots { get; set; }

			public DelayedExecution()
			{
				Filled = false;
				Lots = 1;
			}
		}

		[Parameter]
		public string StartBreakTime = "15:59:00";

		[Parameter]
		public string EndBreakTime = "17:01:00";

		[Parameter]
		public string EndOfContractDate = "11/29/2014";
		
		[Parameter]
		public int MAback = 1000;
		
		[Parameter]
		public decimal Delta = (decimal) 1;
		
		[Parameter]
		public int Delay = 1;

		[Parameter]
		public int DeviationDeltaTime = 60;//in seconds
		
		/*[Parameter]
		public int FromDelta = 1;

		[Parameter]
		public int ToDelta = 1;*/

		[Parameter]
		public decimal Step = 1;

		[Parameter]
		public int PrintEveryMinute = 0;
		//'1' = Print On , '0' = Print Off
		
		[Parameter]
		public int PrintEachRow = 0;
		//'1' = Print On , '0' = Print Off
		
		[Parameter]
		public int PrintEachTickData = 0;
		//'1' = Print On , '0' = Print Off
		
		public MyStrategy(Framework framework, string name)
			: base(framework, name)
		{
		}

		protected override void OnStrategyStart()
		{
			ok = false;
			last = new DateTime();
			lastExecutionTime = new DateTime();
			lastTickTime = new DateTime();
			currentInstrument = Instrument.Legs[0].Instrument;
			futureInstrument = Instrument.Legs[1].Instrument;
			tickSize = (decimal)0.001;
			startBreak = TimeSpan.Parse(StartBreakTime);
			endBreak = TimeSpan.Parse(EndBreakTime);
			MA = new List<decimal>();
			MA_Average = new List<decimal>();
			delayedExecutions = new List<DelayedExecution>();
			martin = Step;
		}
		#region Events Handler
		protected override void OnAsk(Instrument instrument, Ask ask)
		{
			
			handleEvent(instrument, ask);
		}
        protected override void OnLevel2(Instrument instrument, Level2Update update)
        {
            base.OnLevel2(instrument, update);
        }

        protected override void OnBid(Instrument instrument, Bid bid)
		{	
			handleEvent(instrument, bid);
		}
		
		protected override void OnTrade(Instrument instrument, Trade trade)
		{
			handleEvent(instrument, trade);
		}
		#endregion

		private void handleEvent(Instrument instrument, Tick tick)
		{
			if (tick.DateTime.Date.DayOfYear == DateTime.Parse(EndOfContractDate).Date.DayOfYear && tick.DateTime.TimeOfDay > TimeSpan.Parse("15:50:00"))
			{
				#region End of contract
				if (HasPosition(instrument) && Position.Side == PositionSide.Long && inPosition && currentLot > 0)
				{
					inPosition = false;
					prevTimeForExceution = lastTickTime;
					lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
					DelayedExecution order = new DelayedExecution();
					order.OrderType = -1;
					order.ExecutionTime = lastExecutionTime;
					order.Lots = currentLot;
					delayedExecutions.Insert(index, order);
					index++;
					tradeNumber++;
					Console.WriteLine("exit end: " + currentLot);
					Sell(instrument, currentLot, "ExitLongFuture-End #");
					martin = Step;
				}
				if (HasPosition(instrument) && Position.Side == PositionSide.Short && inPosition && currentLot < 0)
				{
					inPosition = false;
					prevTimeForExceution = lastTickTime;
					lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
					DelayedExecution order = new DelayedExecution();
					order.OrderType = -2;
					order.ExecutionTime = lastExecutionTime;
					order.Lots = currentLot*(-1);
					delayedExecutions.Insert(index, order);
					index++;
					tradeNumber++;
					int d = currentLot*(-1);
					Console.WriteLine("exit : " + d);
					Buy(instrument, d, "ExitShortFuture-End #");
					martin = Step;
				}
				return;
				#endregion
			}
			else
			{
				if (!ok)
				{
					ok = true;
					last = tick.DateTime;
					last = last.AddSeconds(1);
					lastTickTime = tick.DateTime;
				}
				if (ok && (tick.DateTime.TimeOfDay < startBreak || tick.DateTime.TimeOfDay > endBreak))
				{		
					#region Avoid weekend
					//---------------avoid weekend-----------------
					if ((tick.DateTime.DayOfWeek == DayOfWeek.Saturday)
						|| (tick.DateTime.DayOfWeek == DayOfWeek.Friday && tick.DateTime.TimeOfDay > endBreak)
						|| (tick.DateTime.DayOfWeek == DayOfWeek.Sunday && tick.DateTime.TimeOfDay < startBreak))
					{
						return;
					}
					#endregion
					if (tick.DateTime != lastTickTime && ready)
					{
						//---------------handle tick event-----------------
						
						decimal f = (virtualAsk + virtualBid) / 2;
						f = Math.Ceiling(f);
						MA.Add(f);
						MAaverage += (f);
						tickCounter++;
						#region Print Each Tick Data
						if (PrintEachTickData == 1)
						{
							using (TextWriter text = File.AppendText(@"C:\Users\U-1001\Desktop\Spread MA Graph\" + instrument.Symbol + " Each Tick.txt"))
							{
								text.WriteLine(lastTickTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," + lastCurrentAsk + "," + lastCurrentBid + "," + lastFutureAsk + "," + lastFutureBid);
							}
						}
						#endregion
						#region MA and deviation calculations
						if (tickCounter == MAback)
						{
							tickReady = true;
							MAaverage /= MAback;
							roundMA = Math.Ceiling(MAaverage);
							if (DateTime.Compare(lastTickTime, last) > 0)
							{
								last = lastTickTime;
								last = last.AddSeconds(DeviationDeltaTime);
								sumMA = 0;
								if (firstTime)
								{
									for (int j = 0; j < MAback; j++)
									{
										MA_Average.Add(MA[j] - MAaverage);
									}
									firstTime = false;
								}
								else
								{
									for (int j = 0; j < MAback; j++)
									{
										MA_Average[j] = MA[j] - MAaverage;
									}
								}
								for (int j = 0; j < MAback; j++)
								{
									MA_Average[j] *= MA_Average[j];
									sumMA += MA_Average[j];
								}
								sumMA /= (MAback - 1);
								deviation = (decimal)Math.Sqrt((double)sumMA);
								deviation = Math.Ceiling(deviation);
                                //virtualASk, virtualBis, roundMA, (deviation * delta), lastTickTime
                                if (PrintEveryMinute == 1)
								{
									using (TextWriter text = File.AppendText(@"C:\Users\U-1001\Desktop\Spread MA Graph\" + instrument.Symbol + " Every minute.txt"))
									{
										text.WriteLine(lastTickTime + " " + MAaverage + " " + roundMA + " " + (roundMA + deviation * Delta) + " " + (roundMA - deviation * Delta) + " " + virtualAsk + " " + virtualBid + " " + deviation);
									}
								}
								if (PrintEachRow == 1)
								{
									using (TextWriter text = File.AppendText(@"C:\Users\U-1001\Desktop\Spread MA Graph\" + instrument.Symbol + " Each row.txt"))
									{
										text.WriteLine(lastTickTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," + lastCurrentAsk + "," + lastCurrentBid + "," + lastFutureAsk + "," + lastFutureBid + " " + + virtualAsk + " " + virtualBid + " " + f + " " + MAaverage + " " + roundMA + " " + deviation + " " + (roundMA + deviation * Delta) + " " + (roundMA - deviation * Delta));
									}
								}
							}
						}
						#endregion
						#region Print Each Row
						if (tickReady)
						{
							if (PrintEachRow == 1)
							{
								using (TextWriter text = File.AppendText(@"C:\Users\U-1001\Desktop\Spread MA Graph\" + instrument.Symbol + " Each row.txt"))
								{
									text.WriteLine(lastTickTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "," + lastCurrentAsk + "," + lastCurrentBid + "," + lastFutureAsk + "," + lastFutureBid + " " + + virtualAsk + " " + virtualBid + " " + f + " " + MAaverage + " " + roundMA + " " + deviation + " " + (roundMA + deviation * Delta) + " " + (roundMA - deviation * Delta));
								}
							}
						}
						#endregion
						#region Handle entries and exits
						if (tickReady)
						{
							if (!HasPosition(instrument) && tick.DateTime.Date < DateTime.Parse(EndOfContractDate).Date && !inPosition && currentLot == 0)
							{
								if ((virtualAsk < roundMA - deviation * Delta))
								{
									inPosition = true;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = 1;
									order.ExecutionTime = lastExecutionTime;
									delayedExecutions.Insert(index, order);
									index++;
									Buy(instrument, 1, "EnterLongFuture #");
									tradeNumber++;
								}
								else if ((virtualBid > roundMA + deviation * Delta))
								{
									inPosition = true;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = 2;
									order.ExecutionTime = lastExecutionTime;
									delayedExecutions.Insert(index, order);
									index++;
									Sell(instrument, 1, "EnterShortFuture #");
									tradeNumber++;
								}
							}
							else if (HasPosition(instrument) && Position.Side == PositionSide.Long && inPosition && currentLot > 0)
							{
								decimal buyLevel = roundMA - deviation * (Delta + martin);

								/*if (virtualAsk < buyLevel && (FromDelta + martin) <= ToDelta)
								{
									//Console.WriteLine(deviation + " " + (FromDelta + martin));
									inPosition = true;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = 1;
									order.ExecutionTime = lastExecutionTime;
									delayedExecutions.Insert(index, order);
									index++;
									Buy(instrument, 1, "EnterLongFuture-Martin #");
									tradeNumber++;
									martin += Step;
								}*/
								if (virtualBid > roundMA)
								{
									inPosition = false;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = -1;
									order.ExecutionTime = lastExecutionTime;
									order.Lots = currentLot;
									delayedExecutions.Insert(index, order);
									index++;
									tradeNumber++;
									Sell(instrument, currentLot, "ExitLongFuture #");
									martin = Step;
								}
							}
							else if (HasPosition(instrument) && Position.Side == PositionSide.Short && inPosition && currentLot < 0)
							{
								decimal sellLevel = roundMA + deviation * (Delta + martin);

								/*if (virtualBid > sellLevel && (FromDelta + martin) <= ToDelta)
								{
									//Console.WriteLine(deviation + " " + (FromDelta + martin));
									inPosition = true;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = 2;
									order.ExecutionTime = lastExecutionTime;
									delayedExecutions.Insert(index, order);
									index++;
									Sell(instrument, 1, "EnterShortFuture-Martin #");
									tradeNumber++;
									martin += Step;
								}*/
								 if (virtualAsk < roundMA)
								{
									inPosition = false;
									prevTimeForExceution = lastTickTime;
									lastExecutionTime = prevTimeForExceution.AddMilliseconds(Delay);
									DelayedExecution order = new DelayedExecution();
									order.OrderType = -2;
									order.ExecutionTime = lastExecutionTime;
									order.Lots = currentLot*(-1);
									delayedExecutions.Insert(index, order);
									index++;
									tradeNumber++;
									int d = currentLot*(-1);
									Buy(instrument, d, "ExitShortFuture #");
									martin = Step;
								}
							}
						}
						#endregion
						if (tickReady)
						{
							if (tickCounter == MAback)
							{
								tickCounter--;
								MAaverage *= MAback;
								MAaverage -= MA[0];
								MA.RemoveAt(0);
							}
						}
						#region Handle order delay
						//---------------handle order delay-----------------------
						using (TextWriter text = File.AppendText(@"\\DTRADE\Public\Strategy Box\Programs\Arbitrage\StratagyPrints\" + instrument.Symbol + "asd.txt"))
						{
							for(int i=0; i< delayedExecutions.Count; i++)
							{
								if (tick.DateTime > delayedExecutions[i].ExecutionTime && delayedExecutions[i].Filled == false)
								{
									switch (delayedExecutions[i].OrderType)
									{
										case 1:
										case -2:
										{
											currentLot += delayedExecutions[i].Lots;
											text.WriteLine(prevTimeForExceution + ";" + futureInstrument.Symbol + ";Buy;" + lastFutureAsk + ";" + delayedExecutions[i].Lots + ";" + lastFutureAsk + ";LongFuture");
											text.WriteLine(prevTimeForExceution + ";" + currentInstrument.Symbol + ";Sell;" + lastCurrentBid + ";" + delayedExecutions[i].Lots + ";" + lastCurrentBid + ";ShortCurrent");
											break;
										}
										case 2:
										case -1:
										{
											currentLot -= delayedExecutions[i].Lots;
											text.WriteLine(prevTimeForExceution + ";" + currentInstrument.Symbol + ";Buy;" + lastCurrentAsk + ";" + delayedExecutions[i].Lots + ";" + lastCurrentAsk + ";LongCurrent");
											text.WriteLine(prevTimeForExceution + ";" + futureInstrument.Symbol + ";Sell;" + lastFutureBid + ";" + delayedExecutions[i].Lots + ";" + lastFutureBid + ";ShortFuture");
											break;
										}
									}
									delayedExecutions[i].Filled = true;
								}
							}
						}
						#endregion
					}
					#region Save last tick values
					//---------------save last tick values-----------------
					lastTickTime = tick.DateTime;
					lastCurrentAsk = Math.Round((decimal)currentInstrument.Ask.Price, 3);
					lastCurrentBid = Math.Round((decimal)currentInstrument.Bid.Price, 3);
					lastFutureAsk = Math.Round((decimal)futureInstrument.Ask.Price, 3);
					lastFutureBid = Math.Round((decimal)futureInstrument.Bid.Price, 3);

					virtualBid = ((lastFutureBid - lastCurrentAsk) / tickSize);
					virtualAsk = ((lastFutureAsk - lastCurrentBid) / tickSize);
					ready = true;
					#endregion

				}
			}
		}
		
		protected override void OnFill(Fill fill)
		{
			string name = fill.Text;
			//Console.WriteLine("### " + name );
		}
	}
}











