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
    public class FVG : BarClosedBaseClass<FVGTradeAction>
	{
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = @"Fair Value Gap";
                Name = "FVG";
                BarsRequiredToTrade = 10;
                Target1InTicks = 40;
            }
            else if (State == State.Configure)
            {
                ClearOutputWindow();

                // Add data for trading
                AddDataSeries(BarsPeriodType.Minute, 5);

                currentTradeAction = FVGTradeAction.NoTrade;
            }
        }

        protected override void OnBarUpdate()
        {
            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {                
                if (TradingStatus == TradingStatus.Idle)
                {
                    // Find the FVG value 
                }
                
            }
        }


        protected override double GetTargetPrice_Half(FVGTradeAction tradeAction, double setPrice)
        {
            return currentTradeAction == FVGTradeAction.Buy 
                ? setPrice + (Target1InTicks * TickSize)
                : -setPrice - (Target1InTicks * TickSize);
        }

        protected override double GetTargetPrice_Full(FVGTradeAction tradeAction, double setPrice)
        {
            return currentTradeAction == FVGTradeAction.Buy
                ? setPrice + (Target2InTicks * TickSize)
                : -setPrice - (Target2InTicks * TickSize);
        }

        protected override FVGTradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }

        protected override double GetSetPrice(FVGTradeAction tradeAction)
        {
            throw new NotImplementedException();
        }
    }
}
