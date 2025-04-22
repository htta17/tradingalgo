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
    public class Rooster : BarClosedATMBase<EMA2129OrderDetail>, IATMStrategy
    {
        public Rooster() : base("ROOSTER")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyRooster.txt");
            Configured_TimeFrameToTrade = TimeFrameToTrade.OneMinute;
        }

        #region Configurations 

        /// <summary>
        /// ATM name for live trade.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Risky ATM Strategy", Description = "Risky ATM Strategy", Order = 2,
            GroupName = StrategiesUtilities.Configuration_ATMStrategy_Group)]
        [TypeConverter(typeof(ATMStrategyConverter))]
        public string RiskyAtmStrategyName { get; set; }

        protected AtmStrategy RiskyAtmStrategy { get; set; }

        /// <summary>
        /// Chốt lời khi cây nến lớn hơn giá trị này (points), current default value: 60 points.
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Adjustment Point:",
            Description = "Số điểm cộng (hoặc trừ) so với đường EMA21 để vào lệnh MUA (hoặc BÁN).",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        public int AdjustmentPoint { get; set; }

        #endregion
        protected TimeFrameToTrade Configured_TimeFrameToTrade { get; set; }
        protected override bool IsBuying
        {
            get { return CurrentTradeAction.Action == GeneralTradeAction.Buy; }
        }

        protected override bool IsSelling
        {
            get { return CurrentTradeAction.Action == GeneralTradeAction.Sell; }
        }

        private DateTime executionTime = DateTime.MinValue;

        #region Indicators
        
        protected EMA EMA20Indicator_5m { get; set; }
        protected EMA EMA50Indicator_5m { get; set; }
        protected EMA EMA100Indicator_5m { get; set; }        
        #endregion
        private EMA2129Status EMA2129Status { get; set; }

        /// <summary>
        /// Giá trị này chỉ nên lưu 2 giá trị là [Above] và [Below] <br/>
        /// Khi giá trị [PreviousPosition] là [Above], và cây nến hiện tại là [Below] thì mới reset việc EnteredOrder <br/>
        /// hoặc khi giá trị [PreviousPosition] là [Below], và cây nến hiện tại là [Above] thì mới reset việc EnteredOrder <br/>
        /// Trong trường hợp giá trị [PreviousPosition] == vị trí cây nến hiện tại thì không cần reset order.
        /// </summary>
        private EMA2129Position PreviousPosition { get; set; } = EMA2129Position.Unknown;        

        protected override void AddCustomDataSeries()
        {
            AddDataSeries(BarsPeriodType.Minute, 5);            
        }

        protected int GetBarIndex(int barsPeriod)
        {
            return 1;
        }

        protected override void OnBarUpdate()
        {
            // Cập nhật lại status 
            tradingStatus = CheckCurrentStatusBasedOnOrders();            

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

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) //1 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                // Cập nhật lại giá trị cây nến
                var index = GetBarIndex(BarsPeriod.Value);

                var high = High[0];
                var low = Low[0];
                var open = Open[0];
                var close = Close[0];

                var ema20Val = EMA20Indicator_5m.Value[0];
                var ema50Val = EMA50Indicator_5m.Value[0];
                var ema100Val = EMA100Indicator_5m.Value[0];

                /*
                // Remember these 3 variable are the same right now
                var ema21Val = EMA21Indicator_1m.Value[0];
                var ema29Val = EMA21Indicator_1m.Value[0];
                var ema10Val = EMA21Indicator_1m.Value[0];
                */

                var minValue = ema20Val; //StrategiesUtilities.MinOfArray(ema20Val, ema20Val);
                var maxValue = ema20Val; // StrategiesUtilities.MaxOfArray(ema20Val, ema20Val);

                // Trạng thái
                if (high > maxValue && low < minValue) // Cross EMA lines
                {
                    LocalPrint($"New status: CROSSING");
                    EMA2129Status.SetPosition(EMA2129Position.Crossing, CurrentBar);
                }
                else if (high < minValue && minValue - high > 5 && EMA2129Status.Position != EMA2129Position.Below)
                {
                    LocalPrint($"New status: BELOW - Current status: {PreviousPosition}, Reset order: {PreviousPosition != EMA2129Position.Below}");
                    EMA2129Status.SetPosition(EMA2129Position.Below, CurrentBar, PreviousPosition != EMA2129Position.Below);

                    PreviousPosition = EMA2129Position.Below;
                }
                else if (low > maxValue && low - maxValue > 5 && EMA2129Status.Position != EMA2129Position.Above)
                {
                    LocalPrint($"New status: ABOVE - Current status: {PreviousPosition}, Reset order: {PreviousPosition != EMA2129Position.Above}");
                    EMA2129Status.SetPosition(EMA2129Position.Above, CurrentBar, PreviousPosition != EMA2129Position.Above);

                    PreviousPosition = EMA2129Position.Above;
                }

                BasicActionForTrading(TimeFrameToTrade.OneMinute);
            }            
        }

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
        [Display(Name = "Hiển thị các chỉ báo:",
            Description = "Hiển thị chỉ báo trên chart",
            Order = 1, GroupName = StrategiesUtilities.Configuration_DisplayIndicators)]
        public bool DisplayIndicators { get; set; }

        
        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster";
            Description = "[Rooster] trade ở khung 5 phút, dùng EMA20/50/100.";

            FullSizeATMName = "Rooster_Default_4cts";
            HalfSizefATMName = "Rooster_Default_2cts";
            RiskyAtmStrategyName = "Rooster_Risky";

            DailyTargetProfit = 500;
            MaximumDailyLoss = 350;

            StartDayTradeTime = new TimeSpan(8, 40, 0); // 8:40:00 am 
            EndDayTradeTime = new TimeSpan(15, 50, 0); // 3:50:00 pm
            EMA2129Status = new EMA2129Status();          

            DisplayIndicators = true;
            AdjustmentPoint = 7;
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA20Indicator_5m = EMA(BarsArray[1], 20);
            EMA20Indicator_5m.Plots[0].Brush = Brushes.Red;

            EMA50Indicator_5m = EMA(BarsArray[1], 50);
            EMA50Indicator_5m.Plots[0].Brush = Brushes.DarkOrange;

            EMA100Indicator_5m = EMA(BarsArray[1], 100);
            EMA100Indicator_5m.Plots[0].Brush = Brushes.DarkCyan;            

            if (DisplayIndicators)
            {
                AddChartIndicator(EMA100Indicator_5m);
                AddChartIndicator(EMA50Indicator_5m);
                AddChartIndicator(EMA20Indicator_5m);
            }
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

                LocalPrint($"Check trading condition, result: {shouldTrade.Action}, EnteredOrder: {EMA2129Status.EnteredOrder}");

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

            var ema50Val = EMA50Indicator_5m.Value[0];
            var ema100_5mVal = EMA100Indicator_5m.Value[0];
            var ema20Val = EMA20Indicator_5m.Value[0];

            if (EMA2129Status.Position == EMA2129Position.Above && ema20Val > ema50Val && ema50Val > ema100_5mVal)
            {
                answer.Action = GeneralTradeAction.Buy;
            }
            else if (EMA2129Status.Position == EMA2129Position.Below && ema20Val < ema50Val && ema50Val < ema100_5mVal)
            {
                answer.Action = GeneralTradeAction.Sell;
            }
            answer.Postition = EMA2129OrderPostition.EMA21;
            answer.Sizing = EMA2129SizingEnum.Big;

            /*
            var adxVal = ADXandDI.Value[0];
            // Nếu giá trị ADX đang ở dưới [ADXValueToCancelOrder]           
            if (adxVal < ADXValueToENTEROrder)
            {
                LocalPrint($"{adxVal:N2} = [adxVal] < [ADXValueToENTEROrder]  = {ADXValueToENTEROrder}--> No Trade.");
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

                FilledTime = Time[0];
            }
            */

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
                CancelAllPendingOrder();
                return;
            }

            var checkShouldTradeAgain = ShouldTrade();

            if (checkShouldTradeAgain.Action == GeneralTradeAction.NoTrade)
            {
                LocalPrint($"Check lại các điều kiện với [ShouldTrade], new answer: [{checkShouldTradeAgain.Action}] --> Cancel lệnh do không thỏa mãn các điều kiện trade");

                CancelAllPendingOrder();

                // Cho phép trade trở lại
                EMA2129Status.ResetEnteredOrder();
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
            var ans = EMA20Indicator_5m.Value[0];

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
                else if (filledPrice - updatedPrice >= 60 && filledPrice - stopOrderPrice < 40)
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
