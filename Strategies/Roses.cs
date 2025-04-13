#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Custom.Strategies;
using System.Reflection;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Roses : PriceChangedATMBasedClass<TradeAction>
	{
		public Roses() : base()
		{ 
		}

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Description = "Roses (ATM realtime)";
            Name = "EMA 21/29 1-min frame, trending.";

            FullSizeATMName = "Roses_Default_4cts";
            HalfSizefATMName = "Roses_Default_4cts";
            RiskyATMName = "Roses_Default_4cts";

            AddPlot(Brushes.Orange, "EMA9");            
        }

        private EMA EMA29Indicator_1m { get; set; }
        private EMA EMA21Indicator_1m { get; set; }
        private EMA EMA46Indicator_5m { get; set; }
        private EMA EMA51Indicator_5m { get; set; }
        private EMA EMA9Indicator_5m { get; set; }

        private ADXandDI ADXandDI { get; set; }

        //private WaddahAttarExplosion WaddahAttarExplosion_5m { get; set; }

        protected override void AddIndicators()
        {
            EMA29Indicator_1m = EMA(BarsArray[2], 29);
            EMA29Indicator_1m.Plots[0].Brush = Brushes.Red;

            EMA21Indicator_1m = EMA(BarsArray[2], 21);
            EMA21Indicator_1m.Plots[0].Brush = Brushes.Blue;

            EMA46Indicator_5m = EMA(BarsArray[1], 46);
            EMA51Indicator_5m = EMA(BarsArray[1], 51);
            EMA9Indicator_5m = EMA(BarsArray[1], 10);

            ADXandDI = ADXandDI(14, 25, 20);

            //WaddahAttarExplosion_5m = WaddahAttarExplosion(BarsArray[1]);

            AddChartIndicator(EMA29Indicator_1m);
            AddChartIndicator(EMA21Indicator_1m);

            AddChartIndicator(ADXandDI);
        }

        protected override void OnBarUpdate_StateHistorical(int barsPeriod)
        {
            if (barsPeriod == 1)
            {     
                //Values[0][0] = EMA46Indicator_5m.Value[0];
            }
            
        }

        protected override void OnNewBarCreated(int barsPeriod)
        {
            var index = GetBarIndex(barsPeriod);

            LocalPrint($"1st tick of the bar {barsPeriod}-mins {DateTime.Now} - Hi: {Highs[index][0]:N2}, Lo: {Lows[index][0]:N2}, Open: {Opens[index][0]:N2}, Close: {Closes[index][0]:N2}");
        }

        protected override void OnCurrentBarClosed(int barsPeriod)
        {
            var index = GetBarIndex(barsPeriod);

            LocalPrint($"Last tick of the bar {barsPeriod}-mins {DateTime.Now} - Hi: {Highs[index][0]:N2}, Lo: {Lows[index][0]:N2}, Open: {Opens[index][0]:N2}, Close: {Closes[index][0]:N2}");
        }
        protected override void OnRegularTick(int barsPeriod)
        {
            var index = GetBarIndex(barsPeriod);
            if (barsPeriod == 5)
            {
                var ema46 = EMA46Indicator_5m.Value[0];
                var ema51 = EMA51Indicator_5m.Value[0];
                var ema9 = EMA9Indicator_5m.Value[0];

                DrawLine("ema46", ema46, Brushes.Green, Brushes.Green, labelText: $"EMA46 (5): {ema46:N2}");
                DrawLine("ema51", ema51, Brushes.Red, Brushes.Red, textPosition: 3, labelText: $"EMA51 (5): {ema51:N2}");
                DrawLine("ema9", ema9, Brushes.Orange, Brushes.Orange, textPosition: -6, labelText: $"EMA9 (5): {ema9:N2}");                
            }
            else if (barsPeriod == 1)
            {
                var ema9 = EMA9Indicator_5m.Value[0];

                if (State == State.Realtime)
                {
                    Values[0][0] = ema9;
                }

                

                LocalPrint($"EMA9: {Values[0][0]:N2}");
            }
            

            



        }

        protected override TradeAction ShouldTrade()
        {
            return TradeAction.NoTrade; 
        }

        protected override void EnterOrderHistorical(TradeAction action)
        {
            
        }

        protected override void TransitionOrdersToLive()
        {
            throw new NotImplementedException();
        }

        protected override void EnterOrderRealtime(TradeAction action)
        {
            throw new NotImplementedException();
        }
    }
}
