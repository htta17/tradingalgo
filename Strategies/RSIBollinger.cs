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
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class RSIBollinger : BarClosedBaseClass<RSIBollingerAction, RSIBollingerAction>
    {
        public RSIBollinger() : base("TIGER")
        {
            HalfPriceSignals = new List<string> { StrategiesUtilities.SignalEntry_RSIBollingerHalf };

            StrategySignals = new List<string> 
            { 
                StrategiesUtilities.SignalEntry_RSIBollingerHalf ,
                StrategiesUtilities.SignalEntry_RSIBollingerFull ,
            };
        }        

        private const string Configuration_TigerParams_Name = "Tiger parameters";

        /// <summary>
        /// Chiều cao tối thiếu của body cây nến ABS(Open - Close) 
        /// </summary>
        /// [NinjaScriptProperty]
        [Display(Name = "Minimum Candle body size",
            Order = 1,
            Description = "Chiều cao tối thiếu của body cây nến",
            GroupName = Configuration_TigerParams_Name)]        
        public int MinimumCandleBody {  get; set; }

        /// <summary>
        /// OverSoldValue
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Over sold value",
            Order = 1,
            GroupName = Configuration_TigerParams_Name)]
        [Range(0, 49)]
        public int OverSoldValue { get; set; }

        /// <summary>
        /// OverSoldValue
        /// </summary>
        [NinjaScriptProperty]
        [Display(Name = "Over bought value",
            Order = 1,
            GroupName = Configuration_TigerParams_Name)]
        [Range(51, 100)]
        public int OverBoughtValue { get; set; }

        protected override void SetDefaultProperties()
        {
            base.SetDefaultProperties();

            Name = "Tiger [RSI + Bollinger (Reverse)]";

            OverBoughtValue = 70;
            OverSoldValue = 30;
            MinimumCandleBody = 2;

            Target1InTicks = 40;
            Target2InTicks = 120;
            StopLossInTicks = 120;

            DefaultQuantity = 2; 
        }
        
        private Bollinger bollinger1 { get; set; }
        private Bollinger bollinger2 { get; set; }
        private RSI rsi { get; set; }

        protected override bool IsBuying
        { 
            get 
            { 
                return CurrentTradeAction == RSIBollingerAction.SetBuyOrder; 
            } 
        }

        protected override bool IsSelling
        {
            get
            {
                return CurrentTradeAction == RSIBollingerAction.SetSellOrder;
            }
        }

        protected double lowPrice_5m = -1;
        protected double highPrice_5m = -1;

        protected double closePrice_5m = -1;
        protected double openPrice_5m = -1;


        protected override void OnStateChange()
		{
			base.OnStateChange();

            if (State == State.Configure)
            {
                ClearOutputWindow();
                AddDataSeries(BarsPeriodType.Minute, 5);
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                bollinger1 = Bollinger(1, 20);
                bollinger1.Plots[0].Brush = bollinger1.Plots[2].Brush = Brushes.DarkCyan;
                bollinger1.Plots[1].Brush = Brushes.DeepPink;

                bollinger2 = Bollinger(2, 20);
                bollinger2.Plots[0].Brush = bollinger2.Plots[2].Brush = Brushes.DarkCyan;
                bollinger2.Plots[1].Brush = Brushes.DeepPink;

                rsi = RSI(14, 3);
                rsi.Plots[0].Brush = Brushes.DeepPink;
                rsi.Plots[1].Brush = Brushes.Gray;

                AddChartIndicator(bollinger1);
                AddChartIndicator(bollinger2);                

                AddChartIndicator(rsi);

               
            }
            else if (State == State.Realtime)
            {
                try
                {
                    // Nếu có lệnh đang chờ thì cancel 
                    TransitionOrdersToLive();
                }
                catch (Exception e)
                {
                    LocalPrint("[OnStateChange] - ERROR" + e.Message);
                }       
            }
        }

        protected virtual void EnterOrder(RSIBollingerAction tradeAction)
        {
            // Set global values
            CurrentTradeAction = tradeAction;

            // Chưa cho move stop loss
            StartMovingStoploss = false;

            var action = IsBuying ? OrderAction.Buy : OrderAction.Sell;

            LocalPrint($"Enter {action} at {Time[0]}");

            double priceToSet = GetSetPrice(tradeAction);
            filledPrice = priceToSet;

            var stopLossPrice = GetStopLossPrice(CurrentTradeAction, priceToSet);
            var targetHalf = GetTargetPrice_Half(CurrentTradeAction, priceToSet);
            var targetFull = GetTargetPrice_Full(CurrentTradeAction, priceToSet);

            try
            {                
                EnterOrderPure(priceToSet, targetHalf, stopLossPrice, StrategiesUtilities.SignalEntry_RSIBollingerHalf, DefaultQuantity, IsBuying, IsSelling);
             
                EnterOrderPure(priceToSet, targetFull, stopLossPrice, StrategiesUtilities.SignalEntry_RSIBollingerFull, DefaultQuantity, IsBuying, IsSelling);
            }
            catch (Exception ex)
            {
                LocalPrint($"[EnterOrder] - ERROR: " + ex.Message);
            }
        }

        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            base.OnOrderUpdate(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, comment);
        }

        private TradingStatus InternalTradingStatus = TradingStatus.Idle; 

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

                if (TradingStatus == TradingStatus.Idle)
                {
                    var shouldTrade = ShouldTrade();

                    LocalPrint($"Check trading condition, result: {shouldTrade}");

                    if (shouldTrade == RSIBollingerAction.WaitForSellSignal || shouldTrade == RSIBollingerAction.WaitForBuySignal)
                    {
                        InternalTradingStatus = TradingStatus.WatingForConfirmation;
                    }
                    else if (shouldTrade == RSIBollingerAction.SetBuyOrder || shouldTrade == RSIBollingerAction.SetSellOrder)
                    {
                        // Enter Order
                        EnterOrder(shouldTrade);                            
                    }
                }
                else if (TradingStatus == TradingStatus.PendingFill)
                {
                    // Kiểm tra các điều kiện để cancel lệnh
                    if (CurrentTradeAction == RSIBollingerAction.SetBuyOrder && Low[0] > bollinger1.Middle[0])
                    {
                        LocalPrint($"Price is greater than Bollinger middle, cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
                    }   
                    else if (CurrentTradeAction == RSIBollingerAction.SetSellOrder && High[0] < bollinger1.Middle[0])
                    {
                        LocalPrint($"Price is smaller than Bollinger middle, Cancel all pending orders");
                        // toàn bộ cây nến 5 phút đã vượt qua vùng giữa của Bollinger 
                        CancelAllPendingOrder();
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

        protected override bool IsFullPriceOrder(Cbi.Order order)
        {
            throw new NotImplementedException();
        }

        protected override double GetStopLossPrice(RSIBollingerAction tradeAction, double setPrice)
        {
            var stopLoss = StopLossInTicks * TickSize;
            
            return tradeAction == RSIBollingerAction.SetBuyOrder
                ? setPrice - stopLoss
                : setPrice + stopLoss;
        }

        protected override double GetSetPrice(RSIBollingerAction tradeAction)
        {
            // Always return openPrice_5m because current candle must be reverse candle
            return openPrice_5m;
        }

        protected override double GetTargetPrice_Half(RSIBollingerAction tradeAction, double setPrice)
        {
            var target1 = TickSize * Target1InTicks; 

            return tradeAction == RSIBollingerAction.SetBuyOrder
                ? setPrice + target1
                : setPrice - target1; 
        }

        protected override double GetTargetPrice_Full(RSIBollingerAction tradeAction, double setPrice)
        {
            var target2 = TickSize * Target2InTicks;

            return tradeAction == RSIBollingerAction.SetBuyOrder
                ? setPrice + target2
                : setPrice - target2;
        }

        protected override RSIBollingerAction ShouldTrade()
        {
            var time = ToTime(Time[0]);

            // Từ 3:30pm - 5:05pm thì không nên trade 
            if (time >= 153000 && time < 170500)
            {
                return RSIBollingerAction.NoTrade;
            }

            var rsi_5m = rsi.Value[0];

            if (InternalTradingStatus == TradingStatus.Idle)
            {
                // RSI > 70 
                // Cây nến có điểm cao nhất vượt qua Bollinger Band (Std=2)                
                if (rsi_5m > OverBoughtValue && High[0] > bollinger2.Upper[0])
                {
                    // Cây nến hiện tại là cây nến XANH 
                    if (Close[0] > Open[0] && Close[0] - Open[0] > MinimumCandleBody)
                    {
                        // Chờ đến ĐỎ
                        return RSIBollingerAction.WaitForSellSignal;
                    }
                    else
                    {
                        // Set lệnh ngay
                        return RSIBollingerAction.SetSellOrder;
                    }
                }
                else if (rsi_5m < OverSoldValue && Low[0] < bollinger2.Lower[0])
                {
                    // Cây nến hiện tại là cây nến ĐỎ
                    if (Open[0] > Open[0] && Open[0] - Close[0] > MinimumCandleBody)
                    {
                        // Chờ đến ĐỎ
                        return RSIBollingerAction.WaitForBuySignal;
                    }
                    else
                    {
                        // Wait for confirmation 
                        return RSIBollingerAction.SetBuyOrder;
                    }                   
                }
            }
            else if (InternalTradingStatus == TradingStatus.WatingForConfirmation)
            {
                if (CurrentTradeAction == RSIBollingerAction.WaitForSellSignal)
                {
                    // Cây nến hiện tại là cây nến XANH 
                    if (Close[0] > Open[0] && Close[0] - Open[0] > MinimumCandleBody)
                    {
                        // Chờ đến ĐỎ
                        return RSIBollingerAction.WaitForSellSignal;
                    }
                    else
                    {
                        // Set lệnh ngay
                        return RSIBollingerAction.SetSellOrder;
                    }
                }
                else if (CurrentTradeAction == RSIBollingerAction.WaitForBuySignal)
                {
                    // Cây nến hiện tại là cây nến ĐỎ
                    if (Open[0] > Open[0] && Open[0] - Close[0] > MinimumCandleBody)
                    {
                        // Chờ đến ĐỎ
                        return RSIBollingerAction.WaitForBuySignal;
                    }
                    else
                    {
                        // Wait for confirmation 
                        return RSIBollingerAction.SetBuyOrder;
                    }
                }
            }

            return RSIBollingerAction.NoTrade; 
        }
    }
}
