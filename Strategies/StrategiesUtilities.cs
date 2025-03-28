using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Serialization;

namespace NinjaTrader.Custom.Strategies
{
    public class StrategiesUtilities
    {
        public const string Configuration_StopLossTarget_Name = "Stoploss/Profit";
        public const string Configuration_DailyPnL_Name = "Daily PnL";
        public const string Configuration_Sizing_Name = "How to set stoploss/gain";
        public const string Configuration_General_Name = "General Setting";

        public const string Configuration_TigerParams_Name = "Based ATM Parameters";

        // Ninja Trader default signal 
        public const string StopLoss_SignalName = "Stop loss";
        public const string ProfitTarget_SignalName = "Profit target";

        // Chicken 
        public const string SignalEntry_ReversalHalf = "Entry-RH";
        public const string SignalEntry_ReversalFull = "Entry-RF";
        public const string SignalEntry_TrendingHalf = "Entry-TH";
        public const string SignalEntry_TrendingFull = "Entry-TF";

        // FVG 
        public const string SignalEntry_FVGHalf = "Entry-FH";
        public const string SignalEntry_FVGFull = "Entry-FF";

        // Vikki
        public const string SignalEntry_VikkiHalf = "Entry-VH";
        public const string SignalEntry_VikkiFull = "Entry-VF";

        //RSI-Bollinger 
        public const string SignalEntry_RSIBollingerHalf = "Entry-BH";
        public const string SignalEntry_RSIBollingerFull = "Entry-BF";

        // General
        public const string SignalEntry_GeneralHalf = "Entry-GH";
        public const string SignalEntry_GeneralFull = "Entry-GF";

        // Some default news time, being used for many places.
        public const string DefaultNewsTime = "0830,1500,1700";


        public static double RoundPrice(double price)
        {
            return Math.Round(price * 4, MidpointRounding.AwayFromZero) / 4.0;
        }

        /// <summary>
        ///  Check xem thời gian hiện tại có gần với thời gian có news không.
        /// </summary>
        /// <param name="time">Thời gian hiện tại</param>
        /// <param name="newsTime">News time</param>
        /// <returns></returns>
        public static bool NearNewsTime(int time, int newsTime)
        {
            // newsTime format: 0700,0830,1300
            var minute = newsTime % 100;
            var hour = newsTime / 100;

            var left = -1;
            var right = -1;

            if (minute >= 5 && minute <= 54) // time is 0806 --> no trade from 080100 to 081100
            {
                left = hour * 10000 + (minute - 5) * 100;
                right = hour * 10000 + (minute + 5) * 100;
            }
            else if (minute < 5) // time is 0802 --> no trade from 075700 to 080700
            {
                left = (hour - 1) * 10000 + (minute + 55) * 100;
                right = hour * 10000 + (minute + 5) * 100;
            }
            else // minute >= 55 - time is 0856 --> No trade from 085100 to 090100
            {
                left = hour * 10000 + (minute - 5) * 100;
                right = (hour + 1) * 10000 + (minute - 55) * 100;
            }

            return left < time && time < right;
        }

        /// <summary>
        /// Nếu đã đủ lợi nhuận hoặc đã bị thua lỗ quá nhiều thì dừng (bool reachDailyPnL, double totalPnL, bool isWinDay)
        /// </summary>
        /// <returns></returns>
        public static bool ReachMaxDayLossOrDayTarget(NinjaScriptBase owner, Account Account, int MaximumDailyLoss, int DailyTargetProfit)
        {
            // Calculate today's P&L
            double todaysPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

            var reachDayLimit = todaysPnL <= -MaximumDailyLoss || todaysPnL >= DailyTargetProfit;

            var additionalText = reachDayLimit ? ". DONE FOR TODAY." : "";

            var textColor = todaysPnL == 0 ? Brushes.Black : todaysPnL > 0 ? Brushes.Green : Brushes.Red;

            Draw.TextFixed(
                        owner,
                        "PnL",
                        $"PnL: {todaysPnL:C2}{additionalText}",
                        TextPosition.BottomRight,
                        textColor,            // Text color
                        new SimpleFont("Arial", 12), // Font and size
                        textColor,      // Background color
                        Brushes.Transparent,      // Outline color
                        0                         // Opacity (0 is fully transparent)
                    );

            return reachDayLimit;
        }

        /// <summary>
        /// Tính toán Profit và Loss, sau đó display ở góc dưới trái màn hình
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="account"></param>
        /// <param name="action"></param>
        public static void CalculatePnL(NinjaScriptBase owner, Account account, Action<string> action)
        {
            try
            {
                var profitloss = account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

                Draw.TextFixed(
                        owner,
                        $"Running with {owner}",
                        $"Acc: {account.Name} - {account.Get(AccountItem.NetLiquidation, Currency.UsDollar):C2}",
                        TextPosition.BottomLeft,
                        Brushes.DarkBlue,            // Text color
                        new SimpleFont("Arial", 12), // Font and size
                        Brushes.DarkBlue,      // Background color
                        Brushes.Transparent,      // Outline color
                        0                         // Opacity (0 is fully transparent)
                    );
            }
            catch (Exception e)
            {
                action(e.Message);
            }
        }


