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
	public class TestStopLossGainMoving : BarClosedATMBase<TradeAction>
	{
        protected override bool IsBuying => throw new NotImplementedException();

        protected override bool IsSelling => throw new NotImplementedException();

        protected override void OnStateChange()
		{
			base.OnStateChange();

			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "TestStopLossGainMoving";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
			}
			else if (State == State.Configure)
			{
			}
		}

        protected override void SetDefaultProperties()
        {
			FullSizeATMName = "TestingMoving";

			HalfSizefATMName = "TestingMoving";
        }

        protected override double GetSetPrice(TradeAction tradeAction)
        {
			return Close[0];
        }

        protected override void OnBarUpdate()
		{
			tradingStatus = CheckCurrentStatusBasedOnOrders();

			if (TradingStatus == TradingStatus.Idle)
			{
				var lastCandleIsRed = CandleUtilities.IsRedCandle(Close[0], Open[0]);
				if (lastCandleIsRed)
				{
					EnterOrder(TradeAction.Sell_Trending);
				}
				else
				{
					EnterOrder(TradeAction.Buy_Trending);
				}
			}
			
		}
		
		protected override void OnOrderUpdate(
			Order order, 
			double limitPrice, 
			double stopPrice, 
			int quantity, 
			int filled, 
			double averageFillPrice, 
			OrderState orderState, 
			DateTime time, 
			ErrorCode error, 
			string comment) 
		{
	   		Print($"[OnOrderUpdate] - Id: {order.Id}, limitPrice: {limitPrice:F2}, stop: {stopPrice:F2} {orderState}");		
	    }

        protected override double GetStopLossPrice(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Half(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override double GetTargetPrice_Full(TradeAction tradeAction, double setPrice)
        {
            throw new NotImplementedException();
        }

        protected override TradeAction ShouldTrade()
        {
            throw new NotImplementedException();
        }
    }
}
