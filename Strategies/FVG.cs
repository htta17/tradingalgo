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
		}

		protected override void OnBarUpdate()
		{
			//Add your custom strategy logic here.
		}

        protected override double GetTargetPrice_Half(FVGTradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Full(FVGTradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
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