        public static bool IsCorrectShift(int time, ShiftType shiftType, Action<string> action)
        {
            if (shiftType == ShiftType.Moning_0700_1500 && (time < 070000 || time > 150000))
            {
                action($"Time: {time} - Shift {shiftType} --> Not trading hour");
                return false;
            }
            else if (shiftType == ShiftType.Afternoon_1700_2300 && (time < 170000 || time > 230000))
            {
                action($"Time: {time} - Shift {shiftType} --> Not trading hour");
                return false;
            }
            else if (shiftType == ShiftType.Night_2300_0700 && (time >= 070000 && time <= 230000))
            {
                action($"Time: {time} - Shift {shiftType} --> Not trading hour");
                return false;
            }

            return true;
        }

        public static HashSet<string> SignalEntries = new HashSet<string>
        {
            // Chicken 
            SignalEntry_ReversalHalf,
            SignalEntry_ReversalFull,
            SignalEntry_TrendingHalf,
            SignalEntry_TrendingFull,

            // FVG 
            SignalEntry_FVGHalf,
            SignalEntry_FVGFull, 

            //RSI-Bollinger 
            SignalEntry_RSIBollingerHalf,
            SignalEntry_RSIBollingerFull, 

            SignalEntry_GeneralHalf,
            SignalEntry_GeneralFull,
        };

        public static string GenerateKey(Order order)
        {
            // Order là Entry thì dùng Name làm Key
            if (SignalEntries.Any(signal => signal == order.Name))
            {
                return order.Name;
            }
            // Order không phải entry --> Name sẽ có dạng "Stop loss" hoặc "Profit target"
            else if (order.Name == StopLoss_SignalName || order.Name == ProfitTarget_SignalName)
            {
                // Back test data, không có Id
                if (order.Id == -1)
                {
                    return $"{order.Name}-{order.FromEntrySignal}";
                }
                else
                {
                    return $"{order.Id}";
                }
            }
            return $"{order.Name}-{order.FromEntrySignal}-{order.Id}";
        }

        public static string ATMFolderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8\\templates\\AtmStrategy");
        public static NinjaTraderConfig ReadStrategyData(string strategyName, Action<string> action = null)
        {
            var xmlFilePath = $"{ATMFolderName}\\{strategyName}.xml";

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(NinjaTraderConfig));
                using (StreamReader reader = new StreamReader(xmlFilePath))
                {
                    return (NinjaTraderConfig)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                if (action != null)
                {
                    action(ex.Message);
                }
            }
            return null;
        }
    }

    public class SimpleInfoOrder
    {
        public string Name { get; set; }

        public string FromEntrySignal { get; set; }
    }

    public class OrderDetail
    {
        public double Price { get; set; }

        public OrderAction Action { get; set; }

        public int StopLossInTicks_1 { get; set; }

        public int StopLossInTicks_2 { get; set; }

        public int TargetProfitInTicks_1 { get; set; }

        public int TargetProfitInTicks_2 { get; set; }

        public int Quantity { get; set; }
    }

    public class FVGTradeDetail
    {
        public GeneralTradeAction FVGTradeAction { get; set; }

        public double FilledPrice { get; set; }

        /// <summary>
        /// Giá trị stop loss theo FVG
        /// </summary>
        public double StopLossPrice { get; set; }

        public double TargetProfitPrice { get; set; }

        public double BarIndex { get; set; }

        private int _noOfContracts = 0;
        public int NoOfContracts
        {
            get
            {
                if (_noOfContracts == 0)
                {
                    _noOfContracts = (int)Math.Round(120 / Math.Abs(FilledPrice - StopLossPrice));
                    if (_noOfContracts == 0)
                    {
                        _noOfContracts = 1;
                    }
                }
                return _noOfContracts;
            }
        }

        private double? stopLossDistance;

        /// <summary>
        /// Khoảng cách từ gap điểm filled gap đến đỉnh/đáy của cây nến thứ 1
        /// </summary>
        public double StopLossDistance
        {
            get
            {
                if (stopLossDistance == null)
                {
                    stopLossDistance = Math.Abs(FilledPrice - StopLossPrice);
                }
                return stopLossDistance.Value;
            }
        }

        private double? targetProfitDistance;

        /// <summary>
        /// Khoảng cách từ gap điểm filled gap đến đỉnh/đáy của cây nến thứ 3
        /// </summary>
        public double TargetProfitDistance
        {
            get
            {
                if (targetProfitDistance == null)
                {
                    targetProfitDistance = Math.Abs(FilledPrice - TargetProfitPrice);
                }
                return targetProfitDistance.Value;
            }
        }
    }

    public class RSITradeDetail
    { 

    }
}
