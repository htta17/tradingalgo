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
	public class Roses : PriceChangedATMBasedClass<TradeAction>
	{
		public Roses() : base()
		{ 
		}

		protected override void OnStateChange()
		{
			base.OnStateChange();

            Description = "Roses (ATM realtime)";
            Name = "EMA 21/29 1-min frame, trending.";

            FullSizeATMName = "Roses_Default_4cts";
            HalfSizefATMName = "Roses_Default_4cts";
            RiskyATMName = "Roses_Default_4cts";
        }		

        protected override void OnNewBarOpen(int barsPeriod)
        {
			LocalPrint($"1st bar {barsPeriod}");
        }

        protected override void OnCurrentBarClose(int barsPeriod)
        {
            LocalPrint($"last bar {barsPeriod}");
        }

        protected override TradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }

        protected override void EnterOrder(TradeAction action)
        {
            throw new NotImplementedException();
        }
    }
}
