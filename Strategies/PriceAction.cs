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
    public class FVGStrategy : Strategy
    {
        private double fvgHigh;
        private double fvgLow;
        private bool fvgExists;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Fair Value Gap (FVG) Strategy";
                Name = "FVG Strategy";
                Calculate = Calculate.OnBarClose;
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
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 3) return;  // Ensure enough bars exist

            // Define FVG Conditions (Bullish and Bearish)
            bool bullishFVG = Low[2] > High[0];  // Previous Low > Current High
            bool bearishFVG = High[2] < Low[0];  // Previous High < Current Low

            // Store FVG Levels
            if (bullishFVG)
            {
                fvgHigh = Low[2];
                fvgLow = High[0];
                fvgExists = true;

                // Draw FVG using custom Rectangle method
                Draw.Rectangle(this, "FVG_Bullish_" + CurrentBar, 1, fvgHigh, -1, fvgLow, Brushes.Green);
            }
            else if (bearishFVG)
            {
                fvgHigh = Low[0];
                fvgLow = High[2];
                fvgExists = true;

                // Draw FVG using custom Rectangle method
                Draw.Rectangle(this, "FVG_Bearish_" + CurrentBar, 1, fvgHigh, -1, fvgLow, Brushes.Red);
            }
            else
            {
                fvgExists = false;
            }

            // Trade Entry Conditions
            if (fvgExists && Close[1] < fvgHigh && Close[0] > fvgHigh)  // Bullish FVG Entry
            {
                EnterLong("FVG_Buy");
				SetStopLoss("FVG_Buy", CalculationMode.Ticks, 120, false);
                SetProfitTarget("FVG_Buy", CalculationMode.Ticks, 120);
            }
            else if (fvgExists && Close[1] > fvgLow && Close[0] < fvgLow)  // Bearish FVG Entry
            {
                EnterShort("FVG_Sell");
				SetStopLoss("FVG_Sell",CalculationMode.Ticks, 120, false);
				SetProfitTarget("FVG_Sell",CalculationMode.Ticks, 120);
            }
        }

       
    }
}
