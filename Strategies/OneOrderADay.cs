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

        [NinjaScriptProperty]
        [Display(Name = "Cách thức đặt lệnh",
           Description = "News - Stop Market, Open - Stop Limit",
           Order = 5, GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        public OrderType SetOrderType { get; set; }

        protected AtmStrategy UpsideATMStrategy { get; set; }

        private double UpsideStoplossTicks { get; set; }
        private double UpsideTargetTicks { get; set; }

        private double DownsideStoplossTicks { get; set; }
        private double DownsideTargetTicks { get; set; }


        protected AtmStrategy DownsideATMStrategy { get; set; }

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

                SetOrderType = OrderType.StopMarket;
            }
			else if (State == State.Configure)
			{
                AddDataSeries(BarsPeriodType.Minute, DATA_SERIE_5m);

                UpsideATMStrategy = StrategiesUtilities.ReadStrategyData(UpsideATMName, Print).AtmStrategy;
                UpsideStoplossTicks = UpsideATMStrategy.Brackets.Length > 0 ? UpsideATMStrategy.Brackets[0].StopLoss : 20; // Default 5 pts (20 ticks)
                UpsideTargetTicks = UpsideATMStrategy.Brackets.Length > 0 ? UpsideATMStrategy.Brackets[0].Target : 24; // Default 6 pts (24 ticks)

                DownsideATMStrategy = StrategiesUtilities.ReadStrategyData(DownsideATMName, Print).AtmStrategy;
                DownsideStoplossTicks = DownsideATMStrategy.Brackets.Length > 0 ? DownsideATMStrategy.Brackets[0].StopLoss : 20; // Default 5 pts (20 ticks)
                DownsideTargetTicks = DownsideATMStrategy.Brackets.Length > 0 ? DownsideATMStrategy.Brackets[0].Target : 24; // Default 6 pts (24 ticks)
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
                var timeNow = ToTime(Times[1][0]);

                LocalPrint($"{Times[1][0]} - {timeNow}");

                if (ToTime(Times[1][0]) == TimeToEnterOrder) // Begin of 8:30:00 candle
				{
					// Xác định điểm trên và dưới để vào lệnh					

					High_Value = Highs[1][0];
					Low_Value = Lows[1][0];

                    LocalPrint($"[OnBarUpdate] Enter orders. High: {High_Value.Value:N2}, Low: {Low_Value.Value:N2}");

                    BuyOrderInfo = EnterOrderPure(OrderAction.Buy, High_Value.Value, UpsideATMName, SetOrderType);
                    SellOrderInfo = EnterOrderPure(OrderAction.Sell, Low_Value.Value, DownsideATMName, SetOrderType);

                    TradingStatus = TradingStatus.PendingFill;
                    Finished = true;
                }                
                else if (ToTime(Times[1][0]) == TimeToResetNewDay)
                {
                    TradingStatus = CheckCurrentStatusBasedOnOrders();
                    // Reset để trade cho ngày hôm đó.
                    Finished = false;
                }
			}
		}

        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";

        protected double FilledPrice = -1;
        protected double StopLossPrice = -1; 
        protected double TargetPrice = -1;
        protected OrderAction CurOrderAction = OrderAction.Buy;

        private void CancelOtherSide(string orderId)
        {
            try
            {
                if (!string.IsNullOrEmpty(orderId))
                {
                    AtmStrategyCancelEntryOrder(orderId);
                }
            }
            catch (Exception e)
            {
                LocalPrint(e.Message);
            }            
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

        protected AtmSavedInfo EnterOrderPure(OrderAction action, double priceToSet, string atmStragtegyName, OrderType orderType)
        {
            var atmStrategyId = GetAtmStrategyUniqueId();
            var orderId = GetAtmStrategyUniqueId();

            double stopPrice = -1;

            var stopLossTick = action == OrderAction.Buy
                ? UpsideATMStrategy.Brackets[0].StopLoss
                : DownsideATMStrategy.Brackets[0].StopLoss;

            try
            {
                if (orderType == OrderType.StopMarket)
                {
                    stopPrice = action == OrderAction.Buy
                        ? priceToSet - TickSize * UpsideATMStrategy.Brackets[0].StopLoss
                        : priceToSet + TickSize * DownsideATMStrategy.Brackets[0].StopLoss;

                    AtmStrategyCreate(
                        action,
                        orderType,
                        priceToSet,
                        stopPrice,
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
                                LocalPrint($"[AtmStrategyCreate] ERROR : " + atmCallbackErrorCode);
                            }
                        });
                }
                else if (orderType == OrderType.StopLimit)
                {
                    stopPrice = priceToSet;

                    AtmStrategyCreate(
                        action,
                        orderType,
                        priceToSet,
                        stopPrice,
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
                }
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrderPure] - ERROR: {ex.Message}");
            }

            LocalPrint($"[EnterOrderPure] {action}, enter price: {priceToSet:N2}, stop price: {stopPrice:N2}. ");            

            
            return new AtmSavedInfo 
            { 
                AtmStrategyId = atmStrategyId, 
                OrderId = orderId
            };
        }

        private void SetStopLossIfNotExist(OrderAction action)
        {
            var orderInfo = action == OrderAction.Buy ? BuyOrderInfo : SellOrderInfo;
            {
                var stopOrder = GetAtmStrategyStopTargetOrderStatus("Stop1", orderInfo.AtmStrategyId);
                if (stopOrder?.Length == 0)
                {
                    LocalPrint($"Không tìm thấy Stop Loss order, add stop loss {UpsideStoplossTicks} ticks");
                    // Vì 1 lý do nào đó mà ko có stop order
                    SetStopLoss(CalculationMode.Ticks, UpsideStoplossTicks);
                }

                var targetOrder = GetAtmStrategyStopTargetOrderStatus("Target1", orderInfo.AtmStrategyId);
                if (targetOrder?.Length == 0)
                {
                    // Vì 1 lý do nào đó mà ko có stop order
                    LocalPrint($"Không tìm thấy Target order, add target {UpsideTargetTicks} ticks");
                    SetProfitTarget(CalculationMode.Ticks, UpsideTargetTicks);
                }
            }    
        }

        protected void LocalPrint(object val)
        {
            if (val.GetType() == typeof(string))
            {
                Print($"{Time?[0]}-" + val);
            }
            else
            {
                Print(val);
            }
        }

        private DateTime executionTime = DateTime.MinValue;
        private bool IsFirstData = true;
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)
            {
                return;
            }

            if (DateTime.Now.Subtract(executionTime).TotalMilliseconds < 500)
            {
                return;
            }

            executionTime = DateTime.Now;

            if (TradingStatus == TradingStatus.PendingFill)
            {
                LocalPrint($"High: {High_Value.Value}, Low: {Low_Value.Value}, current price: {updatedPrice:N2}, status: {TradingStatus}");

                // Kiểm tra 2 lệnh Entry
                var buyOrderStatus = GetAtmStrategyEntryOrderStatus(BuyOrderInfo.OrderId); // fill price, filled amount and order state
                if (buyOrderStatus?.Length == 3 && buyOrderStatus[2] == "Filled")
                {                    
                    TradingStatus = TradingStatus.OrderExists;
                    CurOrderAction = OrderAction.Buy;
                    FilledPrice = double.Parse(buyOrderStatus[0]);

                    LocalPrint($"Cancel lệnh SELL do đã vào lệnh Buy tại giá {FilledPrice:N2}");
                    CancelOtherSide(SellOrderInfo.OrderId);                    

                    StopLossPrice = FilledPrice - UpsideStoplossTicks * TickSize;
                    TargetPrice = FilledPrice + UpsideTargetTicks * TickSize;

                    SetStopLossIfNotExist(OrderAction.Buy);
                }

                var sellOrderStatus = GetAtmStrategyEntryOrderStatus(SellOrderInfo.OrderId); // fill price, filled amount and order state

                if (sellOrderStatus?.Length == 3 && sellOrderStatus[2] == "Filled")
                {
                    TradingStatus = TradingStatus.OrderExists;
                    CurOrderAction = OrderAction.Sell;
                    FilledPrice = double.Parse(sellOrderStatus[0]);

                    LocalPrint($"Cancel lệnh BUY do đã vào lệnh Sell tại giá {FilledPrice:N2}");
                    CancelOtherSide(BuyOrderInfo.OrderId);

                    StopLossPrice = FilledPrice + DownsideStoplossTicks * TickSize;
                    TargetPrice = FilledPrice - DownsideTargetTicks * TickSize;

                    SetStopLossIfNotExist(OrderAction.Sell);
                }
            }
            else if (TradingStatus == TradingStatus.OrderExists)
            {
                if (IsFirstData)
                {
                    SetStopLossIfNotExist(CurOrderAction);

                    IsFirstData = false;
                }
                else if (CurOrderAction == OrderAction.Buy && (updatedPrice >= TargetPrice + 2 || updatedPrice <= StopLossPrice))
                {
                    LocalPrint($"Close lệnh BUY, giá hiện tại {updatedPrice:N2} đã ở ngoài vùng {StopLossPrice:N2} và {TargetPrice:N2}");

                    AtmStrategyClose(BuyOrderInfo.AtmStrategyId);
                    TradingStatus = TradingStatus.Idle;
                }
                else if (CurOrderAction == OrderAction.Sell && (updatedPrice <= TargetPrice -2 || updatedPrice >= StopLossPrice))
                {
                    LocalPrint($"Close lệnh SELL, giá hiện tại {updatedPrice:N2} đã ở ngoài vùng {StopLossPrice:N2} và {TargetPrice:N2}");

                    AtmStrategyClose(SellOrderInfo.AtmStrategyId);
                    TradingStatus = TradingStatus.Idle;
                }

            }    



        }
    }
}
