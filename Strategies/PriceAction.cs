#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class PriceAction : Strategy
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Fair Value Gap (FVG) Strategy";
                Name = "Price Action";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.ImmediatelySubmit;
                TraceOrders = false;
                BarsRequiredToTrade = 10;
                IsInstantiatedOnEachOptimizationIteration = true;
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
        }

        private DateTime lastExecutionTime_5m = DateTime.MinValue;
        private DateTime lastExecutionTime_1m = DateTime.MinValue;

        int lastBar_5m = -1;
        int lastBar_1m = -1;

        int lastProgressBar = 0; 

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 0)
            {
                return; 
            }
            else if (BarsInProgress == 1)
            {
                if (DateTime.Now.Subtract(lastExecutionTime_5m).TotalSeconds < 1)
                {
                    return;
                }
                lastExecutionTime_5m = DateTime.Now;
            }
            else if (BarsInProgress == 2)
            {
                if (DateTime.Now.Subtract(lastExecutionTime_1m).TotalSeconds < 1)
                {
                    return;
                }
                lastExecutionTime_1m = DateTime.Now;
            }


            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                Print($"{lastExecutionTime_5m} - {Time[0]} - {(lastBar_1m != CurrentBar ?  "New bar" : "")} - {BarsInProgress}");

                lastBar_1m = CurrentBar;
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //1 minute
            {
                Print($"{lastExecutionTime_5m} - {Time[0]} - {(lastBar_5m != CurrentBar ? "New bar": "" )} - {BarsInProgress}");

                lastBar_5m = CurrentBar;
            }

            

        }

       
    }
}
