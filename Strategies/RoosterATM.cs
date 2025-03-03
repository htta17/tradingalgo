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
using Rules1;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class RoosterATM : Rooster, IATMStrategy
    {
        public RoosterATM() : base("ROOSTER_ATM") { }

        const string ATMStrategy_Group = "ATM Information";
        private const string OrderEntryName = "Entry";
        private const string OrderStopName = "Stop";
        private const string OrderTargetName = "Target";

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

        private AtmStrategy FullSizeAtmStrategy { get; set; }

        private AtmStrategy HalfSizeAtmStrategy { get; set; }

        protected override void CloseExistingOrders()
        {
            base.CloseExistingOrders();
        }

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

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";
        }

        private TradingStatus tradingStatus { get; set; } = TradingStatus.Idle;

        protected override TradingStatus TradingStatus
        {
            get 
            {
                return tradingStatus;
            }
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            { 
                FullSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(FullSizeATMName).AtmStrategy;

                HalfSizeAtmStrategy = StrategiesUtilities.ReadStrategyData(HalfSizefATMName).AtmStrategy;
            }
        }

        protected override void TransitionOrdersToLive()
        {
            // Do nothing
        }

        protected override void MoveTargetOrStopOrder(double newPrice, Order order, bool isGainStop, string buyOrSell, string fromEntrySignal)
        {
            try
            {
                AtmStrategyChangeStopTarget(
                        isGainStop ? newPrice : 0,
                        isGainStop ? 0 : newPrice,
                        order.Name,
                        atmStrategyId);                

                var text = isGainStop ? "TARGET" : "LOSS";

                LocalPrint($"Dịch chuyển order [{order.Name}], id: {order.Id} ({text}), " +
                    $"{order.Quantity} contract(s) từ [{(isGainStop ? order.LimitPrice : order.StopPrice)}] " +
                    $"đến [{newPrice}] - {buyOrSell}");
            }
            catch (Exception ex)
            {
                LocalPrint($"[MoveTargetOrStopOrder] - ERROR: {ex.Message}");
            }
        }

        protected override void UpdateStopLossPrice(double newStopLossPrice)
        {
            StopLossPrice = newStopLossPrice;
        }

        private DateTime executionTime = DateTime.MinValue;
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100 || DateTime.Now.Subtract(executionTime).TotalSeconds < 1)
            {
                return;
            }

            executionTime = DateTime.Now;

            if (TradingStatus == TradingStatus.OrderExists)
            {
                // Khi giá đã ở dưới StopLossPrice
                if ((IsBuying && updatedPrice < StopLossPrice) || (IsSelling && updatedPrice > StopLossPrice))
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}"); 
                }
                else 
                {
                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains(OrderTargetName)).ToList();

                    if (targetOrders.Count() != 1 || stopOrders.Count() != 1)
                    {
                        return;
                    }

                    var targetOrder = targetOrders.First(); 

                    if ((IsBuying && updatedPrice > targetOrder.LimitPrice) || (IsSelling && updatedPrice < targetOrder.LimitPrice ))
                    {
                        tradingStatus = CheckCurrentStatusBasedOnOrders();

                        LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}");
                    }
                    else
                    {
                        MoveStopOrder(stopOrders.First(), updatedPrice, FilledPrice, IsBuying, IsSelling);

                        MoveTargetOrder(targetOrders.First(), updatedPrice, FilledPrice, IsBuying, IsSelling);
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

        private double StopLossPrice = -1;
        private double TargetPrice = -1; 
        protected override void EnterOrder(TradeAction tradeAction)
        {
            // Set global values
            CurrentTradeAction = tradeAction;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            double priceToSet = GetSetPrice(tradeAction);
            FilledPrice = priceToSet;

            LocalPrint($"Enter {action} at {Time[0]}, price to set: {priceToSet:N2}");

            var profitOrLoss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var isFullSize = profitOrLoss >= -ReduceSizeIfProfit; 

            var atmStrategyName = isFullSize ? FullSizeATMName : HalfSizefATMName;

            var atmStrategy = isFullSize ? FullSizeAtmStrategy : HalfSizeAtmStrategy; 

            // Get stop loss and target ID based on strategy 
            var stopLossTick = atmStrategy.Brackets[0].StopLoss;
            StopLossPrice = IsBuying ?
                priceToSet - stopLossTick * TickSize :
                priceToSet + stopLossTick * TickSize;

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
            AtmStrategyCancelEntryOrder(orderId);
        }        

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyRooster.txt");
        private string atmStrategyId = string.Empty;
        private string orderId = string.Empty;

        private void SaveAtmStrategyIdToFile(string strategyId)
        {
            try
            {
                File.WriteAllText(FileName, strategyId);
            }
            catch (Exception e)
            {
                LocalPrint(e.Message);
            }
        }

        private TradingStatus CheckCurrentStatusBasedOnOrders()
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

        /// <summary>
        /// Do ATM không dùng signal, nên 
        /// </summary>
        /// <param name="priceToSet"></param>
        /// <param name="targetInTicks"></param>
        /// <param name="stoplossInTicks"></param>
        /// <param name="atmStragtegyName"></param>
        /// <param name="quantity"></param>
        /// <param name="isBuying"></param>
        /// <param name="isSelling"></param>
        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling)
        {
            // Vào lệnh theo ATM 
            atmStrategyId = GetAtmStrategyUniqueId();
            orderId = GetAtmStrategyUniqueId();

            // Save to file, in case we need to pull [atmStrategyId] again
            SaveAtmStrategyIdToFile(atmStrategyId);

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;            

            // Enter a BUY/SELL order current price
            AtmStrategyCreate(
                action,
                OrderType.Limit, // Market price if fill immediately
                priceToSet,
                0,
                TimeInForce.Day,
                orderId,
                atmStragtegyName,
                atmStrategyId,
                (atmCallbackErrorCode, atmCallBackId) =>
                {
                    if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
                    {   
                        tradingStatus = TradingStatus.PendingFill;
                    }
                });
        }
    }
}
