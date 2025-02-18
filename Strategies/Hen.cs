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
	public class Hen : Chicken
	{
        // Không cho phép trade ngược trend
        protected override bool InternalAllowReversalTrade
        {
            get { return true; }
        }

        protected override bool InternalAllowTrendingTrade
        {
            get { return false; }
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Hen (Chicken with Reverse ONLY)";
            Description = "[Hen] là giải thuật [Chicken] nhưng chỉ chạy Reverse";

            AllowReversalTrade = true;
            AllowTrendingTrade = false;
        }
    }
}
