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
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class RoosterATM : Rooster, IATMStrategy
    {
        public RoosterATM() : base("ROOSTER_ATM") { }

        const string ATMStrategy_Group = "ATM Information";

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Default ATM Strategy", Description = "Default ATM Strategy", Order = 1,
            GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string FullATMName { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy",
            Description = "Strategy sử dụng khi loss/gain more than a half",            
            Order = 2, GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfATMName { get; set; }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster ATM (Chicken with Trending ONLY)";
            Description = "[Rooster ATM] là giải thuật [Chicken] nhưng chỉ chạy Trending, dùng ATM Strategy để vào lệnh";

            StopLossInTicks = 120;
            Target1InTicks = 40;
            Target2InTicks = 120;

            AllowReversalTrade = false;
            AllowTrendingTrade = true;

            FullATMName = "Rooster_Default_4cts";
            HalfATMName = "Rooster_Default_2cts";
        }

        protected override TradingStatus TradingStatus => base.TradingStatus;

        protected override void OnStateChange()
        {
            base.OnStateChange();
        }

        protected override void TransitionOrdersToLive()
        {
            base.TransitionOrdersToLive();
        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            base.OnMarketData(marketDataUpdate);
        }

        protected override void EnterOrder(TradeAction tradeAction)
        {
            base.EnterOrder(tradeAction);
        }

        protected override void CancelAllPendingOrder()
        {
            base.CancelAllPendingOrder();
        }

        protected override void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            base.MoveStopOrder(stopOrder, updatedPrice, filledPrice, isBuying, isSelling);
        }

        protected override void MoveTargetOrder(Order targetOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            base.MoveTargetOrder(targetOrder, updatedPrice, filledPrice, isBuying, isSelling);
        }

        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string signal, int quantity, bool isBuying, bool isSelling)
        {
            base.EnterOrderPure(priceToSet, targetInTicks, stoplossInTicks, signal, quantity, isBuying, isSelling);
        }
    }
}
