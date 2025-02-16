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
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class RSIBollinger : BarClosedBaseClass<RSIBollingerAction, RSIBollingerAction>
    {
        protected override bool IsBuying
        { 
            get { return CurrentTradeAction == RSIBollingerAction.Buy; }
        }

        protected override bool IsSelling
        {
            get { return CurrentTradeAction == RSIBollingerAction.Sell; }
        }

        /// <summary>
        /// OverSoldValue
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Over sold value",
            Order = 1,
            GroupName = "Tiger parameters")]
        [Range(0, 49)]
        public int OverSoldValue { get; set; } = 30;


        /// <summary>
        /// OverSoldValue
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Over bought value",
            Order = 1,
            GroupName = "Tiger parameters")]
        [Range(51, 100)]
        public int OverBoughtValue { get; set; } = 70;

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [RSI + Bollinger (Reverse)]";

            OverBoughtValue = 70;
            OverSoldValue = 30;
        }
        
        private Bollinger bollinger1 { get; set; }
        private Bollinger bollinger2 { get; set; }
        private RSI rsi { get; set; }

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;


        protected override void OnStateChange()
		{
			base.OnStateChange();

            if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                bollinger1 = Bollinger(1, 20);
                bollinger1.Plots[0].Brush = bollinger1.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1.Plots[1].Brush = Brushes.DeepPink;

                bollinger2 = Bollinger(2, 20);
                bollinger2.Plots[0].Brush = bollinger2.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2.Plots[1].Brush = Brushes.DeepPink;

                rsi = RSI(14, 3);
                rsi.Plots[0].Brush = Brushes.DeepPink;
                rsi.Plots[1].Brush = Brushes.Gray;

                AddChartIndicator(bollinger1);
                AddChartIndicator(bollinger2);                

                AddChartIndicator(rsi); 
            }
            else if (State == State.Realtime)
            {
                try
                {
                    // Nếu có lệnh đang chờ thì cancel 
                    TransitionOrdersToLive();
                }
                catch (Exception e)
                {
                    LocalPrint("[OnStateChange] - ERROR" + e.Message);
                }
            }
        }

		protected override void OnBarUpdate()
		{
            //Add your custom strategy logic here.
            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                openPrice_5m = Open[0];
                closePrice_5m = Close[0];

                
            }
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            throw new NotImplementedException();
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            throw new NotImplementedException();
        }

        protected override double GetStopLossPrice(RSIBollingerAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetSetPrice(RSIBollingerAction tradeAction)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Half(RSIBollingerAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Full(RSIBollingerAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override RSIBollingerAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return RSIBollingerAction.NoTrade;
            }

            var rsi_5m = rsi.Value[0];

            if (rsi_5m > OverBoughtValue)
            { 
                
            }
            else if (rsi_5m < OverSoldValue)
            {
            }

            return RSIBollingerAction.NoTrade; 
        }
    }
}
