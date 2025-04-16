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
	public class Kitty : BarClosedATMBase<EMA2129OrderDetail>, IATMStrategy
    {
        protected TimeFrameToTrade Configured_TimeFrameToTrade { get; set; }

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Ricky ATM Strategy", Description = "Ricky ATM Strategy", Order = 2,
            GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string RiskyAtmStrategyName { get; set; }

        protected AtmStrategy RiskyAtmStrategy { get; set; }

        /// <summary>
        /// Chốt lời khi cây nến lớn hơn giá trị này (points), current default value: 60 points.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Chốt lời khi cây nến > (points):",
            Description = "Nếu TRUE: Nếu cây nến lớn hơn [giá trị] thì đóng lệnh.",
            Order = 2, GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]        
        public int CloseOrderWhenCandleGreaterThan { get; set; }

        #region Indicators
        protected EMA EMA29Indicator_1m { get; set; }
        protected EMA EMA21Indicator_1m { get; set; }
        protected EMA EMA46Indicator_5m { get; set; }
        protected EMA EMA51Indicator_5m { get; set; }
        protected EMA EMA10Indicator_5m { get; set; }
        protected ADXandDI ADXandDI { get; set; }
        protected MACD MACD { get; set; }
        #endregion
        private EMA2129Status EMA2129Status { get; set; }

        /// <summary>
        /// Giá trị này chỉ nên lưu 2 giá trị là [Above] và [Below] <br/>
        /// Khi giá trị [PreviousPosition] là [Above], và cây nến hiện tại là [Below] thì mới reset việc EnteredOrder <br/>
        /// hoặc khi giá trị [PreviousPosition] là [Below], và cây nến hiện tại là [Above] thì mới reset việc EnteredOrder <br/>
        /// Trong trường hợp giá trị [PreviousPosition] == vị trí cây nến hiện tại thì không cần reset order.
        /// </summary>
        private EMA2129Position PreviousPosition { get; set; } = EMA2129Position.Unknown; 

        [NinjaScriptProperty]
        [Display(Name = "Risky Strategy",
            Description = "Strategy sử dụng khi lệnh là risky",
            Order = 1, GroupName = StrategiesUtilities.Configuration_General_Name)]
        public int MaxiumOrderBeforeReset { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Value to Enter Order:",
            Description = "Nếu ADX value > [giá trị]: Enter order",
            Order = 2, GroupName = StrategiesUtilities.Configuration_General_Name)]
        public int ADXValueToENTEROrder { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ADX Value to cancel Order:",
            Description = "Nếu ADX value < [giá trị]: Cancel order",
            Order = 3, GroupName = StrategiesUtilities.Configuration_General_Name)]
        public int ADXValueToCANCELOrder { get; set; }

        protected override void AddCustomDataSeries()
        {
            AddDataSeries(BarsPeriodType.Minute, 5);
            AddDataSeries(BarsPeriodType.Minute, 1);
        }

        protected int GetBarIndex(int barsPeriod)
        {
            return barsPeriod == 5 ? 1 : 2;
        }

        protected override void OnBarUpdate()
        {
            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();

            // Hiển thị các đường indicators
            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                // Hiển thị indicators khung 5 phút
                try
                {
                    // Print(EMA9Indicator_5m.Value[0]);
                    Values[0][0] = EMA10Indicator_5m.Value[0];
                    Values[1][0] = EMA46Indicator_5m.Value[0];
                    Values[2][0] = EMA51Indicator_5m.Value[0];
                }
                catch (Exception ex)
                {
                    LocalPrint("OnBarUpdate: ERROR:" + ex.Message);
                }
            }

            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }
            if (BarsInProgress == 0)
            {
                // Current View --> Do nothing
                return;
            }

            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);                       

                // Cập nhật lại giá trị cây nến
                var index = GetBarIndex(BarsPeriod.Value);

                var high = High[0];
                var low = Low[0];
                var open = Open[0];
                var close = Close[0];
                
                var ema21Val = EMA21Indicator_1m.Value[0];
                var ema29Val = EMA29Indicator_1m.Value[0];
                var ema10Val = EMA10Indicator_5m.Value[0];                

                /*
                // Remember these 3 variable are the same right now
                var ema21Val = EMA21Indicator_1m.Value[0];
                var ema29Val = EMA21Indicator_1m.Value[0];
                var ema10Val = EMA21Indicator_1m.Value[0];
                */                

                var minValue = StrategiesUtilities.MinOfArray(ema21Val, ema29Val, ema10Val);
                var maxValue = StrategiesUtilities.MaxOfArray(ema21Val, ema29Val, ema10Val);                

                // Trạng thái
                if (high > maxValue && low < minValue) // Cross EMA lines
                {
                    LocalPrint($"New status: CROSSING");
                    EMA2129Status.SetPosition(EMA2129Position.Crossing, CurrentBar);
                }
                else if (high < minValue && minValue - high > 5 && EMA2129Status.Position != EMA2129Position.Below)
                {
                    LocalPrint($"New status: BELOW");
                    EMA2129Status.SetPosition(EMA2129Position.Below, CurrentBar, PreviousPosition != EMA2129Position.Below);

                    PreviousPosition = EMA2129Position.Below; 
                }
                else if (low > maxValue && low - maxValue > 5 && EMA2129Status.Position != EMA2129Position.Above)
                {
                    LocalPrint($"New status: ABOVE");
                    EMA2129Status.SetPosition(EMA2129Position.Above, CurrentBar, PreviousPosition != EMA2129Position.Above);

                    PreviousPosition = EMA2129Position.Above;
                }

                BasicActionForTrading(TimeFrameToTrade.OneMinute);
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                // Do nothing for now
            }
        }

        protected override bool IsBuying
        {
            get { return CurrentTradeAction.Action == GeneralTradeAction.Buy; }
        }

        protected override bool IsSelling
        {
            get { return CurrentTradeAction.Action == GeneralTradeAction.Sell; }
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

            if (TradingStatus == TradingStatus.OrderExists)
            {
                var buyPriceIsOutOfRange = IsBuying && (updatedPrice < StopLossPrice || updatedPrice > TargetPrice_Full);
                var sellPriceIsOutOfRange = IsSelling && (updatedPrice > StopLossPrice || updatedPrice < TargetPrice_Full);

                // Khi giá đã ở ngoài range (stoploss, target)
                if (buyPriceIsOutOfRange || sellPriceIsOutOfRange)
                {
                    tradingStatus = CheckCurrentStatusBasedOnOrders();

                    LocalPrint($"Last TradingStatus: OrderExists, new TradingStatus: {TradingStatus}. TargetPrice: {TargetPrice_Full:N2}, " +
                        $"updatedPrice:{updatedPrice:N2}, StopLossPrice: {StopLossPrice:N2}, " +
                        $"buyPriceIsOutOfRange: {buyPriceIsOutOfRange}, :sellPriceIsOutOfRange: {sellPriceIsOutOfRange}. ");

                    OnMarketData_OrderExists(updatedPrice);
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
                            TargetPrice_Full = targetOrder.LimitPrice;
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

                OnMarketData_PendingFill(updatedPrice);
            }
            else if (TradingStatus == TradingStatus.WatingForCondition)
            {
            }
        }

        /// <summary>
        /// Display Volume Indicator
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Hiển thị volume:",
            Description = "Hiển thị chỉ báo Volume trên chart",
            Order = 1, GroupName = StrategiesUtilities.Configuration_DisplayIndicators)]
        public bool DisplayIndicators { get; set; }

        public Kitty() : base("KITTY")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyKitty.txt");            
            Configured_TimeFrameToTrade = TimeFrameToTrade.OneMinute;
        }
        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Kitty";
            Description = "[Kitty] là giải thuật [Rooster], được viết riêng cho my love, Phượng Phan.";

            FullSizeATMName = "Kitty_Default_4cts";
            HalfSizefATMName = "Kitty_Default_2cts";
            RiskyAtmStrategyName = "Kitty_Risky";

            StartDayTradeTime = new TimeSpan(2, 10, 0); // 9:10:00 am 
            EndDayTradeTime = new TimeSpan(23, 50, 0); // 2:00:00 pm
            EMA2129Status = new EMA2129Status();

            AddPlot(Brushes.Green, "EMA9_5m");
            AddPlot(Brushes.Red, "EMA46_5m");
            AddPlot(Brushes.Black, "EMA51_5m");

            ADXValueToCANCELOrder = 18;
            ADXValueToENTEROrder = 22; 
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA29Indicator_1m = EMA(BarsArray[2], 29);
            EMA29Indicator_1m.Plots[0].Brush = Brushes.Red;

            EMA21Indicator_1m = EMA(BarsArray[2], 21);
            EMA21Indicator_1m.Plots[0].Brush = Brushes.Blue;

            EMA46Indicator_5m = EMA(BarsArray[1], 46);
            EMA51Indicator_5m = EMA(BarsArray[1], 51);
            EMA10Indicator_5m = EMA(BarsArray[1], 10);

            ADXandDI = ADXandDI(BarsArray[2], 14, ADXValueToENTEROrder, ADXValueToCANCELOrder);

            AddChartIndicator(EMA29Indicator_1m);
            AddChartIndicator(EMA21Indicator_1m);

            AddChartIndicator(ADXandDI);
        }

        protected override void BasicActionForTrading(TimeFrameToTrade timeFrameToTrade)
        {
            // Make sure each stratergy have each own time frame to trade
            if (timeFrameToTrade != Configured_TimeFrameToTrade)
            {
                return;
            }

            if (TradingStatus == TradingStatus.Idle)
            {
                var shouldTrade = ShouldTrade();

                LocalPrint($"Check trading condition, result: {shouldTrade.Action}");
                
                if (shouldTrade.Action != GeneralTradeAction.NoTrade && !EMA2129Status.EnteredOrder) // Nếu chưa enter order thì mới enter order
                {
                    EMA2129Status.SetEnteredOrder();

                    EnterOrder(shouldTrade);
                }
            }
            else if (TradingStatus == TradingStatus.PendingFill)
            {
                UpdatePendingOrder();
            }
            else if (TradingStatus == TradingStatus.OrderExists)
            {
                
            }
        }

        protected override EMA2129OrderDetail ShouldTrade()
        {
            var answer = new EMA2129OrderDetail
            {
                Action = GeneralTradeAction.NoTrade,
                Postition = EMA2129OrderPostition.NoTrade,
                Sizing = EMA2129SizingEnum.Small
            };

            if (Time[0].TimeOfDay < StartDayTradeTime || Time[0].TimeOfDay > EndDayTradeTime)
            {
                LocalPrint($"Thời gian trade được thiết lập từ {StartDayTradeTime} to {EndDayTradeTime} --> No Trade.");
                return answer;
            }
            else if (EMA2129Status.Position != EMA2129Position.Above && EMA2129Status.Position != EMA2129Position.Below)
            {
                LocalPrint($"Status: {EMA2129Status.Position} --> No Trade");
                return answer;
            }
           
            var adxVal = ADXandDI.Value[0];
            // Nếu giá trị ADX đang ở dưới [ADXValueToCancelOrder]           
            if (adxVal < ADXValueToENTEROrder)
            {
                LocalPrint($"({adxVal:N2}) adxVal < ADXValueToENTEROrder  ({ADXValueToCANCELOrder})--> No Trade.");
                // No trade
                return answer;
            }
            else 
            {
                answer.Action = EMA2129Status.Position == EMA2129Position.Below ? GeneralTradeAction.Sell 
                    : EMA2129Status.Position == EMA2129Position.Above ? GeneralTradeAction.Buy : 
                    GeneralTradeAction.NoTrade;
                answer.Postition = EMA2129OrderPostition.EMA21;
                answer.Sizing = EMA2129SizingEnum.Big;
            }
            return answer;
        }

        protected override (AtmStrategy, string) GetAtmStrategyByPnL(EMA2129OrderDetail tradeAction)
        {
            /*
            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf =
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));
            */
            if (tradeAction.Sizing == EMA2129SizingEnum.Big)
            {
                return (FullSizeAtmStrategy, FullSizeATMName);
            }
            else if (tradeAction.Sizing == EMA2129SizingEnum.Medium)
            {
                return (HalfSizeAtmStrategy, HalfSizefATMName);
            }
            else if (tradeAction.Sizing == EMA2129SizingEnum.Small)
            {
                return (RiskyAtmStrategy, RiskyAtmStrategyName);
            }

            return (HalfSizeAtmStrategy, HalfSizefATMName);
        }

        protected override void UpdatePendingOrder()
        {
            if (TradingStatus != TradingStatus.PendingFill)
            {
                return;
            }

            // Cancel lệnh do đợi quá lâu
            var firstOrder = GetPendingOrder();

            if (firstOrder == null)
            {
                return;
            }

            var cancelOrderDueByTime = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
            if (cancelOrderDueByTime)
            {
                return;
            }

            var checkShouldTradeAgain = ShouldTrade();

            if (checkShouldTradeAgain.Action == GeneralTradeAction.NoTrade)
            {
                LocalPrint($"Check lại các điều kiện với [ShouldTrade], new answer: [{checkShouldTradeAgain.Action}] --> Cancel lệnh do không thỏa mãn các điều kiện trade");
                CancelAllPendingOrder();
                return;
            }
            else
            {
                var (atmStrategy, atmStrategyName) = GetAtmStrategyByPnL(checkShouldTradeAgain);

                var newPrice = GetSetPrice(checkShouldTradeAgain, atmStrategy);

                var stopLossPrice = GetStopLossPrice(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Half = GetTargetPrice_Half(checkShouldTradeAgain, newPrice, atmStrategy);

                var targetPrice_Full = GetTargetPrice_Full(checkShouldTradeAgain, newPrice, atmStrategy);

                // Số lượng contracts hiện tại

                // Nếu ngược trend hoặc backtest thì vào cancel lệnh cũ và vào lệnh mới
                if (State == State.Historical || (CurrentTradeAction.Action != checkShouldTradeAgain.Action))
                {
                    #region Cancel current order and enter new one
                    CancelAllPendingOrder();

                    EnterOrder(checkShouldTradeAgain);
                    #endregion
                }
                // Ngược lại thì update điểm vào lệnh
                else if (State == State.Realtime)
                {
                    #region Begin of move pending order
                    UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
                    #endregion
                }
            }
        }

        protected override double GetSetPrice(EMA2129OrderDetail tradeAction, AtmStrategy additionalInfo)
        {
            double ans = -1;
            /*
             */
            switch (tradeAction.Postition)
            {
                case EMA2129OrderPostition.EMA21:
                    ans = tradeAction.Action == GeneralTradeAction.Buy ? EMA21Indicator_1m.Value[0] + 5 : EMA21Indicator_1m.Value[0] - 5;
                    break;
                case EMA2129OrderPostition.EMA29:
                    ans = EMA29Indicator_1m.Value[0];
                    break;
                case EMA2129OrderPostition.MiddlePoint:
                    ans = (EMA29Indicator_1m.Value[0] + EMA10Indicator_5m.Value[0]) / 2.0;
                    break;
                case EMA2129OrderPostition.EMA10:
                    ans = EMA21Indicator_1m.Value[0];
                    break;
            }

            return StrategiesUtilities.RoundPrice(ans);
        }

        protected override void MoveStopOrder(Order stopOrder, double updatedPrice, double filledPrice, bool isBuying, bool isSelling)
        {
            double newPrice = -1;
            var allowMoving = false;
            var stopOrderPrice = stopOrder.StopPrice;            
            
            if (isBuying)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice > filledPrice && stopOrderPrice + PointToMoveLoss < updatedPrice)
                {
                    newPrice = updatedPrice - PointToMoveLoss;
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 60 && stopOrderPrice - filledPrice < 40)
                {
                    newPrice = filledPrice + 40; 
                    allowMoving = true;
                }
                else if (updatedPrice - filledPrice >= 30 && stopOrderPrice - filledPrice < 10)
                {
                    newPrice = filledPrice + 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice <= stopOrderPrice && stopOrderPrice < TargetPrice_Half && TargetPrice_Half + 7 < updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1 - filledPrice: {filledPrice:N2} <= stopOrderPrice: {stopOrderPrice:N2} <= TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    // Giá lên 37 điểm thì di chuyển stop loss lên 30 điểm
                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;                        
                    }
                    */
                    #endregion                    
                }
            }
            else if (isSelling)
            {
                // Dịch chuyển stop loss nếu giá quá xa stop loss, với điều kiện startMovingStoploss = true 
                if (StartMovingStoploss && stopOrderPrice < filledPrice && stopOrderPrice - PointToMoveLoss > updatedPrice)
                {
                    newPrice = updatedPrice + PointToMoveLoss;
                    allowMoving = true;
                }
                else if (filledPrice - updatedPrice>= 60 && filledPrice - stopOrderPrice < 40)
                {
                    newPrice = filledPrice - 40;
                    allowMoving = true;
                }
                else if (filledPrice - updatedPrice >= 30 && filledPrice - stopOrderPrice < 10)
                {
                    newPrice = filledPrice - 10;
                    allowMoving = true;
                }
                else
                {
                    #region Code cũ - Có thể sử dụng lại sau này
                    /*
                     * Old code cho trường hợp stop loss đã về ngang với giá vào lệnh (break even). 
                     * - Có 2x contracts, cắt x contract còn x contracts
                     * - Khi giá lên [Target_Half + 7] thì đưa stop loss lên Target_Half
                     */

                    /*
                    allowMoving = allowMoving || (filledPrice >= stopOrderPrice && stopOrderPrice > TargetPrice_Half && TargetPrice_Half - 7 > updatedPrice);

                    LocalPrint($"Điều kiện để chuyển stop lên target 1  - filledPrice: {filledPrice:N2} >= stopOrderPrice: {stopOrderPrice:N2} > TargetPrice_Half {TargetPrice_Half:N2} --> Allow move: {allowMoving}");

                    if (allowMoving)
                    {
                        newPrice = TargetPrice_Half;
                    }
                    */
                    #endregion
                }
            }

            if (allowMoving)
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");

                MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
            }
        }

        protected override void AddCustomIndicators()
        {
            
        }
    }

}
