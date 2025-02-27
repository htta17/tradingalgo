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
	public class Rooster : Chicken
	{
        // Không cho phép trade ngược trend
        protected override bool InternalAllowReversalTrade
        { 
            get { return false; }
        }

        protected override bool InternalAllowTrendingTrade
        { 
            get { return true; }
        }

        public Rooster() : base("ROOSTER")
        {
            HalfPriceSignals = new HashSet<string>
            {   
                StrategiesUtilities.SignalEntry_TrendingHalf
            };

            EntrySignals = new HashSet<string>
            {
                StrategiesUtilities.SignalEntry_TrendingFull,
                StrategiesUtilities.SignalEntry_TrendingHalf,
            };
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster (Chicken with Trending ONLY)";
            Description = "[Rooster] là giải thuật [Chicken] nhưng chỉ chạy Trending";

            StopLossInTicks = 80;
            Target1InTicks = 40;
            Target2InTicks = 120;

            AllowReversalTrade = false;
            AllowTrendingTrade = true;
        }
    }
}
