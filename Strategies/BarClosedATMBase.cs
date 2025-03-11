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
	public abstract class BarClosedATMBase<T1> : BarClosedBaseClass<T1>
    {
		public BarClosedATMBase(string name) : base(name) { }

        public BarClosedATMBase() : this("[BASED ATM]")
        {

        }

        protected override void CloseExistingOrders()
        {
            LocalPrint($"[CloseExistingOrders]");
            if (!string.IsNullOrEmpty(AtmStrategyId))
            {
                AtmStrategyClose(AtmStrategyId);
            }
            tradingStatus = TradingStatus.Idle;
        }

        protected override Order GetPendingOrder()
        {
            var order = Account.Orders.FirstOrDefault(c => c.Name.Contains(OrderEntryName) && (c.OrderState == OrderState.Working || c.OrderState == OrderState.Accepted));

            return order;
        }

        protected override void TransitionOrdersToLive()
        {
            // Since we cannot use ATM in History, we don't have to do anything here.
        }

        protected const string Configuration_TigerParams_Name = "Based ATM Parameters";

        protected const string ATMStrategy_Group = "ATM Information";
        protected const string OrderEntryName = "Entry";
        protected const string OrderStopName = "Stop";
        protected const string OrderTargetName = "Target";

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Default ATM Strategy", Description = "Default ATM Strategy", Order = 1,
            GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string FullSizeATMName { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduced size Strategy",
            Description = "Strategy sử dụng khi loss/gain more than a half",
            Order = 2, GroupName = ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string HalfSizefATMName { get; set; }

        /// <summary>
        /// - Nếu đang lỗ (&lt; $100) hoặc đang lời thì vào 2 contracts <br/>
        /// - Nếu đang lỗ > $100 thì vào 1 contract
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduce number of contract when profit less than (< 0):", Order = 2, GroupName = Configuration_TigerParams_Name)]
        public int ReduceSizeIfProfit { get; set; }
        protected AtmStrategy FullSizeAtmStrategy { get; set; }

        protected AtmStrategy HalfSizeAtmStrategy { get; set; }

        protected TradingStatus tradingStatus { get; set; } = TradingStatus.Idle;

        protected string AtmStrategyId = string.Empty;

        protected string OrderId = string.Empty;

        protected override TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected override void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double target)
        {
            if (Math.Abs(FilledPrice - newPrice) > 0.5)
            {
                FilledPrice = newPrice;
                StopLossPrice = stopLossPrice;
                TargetPrice = target;

                try
                {
                    LocalPrint($"Trying to modify waiting order, new Price: {newPrice:N2}, new stop loss: {stopLossPrice:N2}, new target: {target:N2}");

                    AtmStrategyChangeEntryOrder(newPrice, stopLossPrice, OrderId);
                }
                catch (Exception ex)
                {
                    LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
                }
            }
        }
        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [ADX + Bollinger (Reverse)]";
            Description = "";

            tradingStatus = TradingStatus.Idle;

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";

            SetBreakEvenManually = false;
        }       

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);

                FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName).AtmStrategy;

                HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName).AtmStrategy;
            }
            else if (State == State.DataLoaded)
            {
               
            }
            else if (State == State.Realtime)
            {
                // Load thông tin liên quan đến
                if (File.Exists(FileName))
                {
                    try
                    {
                        var text = File.ReadAllText(FileName);

                        var arr = text.Split(',');

                        if (arr.Length == 1)
                        {
                            AtmStrategyId = arr[0];
                        }
                        else if (arr.Length == 2)
                        {
                            AtmStrategyId = arr[0];
                            OrderId = arr[1];

                            tradingStatus = CheckCurrentStatusBasedOnOrders();
                            LocalPrint($"Initial status - {tradingStatus}");
                        }
                    }
                    catch (Exception e)
                    {
                        Print(e.Message);
                    }
                }
            }
        }

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyATMBase.txt");
        

        protected void SaveAtmStrategyIdToFile(string strategyId, string orderId)
        {
            try
            {
                File.WriteAllText(FileName, $"{strategyId},{orderId}");

                LocalPrint($"Saved strategyId [{strategyId}] and orderId [{orderId}] to file");
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
                                .Select(c => new { c.OrderState, c.Name, c.OrderType })
                                .ToList();

            if (activeOrders.Count == 0)
            {
                return TradingStatus.Idle;
            }
            else if (activeOrders.Count == 1 && activeOrders[0].Name == OrderEntryName)
            {
                return TradingStatus.PendingFill;
            }
            else
            {
                return TradingStatus.OrderExists;
            }
        }

        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling)
        {
            // Vào lệnh theo ATM 
            AtmStrategyId = GetAtmStrategyUniqueId();
            OrderId = GetAtmStrategyUniqueId();

            // Save to file, in case we need to pull [atmStrategyId] again
            SaveAtmStrategyIdToFile(AtmStrategyId, OrderId);

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            FilledPrice = priceToSet;

            // Enter a BUY/SELL order current price
            AtmStrategyCreate(
                action,
                OrderType.Limit,
                priceToSet,
                0,
                TimeInForce.Day,
                OrderId,
                atmStragtegyName,
                AtmStrategyId,
                (atmCallbackErrorCode, atmCallBackId) =>
                {
                    if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == AtmStrategyId)
                    {
                        tradingStatus = TradingStatus.PendingFill;                        
                    }
                });
        }

        DateTime enterOrderTimed = DateTime.MinValue; 

        protected double StopLossPrice = -1;
        protected double TargetPrice = -1;
        protected override void EnterOrder(T1 tradeAction)
        {
            if (State != State.Realtime || DateTime.Now.Subtract(enterOrderTimed).TotalSeconds < 5)
            {
                return;
            }
            enterOrderTimed = DateTime.Now;

            // Set global values
            CurrentTradeAction = tradeAction;

            EnteredBarIndex_5m = CurrentBarIndex_5m;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            double priceToSet = GetSetPrice(tradeAction);

            var profitOrLoss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var isFullSize = profitOrLoss >= -ReduceSizeIfProfit;

            var atmStrategyName = isFullSize ? FullSizeATMName : HalfSizefATMName;

            var atmStrategy = isFullSize ? FullSizeAtmStrategy : HalfSizeAtmStrategy;

            // Get stop loss and target ID based on strategy 
            FilledPrice = priceToSet;

            var stopLossTick = atmStrategy.Brackets[0].StopLoss;
            var targetTick = IsBuying ? atmStrategy.Brackets.Max(c => c.Target) : atmStrategy.Brackets.Min(c => c.Target);            

            StopLossPrice = IsBuying ?
                priceToSet - stopLossTick * TickSize :
                priceToSet + stopLossTick * TickSize;

            TargetPrice = IsBuying ?
                priceToSet + targetTick * TickSize :
                priceToSet - targetTick * TickSize;

            LocalPrint($"Enter {action} at {Time[0]}, price to set: {priceToSet:N2}, StopLossPrice: {StopLossPrice:N2}, target: {TargetPrice:N2}, stopLossTick: {stopLossTick}, finalTarget Tick: {targetTick}");

            try
            {
                EnterOrderPure(priceToSet, 0, 0, atmStrategyName, 0, IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override void CancelAllPendingOrder()
        {
            AtmStrategyCancelEntryOrder(OrderId);

            tradingStatus = TradingStatus.Idle;
        }

        DateTime executionTime = DateTime.MinValue;
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

            if (TradingStatus == TradingStatus.OrderExists)
            {
                var buyPriceIsOutOfRange = IsBuying && (updatedPrice < StopLossPrice || updatedPrice > TargetPrice);
                var sellPriceIsOutOfRange = IsSelling && (updatedPrice > StopLossPrice || updatedPrice < TargetPrice);

                // Khi giá đã ở ngoài range (stoploss, target)
                if (buyPriceIsOutOfRange || sellPriceIsOutOfRange)
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}. TargetPrice: {TargetPrice:N2}, " +
                        $"updatedPrice:{updatedPrice:N2}, StopLossPrice: {StopLossPrice:N2}, " +
                        $"buyPriceIsOutOfRange: {buyPriceIsOutOfRange}, :sellPriceIsOutOfRange: {sellPriceIsOutOfRange}. ");
                }
                else
                {
                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains(OrderTargetName)).ToList();

                    var countStopOrder = stopOrders.Count;
                    var countTargetOrder = targetOrders.Count;                    

                    if (countStopOrder == 0 || countTargetOrder == 0)
                    {
                        tradingStatus = TradingStatus.Idle;
                        return;
                    }
                    else if (countStopOrder == 1 && countTargetOrder == 1)
                    {
                        var targetOrder = targetOrders.LastOrDefault();
                        var stopLossOrder = stopOrders.LastOrDefault();

                        if (targetOrder != null)
                        {
                            TargetPrice = targetOrder.LimitPrice;
                            MoveTargetOrder(targetOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                        }

                        if (stopLossOrder != null)
                        {
                            StopLossPrice = stopLossOrder.StopPrice;
                            MoveStopOrder(stopLossOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
                        }
                    }
                }
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                if ((IsBuying && updatedPrice < FilledPrice) || (IsSelling && updatedPrice > FilledPrice))
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: PendingFill, new TradingStatus: {TradingStatus}");
                }
            }
        }

        protected override bool IsFullPriceOrder(Order order)
        {
            // Don't need to implement
            throw new NotImplementedException();
        }

        protected override bool IsHalfPriceOrder(Order order)
        {
            // Don't need to implement
            throw new NotImplementedException();
        }

        protected override void MoveTargetOrStopOrder(double newPrice, Cbi.Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
        {
            try
            {
                var text = isGainStop ? "TARGET" : "LOSS";
                LocalPrint($"Dịch chuyển order [{order.Name}], id: {order.Id} ({text}), " +
                    $"{order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice : order.StopPrice)}] " +
                    $"đến [{newPrice}] - {buyOrSell}");

                AtmStrategyChangeStopTarget(
                        isGainStop ? newPrice : 0,
                        isGainStop ? 0 : newPrice,
                        order.Name,
                        AtmStrategyId);                

                if (isGainStop)
                {
                    TargetPrice = newPrice;
                }
                else
                {
                    StopLossPrice = newPrice;
                }
            }
            catch (Exception ex)
            {
                LocalPrint($"[MoveTargetOrStopOrder] - ERROR: {ex.Message}");
            }
        }
    }
}