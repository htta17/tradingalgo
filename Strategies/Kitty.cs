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
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Kitty : BarClosedATMBase<EMA2129OrderDetail>, IATMStrategy
    {
        public Kitty() : base("KITTY")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyKitty.txt");
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
        /// Giá trị để trade
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Góc cho phép trade", Description = "Nếu giá trị Falcon > [Giá trị] thì mới trade", Order = 1,
           GroupName = StrategiesUtilities.Configuration_Entry)]        
        public int MininumAngleToTrade { get; set; }

        /// <summary>
        /// Số lần vào lệnh tối đa cho mỗi xu hướng
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lần tối đa vào lệnh: ",
            Description = "Số lần tối đa vào lệnh cho mỗi xu hướng.",
            Order = 2, GroupName = StrategiesUtilities.Configuration_Entry)]
        protected int MaximumOrderForEachTrend { get; set; }

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
        protected EMA EMA89Indicator_1m { get; set; }
        protected EMA EMA50Indicator_5m { get; set; }
        protected EMA EMA20Indicator_5m { get; set; }
        protected EMA EMA10Indicator_5m { get; set; }       

        protected Falcon Falcon_1m { get; set; }       

        #endregion
        private EMA2129Status EMA2129Status { get; set; }
        
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


        /// <summary>
        /// Remember current high
        /// </summary>
        protected double CurrentHigh_BULL_Trend { get; set; }

        protected double LastHigh_BULL_Trend { get; set; }

        /// <summary>
        /// Remember current low
        /// </summary>
        protected double CurrentLow_BEAR_Trend { get; set; }

        protected double LastLow_BEAR_Trend { get; set; }

        /// <summary>
        /// Đếm số cây nến liên tiếp khác màu với trend hiện tai
        /// </summary>
        protected int CountReverseCandles { get; set; }

        /// <summary>
        /// Đánh dấu cây nến có thân nhỏ, râu nến dài (trên hoặc dưới). <br/>        
        /// </summary>
        protected bool HammerCandle { get; set; }

        protected override void OnStateChange_Configure()
        {
            base.OnStateChange_Configure();

            RiskyAtmStrategy = StrategiesUtilities.ReadStrategyData(RiskyAtmStrategyName, Print).AtmStrategy;
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Kitty";
            Description = "[Kitty] là giải thuật được viết riêng cho my love, Phượng Phan.";

            FullSizeATMName = "Kitty_Big";
            HalfSizefATMName = "Kitty_Medium"; 
            RiskyAtmStrategyName = "Kitty_Small"; 

            DailyTargetProfit = 500;
            MaximumDailyLoss = 350;

            StartDayTradeTime = new TimeSpan(8, 40, 0); // 8:40:00 am 
            EndDayTradeTime = new TimeSpan(14, 30, 0); // 2:30:00 pm
            EMA2129Status = new EMA2129Status();

            AddPlot(Brushes.Green, "EMA9_5m");
            AddPlot(Brushes.Red, "EMA50_5m");

            //AddPlot(Brushes.Pink, "EMA20_5m");

            DisplayIndicators = true;
            DisplayEMA20_5m = false;
            AdjustmentPoint = 10;

            // Tạm thời cho 2 lệnh trade 
            MaximumOrderForEachTrend = 2;

            CurrentHigh_BULL_Trend = int.MinValue;
            LastHigh_BULL_Trend = int.MinValue;

            CurrentLow_BEAR_Trend = int.MaxValue;
            LastLow_BEAR_Trend = int.MaxValue;

            CountReverseCandles = 0;
            HammerCandle = false;

            MininumAngleToTrade = 30;
        }

        protected override void OnStateChange_DataLoaded()
        {
            EMA29Indicator_1m = EMA(BarsArray[2], 29);
            EMA29Indicator_1m.Plots[0].Brush = Brushes.Orange;

            EMA21Indicator_1m = EMA(BarsArray[2], 21);
            EMA21Indicator_1m.Plots[0].Brush = Brushes.Blue;

            EMA89Indicator_1m = EMA(BarsArray[2], 89);
            EMA89Indicator_1m.Plots[0].Brush = Brushes.Gray;
            EMA50Indicator_5m = EMA(BarsArray[1], 50);
            EMA20Indicator_5m = EMA(BarsArray[1], 20);
            EMA10Indicator_5m = EMA(BarsArray[1], 10);

            Falcon_1m = Falcon(BarsArray[2], 20, MininumAngleToTrade);

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

        private void SetAndDrawTopOrBottom(EMA2129Position position)
        {
            if (position == EMA2129Position.Above)
            {
                // Set lại Current High
                CurrentHigh_BULL_Trend = High[0];

                Draw.HorizontalLine(this, "CurrentKey", CurrentHigh_BULL_Trend, Brushes.Orange, DashStyleHelper.Dash, 1);

                // Cập nhật lại xem đây có phải là cây nến rút râu (Inverted Hammer không)
                HammerCandle = CandleUtilities.IsGreenCandle(Close[0], Open[0]) && CandleUtilities.TopToBodyPercentage(Close[0], Open[0], High[0], Low[0]) > 70;
            }   
            else if (position == EMA2129Position.Below)
            {
                // Set lại Current High
                CurrentLow_BEAR_Trend = Low[0];

                Draw.HorizontalLine(this, "CurrentKey", CurrentLow_BEAR_Trend, Brushes.Orange, DashStyleHelper.Dash, 1);

                // Cập nhật lại xem đây có phải là cây nến rút râu (Inverted Hammer không)
                HammerCandle = CandleUtilities.IsRedCandle(Close[0], Open[0]) && CandleUtilities.BottomToBodyPercentage(Close[0], Open[0], High[0], Low[0]) > 70;
            }            
            CountReverseCandles = 0;
        }

        private bool IsTouched(double high, double low, EMA2129OrderPostition postition)
        {
            // Touches cac đường
            if (postition == EMA2129OrderPostition.EMA21 && high >= EMA21Indicator_1m.Value[0] && low <= EMA21Indicator_1m.Value[0])
            {
                LocalPrint($"Touch EMA21 - Current Count: {EMA2129Status.CountTouch_EMA21}");
                //EMA2129Status.Touch(EMA2129OrderPostition.EMA21);
                return true;
            }
            else if (postition == EMA2129OrderPostition.EMA29 && high >= EMA29Indicator_1m.Value[0] && low <= EMA29Indicator_1m.Value[0])
            {
                LocalPrint($"Touch EMA29 - Current Count: {EMA2129Status.CountTouch_EMA29}");
                return true;
                //EMA2129Status.Touch(EMA2129OrderPostition.EMA29);
            }
            else if (postition == EMA2129OrderPostition.EMA10_5m && high >= EMA10Indicator_5m.Value[0] && low <= EMA10Indicator_5m.Value[0])
            {
                LocalPrint($"Touch EMA10 (khung 5 phút) - Current Count: {EMA2129Status.CountTouch_EMA29}");
                // EMA2129Status.Touch(EMA2129OrderPostition.EMA10_5m);
                return true;
            }
            return false; 
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
                    Values[1][0] = EMA50Indicator_5m.Value[0];

                    //Values[2][0] = EMA20Indicator_5m.Value[0];
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
                    var high = High[0];
                    var low = Low[0];
                    var open = Open[0];
                    var close = Close[0];

                    var ema21Val = EMA21Indicator_1m.Value[0];
                    var ema29Val = EMA29Indicator_1m.Value[0];
                    var ema10_5m_Val = EMA10Indicator_5m.Value[0];

                    var minValue = StrategiesUtilities.MinOfArray(ema21Val, ema29Val, ema10_5m_Val);
                    var maxValue = StrategiesUtilities.MaxOfArray(ema21Val, ema29Val, ema10_5m_Val);                    

                    /*
                     * SUPER IMPORTANT starts here
                     */
                    BasicActionForTrading(TimeFrameToTrade.OneMinute);                    

                    /*
                     * SUPER IMPORTANT
                     */

                    // Trạng thái hiện tại của nến: Phía DƯỚI các đường EMA
                    if (minValue > high && minValue - high >= 2)
                    {
                        //LocalPrint("[BELOW]");
                        // Nến đang ở DƯỚI cả 3 đường EMA
                        // Nếu trạng thái hiện tại không phải là BELOW thì cần reset
                        if (EMA2129Status.Position != EMA2129Position.Below)
                        {
                            var resetOrder = PreviousPosition != EMA2129Position.Below;

                            LocalPrint($"New status: BELOW - Current status: {PreviousPosition}, Reset order: {resetOrder}");

                            EMA2129Status.SetPosition(EMA2129Position.Below, CurrentBar, resetOrder);

                            PreviousPosition = EMA2129Position.Below;

                            SetAndDrawTopOrBottom(EMA2129Position.Below);
                        }    
                        else // Nếu không cần phải Reset
                        {
                            
                            if (CurrentLow_BEAR_Trend > Low[0])
                            {
                                // Cập nhật lại current high low
                                SetAndDrawTopOrBottom(EMA2129Position.Below);
                            }
                            
                            if (CandleUtilities.IsRedCandle(Close[0], Open[0]))
                            {
                                CountReverseCandles = 0;
                            }
                            else if (CandleUtilities.IsGreenCandle(Close[0], Open[0]))
                            {
                                CountReverseCandles++;
                                // Draw Number 
                                Draw.Text(this, $"Reverse_{CurrentBar}", $"{CountReverseCandles}", 0, Low[0] - 5, Brushes.Green);
                            }
                        }   
                    }

                    // Trạng thái hiện tại của nến: Phía TRÊN các đường EMA
                    else if (low > maxValue && low - maxValue >= 2)
                    {
                        //LocalPrint("[ABOVE]");
                        if (EMA2129Status.Position != EMA2129Position.Above)
                        {
                            var resetOrder = PreviousPosition != EMA2129Position.Above;

                            LocalPrint($"New status: ABOVE - Current status: {PreviousPosition}, Reset order: {resetOrder}");

                            EMA2129Status.SetPosition(EMA2129Position.Above, CurrentBar, resetOrder);

                            PreviousPosition = EMA2129Position.Above;

                            SetAndDrawTopOrBottom(EMA2129Position.Above);
                        }
                        else 
                        {
                            if (CurrentHigh_BULL_Trend < High[0])
                            {
                                // Cập nhật lại current high
                                SetAndDrawTopOrBottom(EMA2129Position.Above);
                            }
                            
                            if (CandleUtilities.IsGreenCandle(Close[0], Open[0]))
                            {
                                CountReverseCandles = 0;
                            }
                            else if (CandleUtilities.IsRedCandle(Close[0], Open[0]))
                            {
                                CountReverseCandles++;
                                // Draw Number 
                                Draw.Text(this, $"Reverse_{CurrentBar}", $"{CountReverseCandles}", 0, High[0] + 5, Brushes.Red);
                            }                            
                        } 
                    }
                    else 
                    {
                        LocalPrint($"[Sizeway] - {Falcon_1m.Value[0]:N2}, {MininumAngleToTrade}");
                        if (Math.Abs(Falcon_1m.Value[0]) >= MininumAngleToTrade)
                        {
                            if (IsTouched(high, low, EMA2129OrderPostition.EMA21))
                            {
                                EMA2129Status.Touch(EMA2129OrderPostition.EMA21);

                                LocalPrint($"New count EMA21: {EMA2129Status.CountTouch_EMA21}");
                            }

                            if (IsTouched(high, low, EMA2129OrderPostition.EMA29))
                            {
                                EMA2129Status.Touch(EMA2129OrderPostition.EMA29);

                                LocalPrint($"New count EMA29: {EMA2129Status.CountTouch_EMA29}");
                            }

                            if (IsTouched(high, low, EMA2129OrderPostition.EMA10_5m))
                            {
                                EMA2129Status.Touch(EMA2129OrderPostition.EMA10_5m);

                                LocalPrint($"New count EMA10_5m: {EMA2129Status.CountTouch_EMA10_5m}");
                            }
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

                //LocalPrint($"Check trading condition, result: {shouldTrade.Action}, EnteredOrder21: {EMA2129Status.EnteredOrder21}, EnteredOrder29: {EMA2129Status.EnteredOrder29}");
                
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
                // Tìm các close lệnh nếu thấy có các dấu hiệu
                // - Góc suy giảm ( < AllowToTradeAngle) 
                // - Giá hiện tại được 5 pts
                // - Đã chạm đường XANH                 
                var angle = Falcon_1m.Value[0];
                var noTradeAngle = angle < MininumAngleToTrade;
                var alreadyTouchEMA_10_5m = EMA2129Status.CountTouch_EMA10_5m > 0;

                var buySell = "";
                var newPrice = -1.0; 

                if (CurrentTradeAction.Action == GeneralTradeAction.Buy && Close[0] > FilledPrice + 5 && noTradeAngle && alreadyTouchEMA_10_5m && StopLossPrice < FilledPrice)
                {
                    LocalPrint($@"Dịch Stop loss lên break even do có các điều kiện sau: 
    - Góc suy giảm: {angle:N2} < {MininumAngleToTrade}
    - Giá hiện tại lời được 5pts: {Close[0]:N2}, giá vào lệnh: {FilledPrice:N2}
    - Đã chạm EMA10 khung 5 phút. 
                    ");
                    buySell = "BUY";
                    newPrice = FilledPrice + 5;                    
                }
                else if (CurrentTradeAction.Action == GeneralTradeAction.Sell && Close[0] < FilledPrice - 5 && noTradeAngle && alreadyTouchEMA_10_5m && StopLossPrice > FilledPrice)
                {
                    LocalPrint($@"Dịch Stop loss lên break even do có các điều kiện sau: 
    - Góc suy giảm: {angle:N2} < {MininumAngleToTrade}
    - Giá hiện tại lời được 5pts: {Close[0]:N2}, giá vào lệnh: {FilledPrice:N2}
    - Đã chạm EMA10 khung 5 phút. 
                    ");                    
                    buySell = "SELL";
                    newPrice = FilledPrice - 5;
                }

                if (!string.IsNullOrEmpty(buySell))
                {
                    if (State == State.Realtime)
                    {
                        var stopOrders = Account.Orders.Where(c => (c.OrderType == OrderType.StopLimit || c.OrderType == OrderType.StopMarket) && c.OrderState == OrderState.Accepted).ToList();

                        for (int i = 0; i < stopOrders.Count; i++)
                        {
                            var stopOrder = stopOrders[i];                            

                            MoveTargetOrStopOrder(newPrice, stopOrder, false, buySell, stopOrder.FromEntrySignal);
                        }
                    }
                    else
                    {
                        CloseExistingOrders();
                    }

                    StopLossPrice = newPrice;
                }
               

            }
        }
        
        private EMA2129OrderPostition GetPostitionBasedOnAngleValue(double absolutedAngle)
        {
            if (absolutedAngle < MininumAngleToTrade)
            {
                return EMA2129OrderPostition.NoTrade; 
            }
            else if (absolutedAngle >= MininumAngleToTrade && absolutedAngle < Falcon_1m.RANGE_45_NO_TRD)
            {
                return EMA2129OrderPostition.EMA29;
            }
            else if (absolutedAngle >= Falcon_1m.RANGE_45_NO_TRD && absolutedAngle < Falcon_1m.RANGE_70_YES_TRD)
            {
                // Đã touch EMA21 rồi
                if (EMA2129Status.EnteredOrder21)
                {
                    return EMA2129OrderPostition.EMA29;
                }
                else 
                {
                    return EMA2129OrderPostition.EMA21;
                } 
            }
            else
            {
                return EMA2129OrderPostition.AdjustedEMA21;
            }             
        }

        private EMA2129SizingEnum GetEMA2129Sizing(double absolutedAngle)
        {
            if (absolutedAngle < Falcon_1m.RANGE_45_NO_TRD)
            {
                return EMA2129SizingEnum.Small;
            }
            else if (absolutedAngle >= Falcon_1m.RANGE_45_NO_TRD && absolutedAngle < Falcon_1m.RANGE_55_YES_TRD)
            {
                return EMA2129SizingEnum.Medium;
            }
            else // >= 55
            {
                return EMA2129SizingEnum.Big;
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

            var falcon1mVal = Falcon_1m.Value[0];
            var absolutedAngle = Math.Abs(falcon1mVal);
            var niceAngleToTrade = absolutedAngle >= MininumAngleToTrade;

            var sumTouches = EMA2129Status.CountTouch_EMA29 + EMA2129Status.CountTouch_EMA21 + EMA2129Status.CountTouch_EMA10_5m;

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
            else if (HammerCandle)
            {
                LocalPrint($"Có nến rút râu với cây nến {(EMA2129Status.Position == EMA2129Position.Above ? "XANH cao nhất" : "ĐỎ thấp nhất")} --> No Trade");
                return answer;
            }            
            else if (CountReverseCandles >= 4)
            {
                LocalPrint($"Có 4+ cây nến {(EMA2129Status.Position == EMA2129Position.Above ? "ĐỎ" : "XANH")} (ngược hướng) --> No Trade");
                return answer;
            }
            else if (CountReverseCandles == 3)
            {
              
                if (EMA2129Status.Position == EMA2129Position.Above && CandleUtilities.IsRedCandle(Close[0], Open[0]))
                {
                    LocalPrint($"Có 4 cây nến XANH --> No Trade");
                    return answer;
                }
                else if (EMA2129Status.Position == EMA2129Position.Below && CandleUtilities.IsGreenCandle(Close[0], Open[0]))
                {
                    LocalPrint($"Có 4 cây nến ĐỎ --> No Trade");
                    return answer;
                }
            }            
            else if (sumTouches > 0)
            {
                LocalPrint($"Đã chạm đường EMA29 hoặc EMA21, hoặc EMA10 (5m) trước đó rồi. Sum touches: {sumTouches} --> No Trade");
                return answer;
            }
            else if (!niceAngleToTrade)
            {
                LocalPrint($"Góc = {absolutedAngle:N2} < {MininumAngleToTrade} --> No Trade");
                return answer;
            }    
            else if (IsTouched(High[0], Low[0], EMA2129OrderPostition.EMA21))
            {
                LocalPrint("Mới chạm đường EMA21 --> No Trade");
                return answer;
            }
            else if (IsTouched(High[0], Low[0], EMA2129OrderPostition.EMA29))
            {
                LocalPrint("Mới chạm đường EMA29 --> No Trade");
                return answer;
            }
            else if (IsTouched(High[0], Low[0], EMA2129OrderPostition.EMA10_5m))
            {
                LocalPrint("Mới chạm đường EMA10_5m --> No Trade");
                return answer;
            }

            // Nếu đã có 4 cây nến đỏ hoặc xanh tính từ điểm cao (thấp) nhất khi bán (mua) 

            var ema21Val = EMA21Indicator_1m.Value[0];
            var ema10_5mVal = EMA10Indicator_5m.Value[0];
            var EMA50_5mVal = EMA50Indicator_5m.Value[0];            

            const int MAX_DISTANCE_BETWEEN_EMA50_5m_AND_EMA21 = 10;            

            // EMA21 (khung 1 phút) ở trên EMA10 (5 phút) hoặc ở dưới nhưng rất gần. 
            var ema21_Above_EMA10_5m = ema21Val >= ema10_5mVal; 
            var ema21_Below_EMA10_5m = ema21Val <= ema10_5mVal;

            var minValue = StrategiesUtilities.MinOfArray(ema21Val, ema10_5mVal);
            var maxValue = StrategiesUtilities.MaxOfArray(ema21Val, ema10_5mVal);

            const int MAX_DISTANCE_BETWEEN_EMA10_5m_AND_EMA21 = 7;
            var ema21_BelowAndNear_EM10_5m = ema21Val < ema10_5mVal && ema10_5mVal - ema21Val < MAX_DISTANCE_BETWEEN_EMA10_5m_AND_EMA21;
            var ema21_AboveAndNear_EM10_5m = ema21Val > ema10_5mVal && ema21Val - ema10_5mVal < MAX_DISTANCE_BETWEEN_EMA10_5m_AND_EMA21;

            var ema21_Above_EMA50_5m = ema21Val >= EMA50_5mVal;
            var ema21_Below_EMA50_5m = ema21Val <= EMA50_5mVal;

            var ema21_BelowAndNear_EM46_5m = ema21Val < EMA50_5mVal && 
                (EMA50_5mVal - ema21Val < MAX_DISTANCE_BETWEEN_EMA50_5m_AND_EMA21 || EMA50_5mVal - ema21Val >= 50);

            var ema21_AboveAndNear_EM46_5m = ema21Val > EMA50_5mVal && 
                (ema21Val - EMA50_5mVal < MAX_DISTANCE_BETWEEN_EMA50_5m_AND_EMA21 || ema21Val - EMA50_5mVal >= 50);

            if (EMA2129Status.Position == EMA2129Position.Above && falcon1mVal > 0 &&  Close[0] > maxValue && sumTouches == 0 && absolutedAngle >= MininumAngleToTrade)
            {
                answer.Postition = GetPostitionBasedOnAngleValue(absolutedAngle);
                answer.Sizing = GetEMA2129Sizing(absolutedAngle);

                // EMA 21 nằm trên cả EMA10 và EMA50 khung 5 phút
                if (ema21_Above_EMA10_5m && ema21_Above_EMA50_5m)
                {
                    LocalPrint($"[CONFIRM] Đủ điều kiện vào BUY, count: {sumTouches}");
                    answer.Action = GeneralTradeAction.Buy;
                }
                
                // EMA 21 nằm trên EMA10 nhưng dưới EMA50
                else if (ema21_Above_EMA10_5m && ema21_BelowAndNear_EM46_5m)
                {
                    LocalPrint($"[CONFIRM] Đủ điều kiện vào BUY, count: {sumTouches}");
                    answer.Action = GeneralTradeAction.Buy;
                }
                
                // EMA 21 nằm dưới EMA10 và dưới EMA50                
                else if (ema21_BelowAndNear_EM10_5m && ema21_BelowAndNear_EM46_5m)
                {
                    answer.Action = GeneralTradeAction.Buy;
                }                
                // Không có trường hợp EMA21 nằm dưới EMA50 nhưng lại nằm trên EMA10                
            }
            else if (EMA2129Status.Position == EMA2129Position.Below && falcon1mVal < 0 && Close[0] < minValue && sumTouches == 0 && absolutedAngle >= MininumAngleToTrade)
            {
                answer.Postition = GetPostitionBasedOnAngleValue(absolutedAngle);
                answer.Sizing = GetEMA2129Sizing(absolutedAngle);

                // EMA 21 nằm dưới cả EMA10 và EMA50 khung 5 phút
                if (ema21_Below_EMA10_5m && ema21_Below_EMA50_5m)
                {
                    LocalPrint($"[CONFIRM] Đủ điều kiện vào SELL, count: {sumTouches}");
                    answer.Action = GeneralTradeAction.Sell;
                }
                
                // EMA 21 nằm dưới EMA10 nhưng trên EMA50
                else if (ema21_Below_EMA10_5m && ema21_AboveAndNear_EM46_5m)
                {
                    LocalPrint($"[CONFIRM] Đủ điều kiện vào SELL, count: {sumTouches}");
                    answer.Action = GeneralTradeAction.Sell;
                }
                
                // EMA 21 nằm trên EMA10 và trên EMA50
                else if (ema21_AboveAndNear_EM10_5m && ema21_AboveAndNear_EM46_5m)
                {
                    answer.Action = GeneralTradeAction.Sell;
                }
                // Không có trường hợp EMA21 nằm trên EMA50 nhưng lại nằm dưới EMA10
                
            }
           
            return answer;
        }

        protected override (AtmStrategy, string) GetAtmStrategyByPnL(EMA2129OrderDetail tradeAction)
        {
            var time = ToTime(Time[0]) ;
            var isNightTime = time >= 17_00_00 || time <= 08_30_00;

            if (tradeAction.Sizing == EMA2129SizingEnum.Big)
            {
                return isNightTime 
                    ? (HalfSizeAtmStrategy, HalfSizefATMName)
                    : (FullSizeAtmStrategy, FullSizeATMName);
            }
            else if (tradeAction.Sizing == EMA2129SizingEnum.Medium)
            {
                return
                    isNightTime 
                    ? (RiskyAtmStrategy, RiskyAtmStrategyName)
                    : (HalfSizeAtmStrategy, HalfSizefATMName);
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
                if (CurrentTradeAction.Action != checkShouldTradeAgain.Action || CurrentTradeAction.Sizing != checkShouldTradeAgain.Sizing)
                {

                    #region Cancel current order and enter new one
                    CancelAllPendingOrder();

                    EnterOrder(checkShouldTradeAgain);
                    #endregion
                }
                // Ngược lại thì update điểm vào lệnh
                else 
                {
                    // EMA2129Status.SetEnteredOrder(checkShouldTradeAgain.Postition);

                    UpdatePendingOrderPure(newPrice, stopLossPrice, targetPrice_Full, targetPrice_Half);                    
                }
            }
        }        

        protected override double GetSetPrice(EMA2129OrderDetail tradeAction, AtmStrategy additionalInfo)
        {
            var adjustment = AdjustmentPoint * 1.0;
            var ans = -1.0; 
            
            switch (tradeAction.Postition)
            {
                case EMA2129OrderPostition.AdjustedEMA21:
                    adjustment = AdjustmentPoint; 
                    break;

                case EMA2129OrderPostition.EMA21:
                    adjustment = AdjustmentPoint; 
                    break;
                // Note: Vẫn dùng EMA21 +/- AdjustmentPoint để vào lệnh
                case EMA2129OrderPostition.EMA29:
                    adjustment = 6;
                    break;               
            }

            var time = ToTime(Time[0]);
            var isNightTime = time >= 17_00_00 || time <= 08_30_00;

            if (isNightTime)
            {
                adjustment = adjustment * 0.7; 
            }

            ans = tradeAction.Action == GeneralTradeAction.Buy
                       ? EMA21Indicator_1m.Value[0] + adjustment
                       : EMA21Indicator_1m.Value[0] - adjustment;


            return StrategiesUtilities.RoundPrice(ans);
        }

        protected override double GetStopLossPrice(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy atmStrategy)
        {   
            if (State == State.Historical)
            {
                var stopLoss =
                    tradeAction.Sizing == EMA2129SizingEnum.Big ? 120 : // 30 pts for BIG
                    tradeAction.Sizing == EMA2129SizingEnum.Medium ? 100 : // 25 pts 
                    tradeAction.Sizing == EMA2129SizingEnum.Small ? 80 :
                    0;

                LocalPrint($"[GetStopLossPrice] {tradeAction.Sizing} {stopLoss}");

                return stopLoss;
            }            

            var stopLossTick = atmStrategy.Brackets[0].StopLoss;

            return IsBuying ?
                setPrice - stopLossTick * TickSize :
                setPrice + stopLossTick * TickSize;
        }

        protected override double GetTargetPrice_Full(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy atmStrategy)
        {
            if (State == State.Historical)
            {
                var targetFull =
                    tradeAction.Sizing == EMA2129SizingEnum.Big ? 120 : // 30 pts for BIG
                    tradeAction.Sizing == EMA2129SizingEnum.Medium ? 80 : // 20 pts 
                    tradeAction.Sizing == EMA2129SizingEnum.Small ? 40 : // 10
                    0;
                LocalPrint($"[GetTargetPrice_Full] {tradeAction.Sizing} {targetFull}");
                return targetFull;
            }

            var targetTick_Full = IsBuying ? atmStrategy.Brackets.Max(c => c.Target) : atmStrategy.Brackets.Min(c => c.Target);

            return IsBuying ?
                setPrice + targetTick_Full * TickSize :
                setPrice - targetTick_Full * TickSize;
        }

        protected override double GetTargetPrice_Half(EMA2129OrderDetail tradeAction, double setPrice, AtmStrategy atmStrategy)
        {
            if (State == State.Historical)
            {
                var targetHalf =
                    tradeAction.Sizing == EMA2129SizingEnum.Big ? 120 : // 30 pts for BIG
                    tradeAction.Sizing == EMA2129SizingEnum.Medium ? 80 : // 20 pts 
                    tradeAction.Sizing == EMA2129SizingEnum.Small ? 40 : // 10
                    0;

                LocalPrint($"[GetTargetPrice_Half] {tradeAction.Sizing} {targetHalf}");
                return targetHalf;
            }

            var targetTick_Half = IsBuying ? atmStrategy.Brackets.Min(c => c.Target) : atmStrategy.Brackets.Max(c => c.Target);

            return IsBuying ?
                setPrice + targetTick_Half * TickSize :
                setPrice - targetTick_Half * TickSize;
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
