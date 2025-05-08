#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Custom.Strategies;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public abstract class FVG : BarClosedBaseClass<GeneralTradeAction, FVGTradeDetail>
    {
        // Constants 
        const string Configuration_FVGGroup_Name = "FVG Strategy";
        public FVG() : base("FVG")
        {
            HalfPriceSignals = new HashSet<string> { StrategiesUtilities.SignalEntry_FVGHalf };

            EntrySignals = new HashSet<string>
            {
                StrategiesUtilities.SignalEntry_FVGHalf,
                StrategiesUtilities.SignalEntry_FVGFull
            };
        }

        /// <summary>
        /// Khoảng cách tối thiểu giữa điểm cao (thấp) nhất của cây nến 1 và điểm thấp (cao) nhất của cây nến 3
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Cách đặt stoploss/gain",            
            Order = 3,
            GroupName = Configuration_FVGGroup_Name)]
        public FVGWayToSetStopLoss WayToSetStopLoss { get; set; }

        /// <summary>
        /// Khoảng cách tối thiểu giữa điểm cao (thấp) nhất của cây nến 1 và điểm thấp (cao) nhất của cây nến 3
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Khoảng cách",
            Description = "Khoảng cách tối thiểu giữa điểm cao (thấp) nhất của cây nến 1 và điểm thấp (cao) nhất của cây nến 3",
            Order = 3,
            GroupName = Configuration_FVGGroup_Name)]
        public double MinDistanceToDetectFVG { get; set; } = 0.5;

        /// <summary>
        /// Số lượng contract cho target 1
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lượng contract cho target 1",
            Description = "Số lượng contract cho target 1",
            Order = 1,            
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public int QuantityTargetHalf { get; set; } = 2;

        /// <summary>
        /// Số lượng contract cho target 1
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Số lượng contract cho target 2",
            Description = "Số lượng contract cho target 2",
            Order = 2,
            GroupName = StrategiesUtilities.Configuration_StopLossTarget_Name)]
        public int QuantityTargetFull { get; set; } = 2;

        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == GeneralTradeAction.Sell;
            }
        }

        protected override bool IsBuying
        {
            get
            {
                return CurrentTradeAction == GeneralTradeAction.Buy;
            }
        }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Description = @"Fair Value Gap Strategy";
            Name = "Monkey (FVG + WAE)";
            BarsRequiredToTrade = 10;

            WayToSetStopLoss = FVGWayToSetStopLoss.BasedOnFVGGap;

            StopLossInTicks = 120;
            Target1InTicks = 100;
            Target2InTicks = 120;

            SetOrderQuantity = SetOrderQuantity.Strategy;
            DefaultQuantity = 2;

            QuantityTargetHalf = 2;
            QuantityTargetFull = 2;
        }

        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.Configure)
            {
                // Add data for trading
                AddDataSeries(BarsPeriodType.Minute, 5);

                CurrentTradeAction = GeneralTradeAction.NoTrade;
            }
            else if (State == State.DataLoaded)
            {
                deadZoneSeries = new Series<double>(this);
                waeValuesSeries = new Series<WAE_ValueSet>(this);
            }
            
        }

        private Series<double> deadZoneSeries;
        private Series<WAE_ValueSet> waeValuesSeries;

        WAE_ValueSet waeValueSet_5m = null;
        
        protected override void OnBarUpdate()
        {
            StrategiesUtilities.CalculatePnL(this, Account, Print);

            var passTradeCondition = CheckingTradeCondition();
            if (!passTradeCondition)
            {
                return;
            }

            base.OnBarUpdate();

            if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value == 5) // 5 minute
            {
                waeValueSet_5m = FindWaddahAttarExplosion();
                waeValuesSeries[0] = waeValueSet_5m;

                if (TradingStatus == TradingStatus.Idle)
                {
                    // Find the FVG value 
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if ((shouldTrade == GeneralTradeAction.Buy)
                        || (shouldTrade == GeneralTradeAction.Sell))
                    {
                        EnterOrder(shouldTrade);
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    // Cancel order
                    var shouldCancelOrder = ShouldCancelPendingOrdersByTimeCondition(FilledTime);
                    if (shouldCancelOrder)
                    {
                        CancelAllPendingOrder();
                        return;
                    }

                    var cancelCausedByTrendCondition =
                            // Trend suy yếu, 
                            waeValuesSeries[0].IsInDeadZone ||
                            // Hiện tại có xu hướng bearish nhưng lệnh chờ là BUY
                            (IsBuying && waeValuesSeries[0].HasBEARVolume) ||
                            // Hiện tại có xu hướng bullish nhưng lệnh chờ là SELL
                            (IsSelling && waeValuesSeries[0].HasBULLVolume);

                    if (cancelCausedByTrendCondition)
                    {
                        CancelAllPendingOrder();
                        LocalPrint($"Cancel lệnh do xu hướng hiện tại ngược với lệnh chờ");
                        return;
                    }

                    var shouldChangeVal = ShouldTrade();

                    // Nếu có vùng giá mới thì cập nhật
                    if (shouldChangeVal == GeneralTradeAction.NoTrade)
                    {
                        return;
                    }

                    if (shouldChangeVal == CurrentTradeAction)
                    {
                        var clonedList = ActiveOrders.Values.ToList();
                        var len = clonedList.Count;

                        var newPrice = GetSetPrice(shouldChangeVal, CurrentFVGTradeDetail);

                        var stopLossPrice = GetStopLossPrice(shouldChangeVal, newPrice, CurrentFVGTradeDetail);

                        var targetPrice_Half = GetTargetPrice_Half(shouldChangeVal, newPrice, CurrentFVGTradeDetail);

                        var targetPrice_Full = GetTargetPrice_Full(shouldChangeVal, newPrice, CurrentFVGTradeDetail);

                        for (var i = 0; i < len; i++)
                        {
                            var order = clonedList[i];
                            try
                            {
                                LocalPrint($"Trying to modify waiting order [{order.Name}], " +
                                    $"current Price: {order.LimitPrice}, current stop: {order.StopPrice}, " +
                                    $"new Price: {newPrice:N2}, new stop loss: {stopLossPrice}");

                                ChangeOrder(order, order.Quantity, newPrice, order.StopPrice);

                                SetStopLoss(order.Name, CalculationMode.Price, stopLossPrice, false);

                                if (IsHalfPriceOrder(order))
                                {
                                    SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Half, false);
                                }
                                else if (IsFullPriceOrder(order))
                                {
                                    SetProfitTarget(order.Name, CalculationMode.Price, targetPrice_Full, false);
                                }

                                FilledPrice = newPrice;
                            }
                            catch (Exception ex)
                            {
                                LocalPrint($"[UpdatePendingOrder] - ERROR: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // Hủy lệnh cũ và order lệnh mới 
                        CancelAllPendingOrder();

                        EnterOrder(shouldChangeVal);
                    }
                }
                else if (TradingStatus == TradingStatus.OrderExists)
                {
                    // Cập nhật lại target 2 
                }
            }
        }

        private void DrawFVGBox(double fillPrice, double endOfGap, double stopLoss, double target, bool hasVolume)
        {
            Draw.Rectangle(this, $"FVG_{CurrentBar}_1", false, 0, stopLoss, -2, endOfGap, Brushes.Transparent,
                !hasVolume ? Brushes.DarkGray : Brushes.Red, 
                30);

            Draw.Rectangle(this, $"FVG_{CurrentBar}_3", false, 0, fillPrice, -2, endOfGap, Brushes.Transparent,
                !hasVolume ? Brushes.Gray : Brushes.Yellow,
                30);

            Draw.Rectangle(this, $"FVG_{CurrentBar}_2", false, 0, target, -2, fillPrice, Brushes.Transparent,
                !hasVolume ? Brushes.LightGray : Brushes.LightGreen, 
                30);
        }

        protected override void EnterOrder(GeneralTradeAction action)
        {
            // Set global values
            CurrentTradeAction = action;
            CurrentFVGTradeDetail = NewFVGTradeDetail;

            BackTestStopLossInTicks = StopLossInTicks;
            BackTestTargetInTicks = Target2InTicks;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var orderAction = action == GeneralTradeAction.Buy ? OrderAction.Buy : OrderAction.Sell;

            try
            {
                double priceToSet = GetSetPrice(action, NewFVGTradeDetail);
                FilledPrice = priceToSet;

                var stopLossPrice = GetStopLossPrice(action, priceToSet, NewFVGTradeDetail);
                var targetHalf = GetTargetPrice_Half(action, priceToSet, NewFVGTradeDetail);
                var targetFull = GetTargetPrice_Full(action, priceToSet, NewFVGTradeDetail);

                var quantityHalf = GetNumberOfContracts_Half(NewFVGTradeDetail);
                var quantityFull = GetNumberOfContracts_Full(NewFVGTradeDetail);

                EnterOrderPureUsingTicks(priceToSet, targetHalf, stopLossPrice,
                    StrategiesUtilities.SignalEntry_FVGHalf, quantityHalf,
                    IsBuying, IsSelling);

                EnterOrderPureUsingTicks(priceToSet, targetFull, stopLossPrice,
                    StrategiesUtilities.SignalEntry_FVGFull, quantityFull,
                    IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override double GetStopLossPrice(GeneralTradeAction tradeAction, double setPrice, FVGTradeDetail fVGTradeDetail)
        {
            var stopLossBaseOnFVG =
                (CurrentFVGTradeDetail.StopLossDistance < 7) ? 10
                : (CurrentFVGTradeDetail.StopLossDistance >= 7 && CurrentFVGTradeDetail.StopLossDistance < 10) ? 15
                : (CurrentFVGTradeDetail.StopLossDistance >= 10 && CurrentFVGTradeDetail.StopLossDistance < 15) ? 20
                : (CurrentFVGTradeDetail.StopLossDistance >= 15 && CurrentFVGTradeDetail.StopLossDistance < 20) ? 25
                : 30;

            var stoploss = WayToSetStopLoss == FVGWayToSetStopLoss.FixedNumberOfTicks
                ? (StopLossInTicks * TickSize)
                : stopLossBaseOnFVG;

            return CurrentFVGTradeDetail.FVGTradeAction == GeneralTradeAction.Buy
                ? setPrice - stoploss
                : setPrice + stoploss;
        }

        protected override double GetTargetPrice_Half(GeneralTradeAction tradeAction, double setPrice, FVGTradeDetail fVGTradeDetail)
        {
            return GetTargetPrice_Half(CurrentFVGTradeDetail, setPrice);
        }

        private double GetTargetPrice_Half(FVGTradeDetail tradeDetail, double setPrice)
        {
            var targetBasedOnFVG =
                (tradeDetail.TargetProfitDistance < 7) ? 5
                : (tradeDetail.TargetProfitDistance >= 7 && tradeDetail.TargetProfitDistance < 10) ? 5
                : (tradeDetail.TargetProfitDistance >= 10 && tradeDetail.TargetProfitDistance < 15) ? 7
                : (tradeDetail.TargetProfitDistance >= 15 && tradeDetail.TargetProfitDistance < 20) ? 10
                : 15;

            var target = WayToSetStopLoss == FVGWayToSetStopLoss.FixedNumberOfTicks
                ? (Target1InTicks * TickSize)
                : targetBasedOnFVG;

            return CurrentTradeAction == GeneralTradeAction.Buy
                ? setPrice + target
                : setPrice - target;
        }

        protected override double GetTargetPrice_Full(GeneralTradeAction tradeAction, double setPrice, FVGTradeDetail fVGTradeDetail)
        {
            return GetTargetPrice_Full(CurrentFVGTradeDetail, setPrice); 
        }
        protected double GetTargetPrice_Full(FVGTradeDetail tradeDetail, double setPrice)
        {
            var targetBasedOnFVG =
                (tradeDetail.TargetProfitDistance < 7) ? 7
                : (tradeDetail.TargetProfitDistance >= 7 && tradeDetail.TargetProfitDistance < 10) ? 7
                : (tradeDetail.TargetProfitDistance >= 10 && tradeDetail.TargetProfitDistance < 15) ? 10
                : (tradeDetail.TargetProfitDistance >= 15 && tradeDetail.TargetProfitDistance < 20) ? 15
                : (tradeDetail.TargetProfitDistance >= 20 && tradeDetail.TargetProfitDistance < 25) ? 20
                : 30;

            var target = WayToSetStopLoss == FVGWayToSetStopLoss.FixedNumberOfTicks
                ? (Target2InTicks * TickSize)
                : targetBasedOnFVG;

            return tradeDetail.FVGTradeAction == GeneralTradeAction.Buy
                ? setPrice + target
                : setPrice - target;
        }

        private int GetNumberOfContracts_Half(FVGTradeDetail tradeDetail)
        {
            var quantity =
                (tradeDetail.StopLossDistance < 7) ? 3 // 10 loss 
                : (tradeDetail.StopLossDistance >= 7 && tradeDetail.StopLossDistance < 10) ? 2 // 15 loss 
                : (tradeDetail.StopLossDistance >= 10 && tradeDetail.StopLossDistance < 15) ? 2 // 20 loss 
                : (tradeDetail.StopLossDistance >= 15 && tradeDetail.StopLossDistance < 20) ? 2 // 25 loss 
                : 2; // 30 loss 

            return WayToSetStopLoss == FVGWayToSetStopLoss.FixedNumberOfTicks
                ? QuantityTargetHalf
                : quantity;
        }

        private int GetNumberOfContracts_Full(FVGTradeDetail tradeDetail)
        {
            var quantity =
                (tradeDetail.StopLossDistance < 7) ? 3
                : (tradeDetail.StopLossDistance >= 7 && tradeDetail.StopLossDistance < 10) ? 2
                : (tradeDetail.StopLossDistance >= 10 && tradeDetail.StopLossDistance < 15) ? 2
                : (tradeDetail.StopLossDistance >= 15 && tradeDetail.StopLossDistance < 20) ? 2
                : 1;

            return WayToSetStopLoss == FVGWayToSetStopLoss.FixedNumberOfTicks
                ? QuantityTargetFull
                : quantity;
        }

        protected override GeneralTradeAction ShouldTrade()
        {
            if (High[2] < Low[0] && Low[0] - High[2] > MinDistanceToDetectFVG)
            {
                // Draw box 
                DrawFVGBox(High[2], Low[0], Low[2], High[0], waeValueSet_5m.HasBULLVolume);

                if (waeValueSet_5m.HasBULLVolume)
                {
                    FilledTime = Time[0];

                    NewFVGTradeDetail = new FVGTradeDetail
                    {
                        FilledPrice = High[2],
                        FVGTradeAction = GeneralTradeAction.Buy,
                        StopLossPrice = Low[2],
                        TargetProfitPrice = High[0],
                        BarIndex = CurrentBar
                    };
                    return GeneralTradeAction.Buy;
                }
            }
            else if (Low[2] > High[0] && Low[2] - High[0] > MinDistanceToDetectFVG)
            {
                // Draw box 
                DrawFVGBox(Low[2], High[0], High[2], Low[0], waeValueSet_5m.HasBEARVolume);

                if (waeValueSet_5m.HasBEARVolume)
                {
                    FilledTime = Time[0];

                    NewFVGTradeDetail = new FVGTradeDetail
                    {
                        FilledPrice = Low[2],
                        FVGTradeAction = GeneralTradeAction.Sell,
                        StopLossPrice = High[2],
                        TargetProfitPrice = Low[0],
                        BarIndex = CurrentBar
                    };
                    return GeneralTradeAction.Sell;
                }
            }

            NewFVGTradeDetail = new FVGTradeDetail
            {
                FilledPrice = -1,
                FVGTradeAction = GeneralTradeAction.NoTrade,
                StopLossPrice = -1,
                TargetProfitPrice = -1,
                BarIndex = -1
            };
            return GeneralTradeAction.NoTrade;
        }

        private FVGTradeDetail CurrentFVGTradeDetail { get; set; }

        private FVGTradeDetail NewFVGTradeDetail { get; set; }

        protected override double GetSetPrice(GeneralTradeAction tradeAction, FVGTradeDetail fVGTradeDetail)
        {
            return NewFVGTradeDetail.FilledPrice;
        }

        /// <summary>
        /// Tìm các giá trị của Waddah Attar Explosion ở khung 5 phút
        /// </summary>
        /// <returns></returns>
        private WAE_ValueSet FindWaddahAttarExplosion()
        {
            int sensitivity = 150;
            int fastLength = 20;
            int slowLength = 40;
            int channelLength = 20;
            double mult = 2.0;

            // WAE
            // Calculate Typical Price
            double typicalPrice = (High[0] + Low[0] + Close[0]) / 3.0;

            // Calculate True Range and store it in a Series
            double trueRange = Math.Max(High[0] - Low[0], Math.Max(Math.Abs(High[0] - Close[1]), Math.Abs(Low[0] - Close[1])));
            deadZoneSeries[0] = trueRange; // Initialize the first value

            // Calculate smoothed ATR using EMA of the True Range Series
            double smoothedATR = EMA(deadZoneSeries, 100)[0];

            // Dead Zone
            double deadZone = smoothedATR * 3.7;

            // MACD Difference Calculation
            double fastEMA = EMA(Close, fastLength)[0];
            double slowEMA = EMA(Close, slowLength)[0];
            double prevFastEMA = EMA(Close, fastLength)[1];
            double prevSlowEMA = EMA(Close, slowLength)[1];

            double macd = fastEMA - slowEMA;
            double prevMacd = prevFastEMA - prevSlowEMA;
            double trendCalculation = (macd - prevMacd) * sensitivity;

            // Bollinger Bands Calculation
            double bbBasis = SMA(Close, channelLength)[0];
            double bbDev = mult * StdDev(Close, channelLength)[0];
            double bbUpperVal = bbBasis + bbDev;
            double bbLowerVal = bbBasis - bbDev;

            // Explosion Line
            double explosionValue = bbUpperVal - bbLowerVal;

            return new WAE_ValueSet
            {
                DeadZoneVal = deadZone,
                DownTrendVal = trendCalculation < 0 ? -trendCalculation : 0,
                ExplosionVal = explosionValue,
                UpTrendVal = trendCalculation >= 0 ? trendCalculation : 0
            };
        }

        protected override bool IsHalfPriceOrder(Cbi.Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_FVGHalf;
        }

        protected override bool IsFullPriceOrder(Cbi.Order order)
        {
            return order.Name == StrategiesUtilities.SignalEntry_FVGFull;
        }
    }
}
