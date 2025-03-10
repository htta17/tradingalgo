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
using NinjaTrader.CQG.ProtoBuf;
using System.IO;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class ADXBollinger : BarClosedBaseClass<ADXBollingerAction, ADXBollingerAction>
    {
        public ADXBollinger() : base("TIGER")
        {
            
        }        

        private const string Configuration_TigerParams_Name = "Tiger parameters";

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

        private TradingStatus tradingStatus { get; set; } = TradingStatus.Idle;

        /// <summary>
        /// - Nếu đang lỗ (&lt; $100) hoặc đang lời thì vào 2 contracts <br/>
        /// - Nếu đang lỗ > $100 thì vào 1 contract
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Reduce number of contract when profit less than (< 0):", Order = 2, GroupName = Configuration_TigerParams_Name)]
        public int ReduceSizeIfProfit { get; set; }


        /// <summary>
        /// - Nếu đang lỗ (&lt; $100) hoặc đang lời thì vào 2 contracts <br/>
        /// - Nếu đang lỗ > $100 thì vào 1 contract
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Enter order  ADX < ?:", Order = 2, GroupName = "Trading Parameters")]
        public int ADXToEnterOrder { get; set; }


        /// <summary>
        /// - Nếu đang lỗ (&lt; $100) hoặc đang lời thì vào 2 contracts <br/>
        /// - Nếu đang lỗ > $100 thì vào 1 contract
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Vào lệnh nếu ADX < ?:", Order = 2, GroupName = "Trading Parameters")]
        public int ADXToCancelOrder { get; set; }

        protected override TradingStatus TradingStatus
        {
            get
            {
                return tradingStatus;
            }
        }

        protected override void UpdatePendingOrderPure(double newPrice, double stopLossPrice, double targetFull)
        {
            
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [ADX + Bollinger (Reverse)]";
            Description = "";

            tradingStatus = TradingStatus.Idle;

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";

            ADXToEnterOrder = 18;
            ADXToCancelOrder = 22; 
        }
        
        private Bollinger bollinger1Indicator_5m { get; set; }
        private Bollinger bollinger2Indicator_5m { get; set; }
        private ADX adxIndicator_5m { get; set; }

        protected override bool IsBuying
        { 
            get 
            { 
                return CurrentTradeAction == ADXBollingerAction.SetBuyOrder; 
            } 
        }

        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == ADXBollingerAction.SetSellOrder;
            }
        }

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;

        protected double adx_5m = -1;

        protected double upperBB_5m = -1;
        protected double lowerBB_5m = -1;
        protected double middleBB_5m = -1;

        protected double upperStd2BB_5m = -1;
        protected double lowerStd2BB_5m = -1;

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
                bollinger1Indicator_5m = Bollinger(1, 20);
                bollinger1Indicator_5m.Plots[0].Brush = bollinger1Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                bollinger2Indicator_5m = Bollinger(2, 20);
                bollinger2Indicator_5m.Plots[0].Brush = bollinger2Indicator_5m.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2Indicator_5m.Plots[1].Brush = Brushes.DeepPink;

                adxIndicator_5m = ADX(14);                

                AddChartIndicator(bollinger1Indicator_5m);
                AddChartIndicator(bollinger2Indicator_5m);                

                AddChartIndicator(adxIndicator_5m);
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
                            atmStrategyId = arr[0];
                        }
                        else if (arr.Length == 2)
                        {
                            atmStrategyId = arr[0];
                            orderId = arr[1];

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

        protected string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyADX.txt");
        private string atmStrategyId = string.Empty;
        private string orderId = string.Empty;

        private void SaveAtmStrategyIdToFile(string strategyId, string orderId)
        {
            try
            {
                File.WriteAllText(FileName, $"{strategyId},{orderId}");
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

        protected override void EnterOrderPure(double priceToSet, int targetInTicks, double stoplossInTicks, string atmStragtegyName, int quantity, bool isBuying, bool isSelling)
        {
            // Vào lệnh theo ATM 
            atmStrategyId = GetAtmStrategyUniqueId();
            orderId = GetAtmStrategyUniqueId();

            // Save to file, in case we need to pull [atmStrategyId] again
            SaveAtmStrategyIdToFile(atmStrategyId, orderId);

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            // Enter a BUY/SELL order current price
            AtmStrategyCreate(
                action,
                OrderType.Limit,
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

        private double StopLossPrice = -1;
        private double TargetPrice = -1;
        protected virtual void EnterOrder(ADXBollingerAction tradeAction)
        {
            // Set global values
            CurrentTradeAction = tradeAction;

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

            LocalPrint($"Enter {action} at {Time[0]}, price to set: {priceToSet:N2}, stopLossTick: {stopLossTick}, finalTarget Tick: {targetTick}");

            StopLossPrice = IsBuying ?
                priceToSet - stopLossTick * TickSize :
                priceToSet + stopLossTick * TickSize;

            TargetPrice = IsBuying ?
                priceToSet + targetTick * TickSize :
                priceToSet - targetTick * TickSize;

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

            tradingStatus = TradingStatus.Idle;
        }

        protected override void OnBarUpdate()
		{
            //Add your custom strategy logic here.
            var passTradeCondition = CheckingTradeCondition(ValidateType.MaxDayGainLoss);
            if (!passTradeCondition)
            {
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                lowPrice_5m = Low[0];
                highPrice_5m = High[0];
                openPrice_5m = Open[0];
                closePrice_5m = Close[0];

                adx_5m = adxIndicator_5m.Value[0];

                upperBB_5m = bollinger1Indicator_5m.Upper[0];
                lowerBB_5m = bollinger1Indicator_5m.Lower[0];
                middleBB_5m = bollinger1Indicator_5m.Middle[0];

                upperStd2BB_5m = bollinger2Indicator_5m.Upper[0];
                lowerStd2BB_5m = bollinger2Indicator_5m.Lower[0];

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if (shouldTrade == ADXBollingerAction.SetBuyOrder || shouldTrade == ADXBollingerAction.SetSellOrder)
                    {
                        // Enter Order
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    // Kiểm tra các điều kiện để cancel lệnh

                    if (adx_5m > ADXToCancelOrder)
                    {
                        LocalPrint($"Price is greater than Bollinger middle, cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else if (CurrentTradeAction == ADXBollingerAction.SetBuyOrder && lowPrice_5m > middleBB_5m)
                    {
                        LocalPrint($"Price is greater than Bollinger middle, cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else if (CurrentTradeAction == ADXBollingerAction.SetSellOrder && highPrice_5m < middleBB_5m)
                    {
                        LocalPrint($"Price is smaller than Bollinger middle, Cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }
                    else 
                    {
                        var shouldTrade = ShouldTrade();

                        // Xem điều kiện có bị thay đổi gì không? 
                        if (shouldTrade == ADXBollingerAction.NoTrade)
                        {
                            // Do nothing, do việc cancel xảy ra khi adx_5m > [ADXToCancelOrder]
                        }
                        else if (shouldTrade == CurrentTradeAction)
                        {
                            // Nếu cùng chiều thì di chuyển điểm vào lệnh. 

                            
                        }    




                    }
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                {
                    
                }
            }
        }

        protected override bool IsHalfPriceOrder(Cbi.Order order)
        {
            throw new NotImplementedException();
        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            var updatedPrice = marketDataUpdate.Price;

            if (updatedPrice < 100)  // || DateTime.Now.Subtract(executionTime).TotalSeconds < 1)
            {
                return;
            }

            //executionTime = DateTime.Now;

            if (TradingStatus == TradingStatus.OrderExists)
            {
                var buyPriceIsOutOfRange = IsBuying && (updatedPrice < StopLossPrice || updatedPrice > TargetPrice);
                var sellPriceIsOutOfRange = IsSelling && (updatedPrice > StopLossPrice || updatedPrice < TargetPrice);

                // Khi giá đã ở ngoài range (stoploss, target)
                if (buyPriceIsOutOfRange || sellPriceIsOutOfRange)
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}");
                }
                else
                {
                    var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();
                    var targetOrders = Account.Orders.Where(order => order.OrderState == OrderState.Working && order.Name.Contains(OrderTargetName)).ToList();

                    var countStopOrder = stopOrders.Count;
                    var countTargetOrder = stopOrders.Count;

                    LocalPrint($"countStopOrder: {countStopOrder}, countTargetOrder: {countTargetOrder}");

                    if (countStopOrder == 0 || countTargetOrder == 0)
                    {
                        tradingStatus = TradingStatus.Idle;
                        return;
                    }
                    else if (countStopOrder == 1 && countTargetOrder == 1)
                    {
                        var targetOrder = targetOrders.First();
                        var stopLossOrder = stopOrders.First();

                        TargetPrice = targetOrder.LimitPrice;
                        StopLossPrice = stopLossOrder.StopPrice;

                        MoveTargetOrder(targetOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);

                        MoveStopOrder(stopLossOrder, updatedPrice, FilledPrice, IsBuying, IsSelling);
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

        protected override bool IsFullPriceOrder(Cbi.Order order)
        {
            throw new NotImplementedException();
        }

        protected override double GetStopLossPrice(ADXBollingerAction tradeAction, double setPrice)
        {
            var stopLoss = StopLossInTicks * TickSize;
            
            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice - stopLoss
                : setPrice + stopLoss;
        }

        protected override double GetSetPrice(ADXBollingerAction tradeAction)
        {
            if (tradeAction == ADXBollingerAction.SetBuyOrder)
            {
                return lowerStd2BB_5m; 
            }
            else if (tradeAction == ADXBollingerAction.SetSellOrder)
            {
                return upperStd2BB_5m;
            }
            return middleBB_5m;
        }

        protected override double GetTargetPrice_Half(ADXBollingerAction tradeAction, double setPrice)
        {
            var target1 = TickSize * Target1InTicks; 

            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice + target1
                : setPrice - target1; 
        }

        protected override double GetTargetPrice_Full(ADXBollingerAction tradeAction, double setPrice)
        {
            var target2 = TickSize * Target2InTicks;

            return tradeAction == ADXBollingerAction.SetBuyOrder
                ? setPrice + target2
                : setPrice - target2;
        }

        protected override ADXBollingerAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return ADXBollingerAction.NoTrade;
            }

            if (adx_5m < ADXToEnterOrder) 
            {
                if (lowPrice_5m > middleBB_5m)
                {
                    return ADXBollingerAction.SetSellOrder;
                }
                else if (highPrice_5m < middleBB_5m)
                {
                    return ADXBollingerAction.SetBuyOrder;
                }
            } 

            return ADXBollingerAction.NoTrade; 
        }
    }
}
