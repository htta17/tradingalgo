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
	public class OneOrderADay : Strategy
	{
        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Upside ATM Name", Description = "Default ATM Strategy",
			Order = 1, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string UpsideATMName { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Downside ATMName",
            Description = "Strategy sử dụng khi loss/gain more than a half",
            Order = 2, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string DownsideATMName { get; set; }

        public const int DATA_SERIE_5m = 5;

        [NinjaScriptProperty]
        [Display(Name = "Time To Enter Order",
            Description = "Giờ vào lệnh (Ex: 8:30am (08:30:00) --> 083000, 2:00pm --> 140000)",
            Order = 3, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]        
        public int TimeToEnterOrder { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reset new day at: ",
            Description = "Giờ để bắt đầu tính ngày mới (Ex: 8:30am (08:30:00) --> 083000, 2:00pm --> 140000)",
            Order = 4, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        public int TimeToResetNewDay { get; set; }

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter only 1 a day, using NQ.";
				Name										= "OneOrderADay (Remember NQ)";
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

                UpsideATMName = "1_A_DAY_UP";
                DownsideATMName = "1_A_DAY_DOWN";

                TimeToEnterOrder = 08_30_00;
                TimeToResetNewDay = 08_00_00;

                TradingStatus = TradingStatus.Idle;
            }
			else if (State == State.Configure)
			{
                AddDataSeries(BarsPeriodType.Minute, DATA_SERIE_5m);
            }
		}

		private bool Finished = false;

		private double? High_Value = null;

        private double? Low_Value = null;

		private TradingStatus TradingStatus = TradingStatus.Idle;

        private AtmSavedInfo BuyOrderInfo {  get; set; }
        private AtmSavedInfo SellOrderInfo { get; set; }

        protected override void OnBarUpdate()
		{
            if (State != State.Realtime)
            {
                return; 
            }

			if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == DATA_SERIE_5m && !Finished) // 5 minute
			{
				if (ToTime(Times[1][0]) == TimeToEnterOrder) // Begin of 8:30:00 candle
				{
					// Xác định điểm trên và dưới để vào lệnh					

					High_Value = Highs[1][0];
					Low_Value = Lows[1][0];

                    Print($"[OnBarUpdate] Enter orders. High: {High_Value.Value:N2}, Low: {Low_Value.Value:N2}");

                    BuyOrderInfo = EnterOrderPure(OrderAction.Buy, High_Value.Value, UpsideATMName, OrderType.StopLimit);
                    SellOrderInfo = EnterOrderPure(OrderAction.Sell, Low_Value.Value, DownsideATMName, OrderType.StopLimit);

                    TradingStatus = TradingStatus.PendingFill;
                    Finished = true;
                }                
                else if (ToTime(Times[1][0]) == TimeToResetNewDay)
                {
                    TradingStatus = CheckCurrentStatusBasedOnOrders();
                    // Reset để trade cho ngày hôm đó.
                    Finished = true;
                }
			}
		}

        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";

        private void CancelOtherSide(string orderId)
        {            
            AtmStrategyCancelEntryOrder(orderId);
        }

        private void WriteText(string text)
        {
            Draw.TextFixed(
                this,
                "Distance",
                text,
                TextPosition.TopRight,
                Brushes.DarkBlue,            // Text color
                new SimpleFont("Arial", 12), // Font and size
                Brushes.DarkBlue,      // Background color
                Brushes.Transparent,      // Outline color
                0                         // Opacity (0 is fully transparent)
            );
        }

        protected TradingStatus CheckCurrentStatusBasedOnOrders()
        {
            var activeOrders = Account.Orders
                                .Where(c => c.OrderState == OrderState.Accepted || c.OrderState == OrderState.Working)
                                .Select(c => new { c.OrderState, c.Name, c.OrderType, c.OrderAction })
                                .ToList();            
            // Nếu không có active orders
            if (activeOrders.Count == 0)
            {
                return TradingStatus.Idle;
            }
            // Có 2 active orders, cả 2 đều là Entry và 2 actions ngược nhau (1 short, 1 long)
            else if (activeOrders.Count == 2 && activeOrders[0].Name == OrderEntryName && activeOrders[1].Name == OrderEntryName && activeOrders[0].OrderAction != activeOrders[1].OrderAction)
            {
                return TradingStatus.PendingFill;
            }
            else
            {
                return TradingStatus.OrderExists;
            }
        }

        protected AtmSavedInfo EnterOrderPure(OrderAction action, double priceToSet, string atmStragtegyName, OrderType orderType = OrderType.Limit)
        {
            var atmStrategyId = GetAtmStrategyUniqueId();
            var orderId = GetAtmStrategyUniqueId();

            AtmStrategyCreate(
                action,
                orderType,
                priceToSet,
                orderType == OrderType.StopLimit ? priceToSet : 0,
                TimeInForce.Day,
                orderId,
                atmStragtegyName,
                atmStrategyId,
                (atmCallbackErrorCode, atmCallBackId) =>
                {
                    if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                    {
                    }
                    else if (atmCallbackErrorCode != ErrorCode.NoError)
                    {
                        Print($"[AtmStrategyCreate] ERROR : " + atmCallbackErrorCode);
                    }
                });
            return new AtmSavedInfo 
            { 
                AtmStrategyId = atmStrategyId, 
                OrderId = orderId
            };
        }

        private DateTime executionTime = DateTime.MinValue;
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)
            {
                return;
            }

            if (DateTime.Now.Subtract(executionTime).TotalSeconds < 1)
            {
                return;
            }

            executionTime = DateTime.Now;

            // var timeSpan = DateTime.Now - Times[1][0];

            // WriteText(string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds));

            if (TradingStatus == TradingStatus.PendingFill)
            {
                Print($"High: {High_Value.Value}, Low: {Low_Value.Value}, current price: {updatedPrice:N2}, status: {TradingStatus}");

                if (updatedPrice > High_Value.Value) // Đã fill lệnh buy
                {
                    TradingStatus = TradingStatus.OrderExists; 
                    // Cancel lệnh sell 
                    CancelOtherSide(SellOrderInfo.OrderId);
                    Print("Cancel 1 lệnh ngược hướng");
                }
                else if (updatedPrice < Low_Value.Value) // Đã fill lệnh sell 
                {
                    TradingStatus = TradingStatus.OrderExists;
                    // Cancel lệnh buy 
                    CancelOtherSide(BuyOrderInfo.OrderId); 
                }
            }
        }
    }
}
