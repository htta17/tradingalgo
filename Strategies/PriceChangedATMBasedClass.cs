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
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class PriceChangedATMBasedClass : Strategy
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Fair Value Gap (FVG) Strategy";
                Name = "Price Action Realtime";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.WaitUntilFlat;
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

        #region Helpers - Make sure not too many times OnBarUpdate is counted
        private DateTime lastExecutionTime_5m = DateTime.MinValue;
        private DateTime lastExecutionTime_3m = DateTime.MinValue;
        private DateTime lastExecutionTime_1m = DateTime.MinValue;

        int lastBar_5m = -1;
        int lastBar_1m = -1;

        List<double> KeyLevels = new List<double>();

        private bool RunningTooFast(int barsPeriod)
        {
            bool isTooFast = false; 
            if (barsPeriod == 1)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_1m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_1m = DateTime.Now;
                }
            }
            else if (barsPeriod == 3)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_3m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_3m = DateTime.Now;
                }
            }
            else if (barsPeriod == 5)
            {
                isTooFast = DateTime.Now.Subtract(lastExecutionTime_5m).TotalSeconds < 1;

                if (!isTooFast)
                {
                    lastExecutionTime_5m = DateTime.Now;
                }
            }

            return isTooFast;
        }


        /// <summary>
        /// Excecute when it moved to new bar
        /// </summary>
        /// <param name="barsPeriod"></param>
        protected void DoForFirstTickOfTheBar(int barsPeriod)
        { 
            
        }

        #endregion       

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 0) // BarsInProgress == 0 dùng cho view hiện tại, ko làm gì
            {
                return; 
            }

            if (RunningTooFast(BarsPeriod.Value))
            {
                return;
            }    

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                if (lastBar_1m != CurrentBar)
                {
                    DoForFirstTickOfTheBar(BarsPeriod.Value); 
                }
                lastBar_1m = CurrentBar;
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //1 minute
            {
                if (lastBar_5m != CurrentBar)
                {
                    DoForFirstTickOfTheBar(BarsPeriod.Value);
                }
                lastBar_5m = CurrentBar;
            }
        }

       
    }
}
