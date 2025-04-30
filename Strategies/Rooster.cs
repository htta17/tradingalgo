#define USE_ADX_TO_TRADE
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
using NinjaTrader.NinjaScript.SuperDomColumns;
using System.Xml.Linq;
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
        /// Số điểm cộng (hoặc trừ) so với đường EMA21 để vào lệnh MUA (hoặc BÁN).
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Adjustment Point:",
            Description = "Số điểm cộng (hoặc trừ) so với đường EMA21 để vào lệnh MUA (hoặc BÁN).",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        public int AdjustmentPoint { get; set; }

        /// <summary>
        /// Số lần vào lệnh tối đa cho mỗi xu hướng
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lần tối đa vào lệnh: ",
            Description = "Số lần tối đa vào lệnh cho mỗi xu hướng.",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        protected int MaximumOrderForEachTrend { get; set; }

        /// <summary>
        /// Số lần vào lệnh tối đa cho mỗi xu hướng
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lần tối đa vào lệnh: ",
            Description = "Số lần tối đa vào lệnh cho mỗi xu hướng.",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        protected int MinimumAngleToTrade { get; set; }

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

        #region Indicators
        protected EMA EMA29Indicator_1m { get; set; }
        protected EMA EMA21Indicator_1m { get; set; }
        protected EMA EMA46Indicator_5m { get; set; }
        protected EMA EMA20Indicator_5m { get; set; }
        protected EMA EMA10Indicator_5m { get; set; }

        protected Falcon Falcon_1m { get; set; }

        #endregion
        private EMA2129Status EMA2129Status { get; set; }


        protected double CurrentHigh { get; set; }


        protected double CurrentLow { get; set; }


        /// <summary>
        /// Giá trị này chỉ nên lưu 2 giá trị là [Above] và [Below] <br/>
        /// Khi giá trị [PreviousPosition] là [Above], và cây nến hiện tại là [Below] thì mới reset việc EnteredOrder <br/>
        /// hoặc khi giá trị [PreviousPosition] là [Below], và cây nến hiện tại là [Above] thì mới reset việc EnteredOrder <br/>
        /// Trong trường hợp giá trị [PreviousPosition] == vị trí cây nến hiện tại thì không cần reset order.
        /// </summary>
        private EMA2129Position PreviousPosition { get; set; } = EMA2129Position.Unknown;

        /// <summary>
        /// Display Indicators
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Hiển thị các chỉ báo:",
            Description = "Hiển thị chỉ báo trên chart",
            Order = 1, GroupName = StrategiesUtilities.Configuration_DisplayIndicators)]
        public bool DisplayIndicators { get; set; }

        /// <summary>
        /// Display Indicators
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Hiển thị EMA20 khung 5 phút (Tham khảo):",
            Description = "Hiển thị chỉ báo trên chart",
            Order = 2, GroupName = StrategiesUtilities.Configuration_DisplayIndicators)]
        public bool DisplayEMA20_5m { get; set; }

        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Rooster";
            Description = "[Rooster] là giải thuật trade on First touch";

            FullSizeATMName = "Kitty_Default_4cts";
            HalfSizefATMName = "Kitty_Default_2cts";
            RiskyAtmStrategyName = "Kitty_Risky";

            DailyTargetProfit = 500;
            MaximumDailyLoss = 350;

            StartDayTradeTime = new TimeSpan(8, 40, 0); // 8:40:00 am 
            EndDayTradeTime = new TimeSpan(14, 30, 0); // 2:30:00 pm
            EMA2129Status = new EMA2129Status();

            AddPlot(Brushes.Green, "EMA9_5m");
            AddPlot(Brushes.Red, "EMA46_5m");
            AddPlot(Brushes.DeepPink, "EMA20_5m");

            DisplayIndicators = true;
            DisplayEMA20_5m = false;
            AdjustmentPoint = 10;

            // Tạm thời cho 2 lệnh trade 
            MaximumOrderForEachTrend = 2;
            MinimumAngleToTrade = 35;
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA29Indicator_1m = EMA(BarsArray[2], 29);
            EMA29Indicator_1m.Plots[0].Brush = Brushes.Orange;

            EMA21Indicator_1m = EMA(BarsArray[2], 21);
            EMA21Indicator_1m.Plots[0].Brush = Brushes.Blue;

            EMA46Indicator_5m = EMA(BarsArray[1], 46);
            EMA20Indicator_5m = EMA(BarsArray[1], 20);
            EMA10Indicator_5m = EMA(BarsArray[1], 10);
            // WaddahAttarExplosion_5m = WaddahAttarExplosion(BarsArray[1]);

            Falcon_1m = Falcon(BarsArray[2], 20, MinimumAngleToTrade);

            if (DisplayIndicators)
            {
                AddChartIndicator(EMA29Indicator_1m);
                AddChartIndicator(EMA21Indicator_1m);

                AddChartIndicator(Falcon_1m);
            }
        }

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
                    Values[0][0] = EMA10Indicator_5m.Value[0];
                    Values[1][0] = EMA46Indicator_5m.Value[0];
                    Values[2][0] = EMA20Indicator_5m.Value[0];
                }
                catch (Exception ex)
                {
                    LocalPrint("[OnBarUpdate]: ERROR:" + ex.Message);
                }
            }

            if (BarsInProgress == 0)
            {
                // Current View --> Do nothing
                return;
            }

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 1) //1 minute
            {
                StrategiesUtilities.CalculatePnL(this, Account, Print);

                var time = ToTime(Time[0]);

                if (time == 16_00_00 && State == State.Historical)
                {
                    LocalPrint($"Reset daily PnL for back test");
                    BackTestDailyPnL = 0;
                    Draw.Text(this, "NewDay_" + CurrentBar, $"{Time[0]:MM/dd}", 0, High[0] + 120 * TickSize, Brushes.Blue);
                    Draw.VerticalLine(this, $"Day {Time[0]:yyyy-MM-dd}", Time[0], Brushes.Red, DashStyleHelper.Dot, 2);

                    EMA2129Status.ResetEnteredOrder();

                    EMA2129Status.ResetCounters();
                }

                try
                {
                    BasicActionForTrading(TimeFrameToTrade.OneMinute);

                    var high = High[0];
                    var low = Low[0];
                    var open = Open[0];
                    var close = Close[0];

                    var ema21Val = EMA21Indicator_1m.Value[0];
                    var ema29Val = EMA29Indicator_1m.Value[0];
                    var ema10_5m_Val = EMA10Indicator_5m.Value[0];

                    var minValue = StrategiesUtilities.MinOfArray(ema21Val, ema29Val, ema10_5m_Val);
                    var maxValue = StrategiesUtilities.MaxOfArray(ema21Val, ema29Val, ema10_5m_Val);

                    if (high < minValue && minValue - high > 5 && EMA2129Status.Position != EMA2129Position.Below)
                    {
                        var resetOrder = PreviousPosition != EMA2129Position.Below;

                        LocalPrint($"New status: BELOW - Current status: {PreviousPosition}, Reset order: {resetOrder}");

                        EMA2129Status.SetPosition(EMA2129Position.Below, CurrentBar, resetOrder);

                        PreviousPosition = EMA2129Position.Below;

                        CurrentLow = Low[0];
                    }
                    else if (low > maxValue && low - maxValue > 5 && EMA2129Status.Position != EMA2129Position.Above)
                    {
                        var resetOrder = PreviousPosition != EMA2129Position.Above;

                        LocalPrint($"New status: ABOVE - Current status: {PreviousPosition}, Reset order: {resetOrder}");

                        EMA2129Status.SetPosition(EMA2129Position.Above, CurrentBar, resetOrder);

                        PreviousPosition = EMA2129Position.Above;

                        CurrentHigh = High[0];
                    }
                    else
                    {
                        if (EMA2129Status.Position == EMA2129Position.Above)
                        {
                            CurrentHigh = Math.Max(CurrentHigh, High[0]);
                        }
                        else if (EMA2129Status.Position == EMA2129Position.Below)
                        {
                            CurrentLow = Math.Max(CurrentLow, Low[0]);
                        }

                        if (high >= ema21Val && low <= ema21Val)
                        {
                            LocalPrint($"Touch EMA21");
                            EMA2129Status.Touch(EMA2129OrderPostition.EMA21);
                        }

                        if (high >= ema29Val && low <= ema29Val)
                        {
                            LocalPrint($"Touch EMA29");
                            EMA2129Status.Touch(EMA2129OrderPostition.EMA29);
                        }

                        if (high >= ema10_5m_Val && low <= ema10_5m_Val)
                        {
                            LocalPrint($"Touch EMA10 (khung 5 phút)");
                            EMA2129Status.Touch(EMA2129OrderPostition.EMA10_5m);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LocalPrint("[OnBarUpdate]: ERROR:" + ex.Message);
                }
            }
            else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                // Do nothing for now
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
                var passTradeCondition = CheckingTradeCondition();
                if (!passTradeCondition)
                {
                    LocalPrint($"[BasicActionForTrading] Not Pass Condition to trade");
                    return;
                }

                var shouldTrade = ShouldTrade();

                LocalPrint($"Check trading condition, result: {shouldTrade.Action}, EnteredOrder21: {EMA2129Status.EnteredOrder21}, EnteredOrder29: {EMA2129Status.EnteredOrder29}");

                if (shouldTrade.Action != GeneralTradeAction.NoTrade) // Nếu chưa enter order thì mới enter order
                {
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

        private EMA2129OrderPostition GetPostitionBasedOnAngleValue(double absolutedAngle)
        {
            if (absolutedAngle < MinimumAngleToTrade)
            {
                return EMA2129OrderPostition.NoTrade;
            }
            else
            {
                return EMA2129OrderPostition.EMA29;
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

            var ema21Val = EMA21Indicator_1m.Value[0];
            var ema29Val = EMA29Indicator_1m.Value[0];
            var ema10_5mVal = EMA10Indicator_5m.Value[0];
            var ema46_5mVal = EMA46Indicator_5m.Value[0];

            var absolutedAngle = Math.Abs(Falcon_1m.Value[0]);
            var postionAngle = GetPostitionBasedOnAngleValue(absolutedAngle);

            var high = High[0];
            var low = Low[0];
            var allowTrade = false;
            var previousTouch = EMA2129Status.CountTouch_EMA10_5m + EMA2129Status.CountTouch_EMA21 + EMA2129Status.CountTouch_EMA29;

            LocalPrint($"postionAngle: {postionAngle}, {EMA2129Status.CountTouch_EMA10_5m} {EMA2129Status.CountTouch_EMA21} {EMA2129Status.CountTouch_EMA29}");

            if (postionAngle != EMA2129OrderPostition.NoTrade && previousTouch == 0)
            {
                if (high >= ema21Val && low <= ema21Val)
                {
                    allowTrade = true;
                    LocalPrint($"allowTrade = true, {EMA2129Status.CountTouch_EMA10_5m} {EMA2129Status.CountTouch_EMA21} {EMA2129Status.CountTouch_EMA29}");
                }
                else if (high >= ema29Val && low <= ema29Val)
                {
                    allowTrade = true;
                    LocalPrint($"allowTrade = true, {EMA2129Status.CountTouch_EMA10_5m} {EMA2129Status.CountTouch_EMA21} {EMA2129Status.CountTouch_EMA29}");
                }
                else if (high >= ema10_5mVal && low <= ema10_5mVal)
                {
                    allowTrade = true;
                    LocalPrint($"allowTrade = true, {EMA2129Status.CountTouch_EMA10_5m} {EMA2129Status.CountTouch_EMA21} {EMA2129Status.CountTouch_EMA29}");
                }
            }

            if (allowTrade)
            {
                if (EMA2129Status.Position == EMA2129Position.Above)
                {
                    answer.Postition = GetPostitionBasedOnAngleValue(absolutedAngle);
                    answer.Sizing = EMA2129SizingEnum.Big;
                    answer.Action = GeneralTradeAction.Buy;
                }
                else if (EMA2129Status.Position == EMA2129Position.Below )
                {
                    answer.Postition = GetPostitionBasedOnAngleValue(absolutedAngle);
                    answer.Sizing = EMA2129SizingEnum.Big;
                    answer.Action = GeneralTradeAction.Sell;
                }
            }
            
            return answer;
        }

        protected override (AtmStrategy, string) GetAtmStrategyByPnL(EMA2129OrderDetail tradeAction)
        {
            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf =
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));

            if (reachHalf)
            {
                return (HalfSizeAtmStrategy, HalfSizefATMName);
            }

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

        protected override bool ShouldCancelPendingOrdersByTimeCondition(DateTime filledOrderTime)
        {
            // Cancel lệnh hết giờ trade
            if (ToTime(Time[0]) >= 15_00_00 && ToTime(filledOrderTime) < 15_00_00)
            {
                LocalPrint($"Cancel lệnh hết giờ trade");
                return true;
            }

            return false;
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

                // Cho phép trade trở lại
                EMA2129Status.ResetEnteredOrder();

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
                if (CurrentTradeAction.Action != checkShouldTradeAgain.Action)
                {

                    #region Cancel current order and enter new one
                    CancelAllPendingOrder();

                    EnterOrder(checkShouldTradeAgain);
                    #endregion
                }
                // Ngược lại thì update điểm vào lệnh
                else
                {
                    EMA2129Status.SetEnteredOrder(checkShouldTradeAgain.Postition);

                    UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);
                }
            }
        }

        protected override void EnterOrderPureUsingPrice(double priceToSet, double targetInTicks, double stoplossInTicks, string signal, int quantity, bool isBuying, bool isSelling)
        {
            var text = isBuying ? "LONG" : "SHORT";

            if (isBuying)
            {
                EnterLong(0, quantity, signal);
            }
            else
            {
                EnterShort(0, quantity, signal);
            }

            SetStopLoss(signal, CalculationMode.Ticks, stoplossInTicks, false);

            SetProfitTarget(signal, CalculationMode.Ticks, targetInTicks);

            LocalPrint($"Enter {text} for {quantity} contracts with signal [{signal}] at {priceToSet:N2}, stop loss ticks: {stoplossInTicks:N2}, target ticks: {targetInTicks:N2}");
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
                OrderType.Market,
                0, // Enter market
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
                    else if (atmCallbackErrorCode != ErrorCode.NoError)
                    {
                        LocalPrint($"[AtmStrategyCreate] ERROR : " + atmCallbackErrorCode);
                    }
                });
        }

        protected override double GetSetPrice(EMA2129OrderDetail tradeAction, AtmStrategy additionalInfo)
        {
            double ans = -1;
            LocalPrint($"EMA21: {EMA21Indicator_1m.Value[0]:N2}, AdjustmentPoint: {AdjustmentPoint}, tradeAction: {tradeAction.Action}");
            /*
             */
            switch (tradeAction.Postition)
            {
                case EMA2129OrderPostition.AdjustedEMA21:
                    ans = tradeAction.Action == GeneralTradeAction.Buy
                        ? EMA21Indicator_1m.Value[0] + AdjustmentPoint
                        : EMA21Indicator_1m.Value[0] - AdjustmentPoint;
                    break;

                case EMA2129OrderPostition.EMA21:
                    ans = EMA21Indicator_1m.Value[0];
                    break;
                // Note: Vẫn dùng EMA21 +/- AdjustmentPoint để vào lệnh
                case EMA2129OrderPostition.EMA29:
                    ans = EMA29Indicator_1m.Value[0];
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
                //else if (updatedPrice - filledPrice >= 60 && stopOrderPrice - filledPrice < 40)
                //{
                //    newPrice = filledPrice + 40; 
                //    allowMoving = true;
                //}
                else if (updatedPrice - filledPrice >= 30 && stopOrderPrice - filledPrice < 25)
                {
                    newPrice = filledPrice + 25;
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
                //else if (filledPrice - updatedPrice>= 60 && filledPrice - stopOrderPrice < 40)
                //{
                //    newPrice = filledPrice - 40;
                //    allowMoving = true;
                //}
                else if (filledPrice - updatedPrice >= 30 && filledPrice - stopOrderPrice < 25)
                {
                    newPrice = filledPrice - 25;
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

        protected override void TransitionOrdersToLive()
        {
            base.TransitionOrdersToLive();

            // Reset
            EMA2129Status.ResetEnteredOrder();
        }

        protected override void AddCustomIndicators()
        {

        }
    }

}
