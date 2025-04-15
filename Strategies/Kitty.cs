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
	public class Kitty : Rooster
	{

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

        public Kitty() : base("KITTY")
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "atmStrategyKitty.txt");
            Configured_TimeFrameToTrade = TimeFrameToTrade.FiveMinutes;
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



            StartDayTradeTime = new TimeSpan(9, 10, 0); // 9:10:00 am 
            EndDayTradeTime = new TimeSpan(15, 0, 0); // 2:00:00 pm

            CloseOrderWhenCandleGreaterThan = 60; // 60 điểm
        }

        protected override TradeAction ShouldTrade()
        {
            if (Time[0].TimeOfDay < StartDayTradeTime || Time[0].TimeOfDay > EndDayTradeTime)
            {
                LocalPrint($"Thời gian trade được thiết lập từ {StartDayTradeTime} to {EndDayTradeTime} --> No Trade.");
                return TradeAction.NoTrade;
            }            

            return TradeAction.NoTrade;         
        }

        protected override (AtmStrategy, string) GetAtmStrategyByPnL(TradeAction tradeAction)
        {
            /*
             * Các trường hợp risky
             */

            // Trường hợp 1: Cây nến có thân nhỏ hơn cả râu trên và râu phía dưới. 
            var currentBodyLength = Math.Abs(closePrice_5m - openPrice_5m);
            var bodyIsSmallerThanOthers = currentBodyLength < (highPrice_5m - Math.Max(closePrice_5m, openPrice_5m))
                && currentBodyLength < (Math.Min(closePrice_5m, openPrice_5m) - lowPrice_5m);
            
            if (bodyIsSmallerThanOthers)
            {
                return (RiskyAtmStrategy, RiskyAtmStrategyName);
            }            

            // Trường hợp 2: Giá vào lệnh và target băng qua đường EMA46/51. 
            // Đây là 1 key quan trọng nên rất dễ bị reverse. 

            // Lấy thử giá (giả sử là full size) 
            var assumeAtmStrategy = FullSizeAtmStrategy; 
            var assumeEntryPrice = GetSetPrice(tradeAction, assumeAtmStrategy);
            var target1 = tradeAction == TradeAction.Buy_Trending ? assumeEntryPrice + assumeAtmStrategy.Brackets[0].Target * TickSize
                : assumeEntryPrice - assumeAtmStrategy.Brackets[0].Target * TickSize;

            //  Giá vào lệnh và target băng qua đường EMA46/51. 
            if ((target1 < middleEma4651_5m && assumeEntryPrice > middleEma4651_5m) || (target1 > middleEma4651_5m && assumeEntryPrice < middleEma4651_5m))
            {
                return (RiskyAtmStrategy, RiskyAtmStrategyName);
            }

            // Trường hợp 3: Volume giảm dần (NOT IN USE)
            /*
            var currentWAE = waeValuesSeries[0];
            var previousWAE = waeValuesSeries[1];
            var previous2WAE = waeValuesSeries[2];

            var descreaseBULLVolume = tradeAction == TradeAction.Buy_Trending &&
                (currentWAE.UpTrendVal < previousWAE.UpTrendVal || (previousWAE.UpTrendVal < previous2WAE.UpTrendVal && previous2WAE.UpTrendVal > 0));
            var descreaseBEARVolume = tradeAction == TradeAction.Sell_Trending &&
                (currentWAE.DownTrendVal < previousWAE.DownTrendVal || (previousWAE.DownTrendVal < previous2WAE.DownTrendVal && previous2WAE.DownTrendVal > 0));
            */

            var todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachHalf =
                (todaysPnL <= (-MaximumDailyLoss / 2)) || (todaysPnL >= (DailyTargetProfit / 2));

            return reachHalf ? (HalfSizeAtmStrategy, HalfSizefATMName) : (FullSizeAtmStrategy, FullSizeATMName);
        }

        protected override double GetSetPrice(TradeAction tradeAction, AtmStrategy atmStrategy)
        {
            return StrategiesUtilities.RoundPrice((ema29_1m + ema10_5m) / 2);
        }

        protected override bool ShouldCancelPendingOrdersByTrendCondition()
        {
            if (IsTrendingTrade)
            {
                // Nến gần nhất là ĐỎ hoặc nến rút râu phía trên
                var reverseRed = CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m) || CandleUtilities.TopToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

                // Nến gần nhất là ĐỎ hoặc nến rút râu phía dưới
                var reverseGreen = CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m) || CandleUtilities.BottomToBodyPercentage(closePrice_5m, openPrice_5m, highPrice_5m, lowPrice_5m) > 50;

                if (IsBuying && reverseRed)
                {
                    LocalPrint($"Đang có lệnh MUA nhưng lại xuất hiện nến ĐỎ hoặc hoặc nến rút râu phía trên (>50%)");
                    return true;
                }

                if (IsSelling && reverseGreen)
                {
                    LocalPrint($"Đang có lệnh BÁN nhưng lại xuất hiện nến XANH hoặc nến rút râu phía dưới (>50%)");
                    return true;
                }

                return base.ShouldCancelPendingOrdersByTrendCondition();
            }

            return false;
        }


        protected override void UpdateExistingOrder()
        {
            LocalPrint("[UpdateExistingOrder - Kitty] - NOT Using now");
            return; 

            // NOT USE NOW
            
            /*
             * Giải thuật hiện tại của Kitty: 
             * - Sau khi ATM đưa stop loss về break even
             * - Nếu đóng nến xanh (khi đang có lệnh mua) thì chuyển stop loss, nếu nến xanh > 60 pts thì đóng lệnh
             * - Nếu đóng nến đỏ (khi đang có lệnh bán) thì chuyển stop loss, nếu nến đỏ > 60 pts thì đóng lệnh
             * 
             */
            var stopOrders = Account.Orders.Where(order => order.OrderState == OrderState.Accepted && order.Name.Contains(OrderStopName)).ToList();            

            var stopOrder = stopOrders.First();

            if (stopOrders.Count == 1)
            {
                LocalPrint($"Có {stopOrders.Count} stop order, should move stop loss if pass condition.");

                var stopOrderPrice = stopOrder.LimitPrice;

                var filledPrice = FilledPrice;

                bool allowMoving = false;                
                double newPrice = -1;
                var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);

                if (IsBuying && filledPrice <= stopOrderPrice && CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, null, null))
                {
                    if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                    {
                        LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                        CloseExistingOrders();
                    }
                    else if (bodyLength > 12)
                    {
                        var newStopLossBasedOnGreenCandle = StrategiesUtilities.RoundPrice(openPrice_5m + Math.Abs(closePrice_5m - openPrice_5m) / 3);

                        allowMoving = stopOrderPrice < newStopLossBasedOnGreenCandle;

                        if (allowMoving)
                        {
                            LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnGreenCandle:N2}");
                            newPrice = newStopLossBasedOnGreenCandle;
                        }
                    }
                }
                else if (IsSelling && filledPrice >= stopOrderPrice && CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, null, null))
                {
                    if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                    {
                        LocalPrint($"Nến đỏ có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                        CloseExistingOrders();
                    }
                    else if (bodyLength > 12)
                    {
                        var newStopLossBasedOnRedCandle = StrategiesUtilities.RoundPrice(openPrice_5m - Math.Abs(closePrice_5m - openPrice_5m) / 3);

                        allowMoving = stopOrderPrice > newStopLossBasedOnRedCandle;

                        if (allowMoving)
                        {
                            LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnRedCandle:N2}");
                            newPrice = newStopLossBasedOnRedCandle;
                        }
                    } 
                }

                if (allowMoving)
                {
                    LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{currentPrice}]");

                    MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
                }
            }
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
                    
                    
                    // Nếu giá đã về break even và cây nến là XANH
                    if (filledPrice <= stopOrderPrice && CandleUtilities.IsGreenCandle(closePrice_5m, openPrice_5m, null, null))
                    {
                        var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);

                        if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                        {
                            LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                            CloseExistingOrders();
                        }
                        else if (bodyLength > 12)
                        {
                            var newStopLossBasedOnGreenCandle = StrategiesUtilities.RoundPrice(openPrice_5m + Math.Abs(closePrice_5m - openPrice_5m) / 3);

                            allowMoving = stopOrderPrice < newStopLossBasedOnGreenCandle;

                            if (allowMoving)
                            {
                                LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnGreenCandle:N2}");
                                newPrice = newStopLossBasedOnGreenCandle;
                            }
                        }
                    }
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

                    #region Code mới - Dịch stop loss dựa trên cây nến đỏ gần nhất
                    
                    if (filledPrice >= stopOrderPrice && CandleUtilities.IsRedCandle(closePrice_5m, openPrice_5m, null, null))
                    {
                        var bodyLength = Math.Abs(closePrice_5m - openPrice_5m);


                        if (bodyLength >= CloseOrderWhenCandleGreaterThan)
                        {
                            LocalPrint($"Nến xanh có body > {CloseOrderWhenCandleGreaterThan} pts, chốt lời. --> Close order.");
                            CloseExistingOrders();
                        }
                        else if (bodyLength > 12)
                        {
                            var newStopLossBasedOnRedCandle = StrategiesUtilities.RoundPrice(openPrice_5m - Math.Abs(closePrice_5m - openPrice_5m) / 3);

                            allowMoving = stopOrderPrice > newStopLossBasedOnRedCandle;

                            if (allowMoving)
                            {
                                LocalPrint($"Chuyển stop loss đến {newStopLossBasedOnRedCandle:N2}");
                                newPrice = newStopLossBasedOnRedCandle;
                            }
                        }    
                    }
                    
                    #endregion
                }
            }

            if (allowMoving)
            {
                LocalPrint($"Trying to move stop order to [{newPrice:N2}]. Filled Price: [{filledPrice:N2}], current Stop: {stopOrderPrice}, updatedPrice: [{updatedPrice}]");

                MoveTargetOrStopOrder(newPrice, stopOrder, false, IsBuying ? "BUY" : "SELL", stopOrder.FromEntrySignal);
            }
        }
    }

}
