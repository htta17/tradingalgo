﻿using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NinjaTrader.Custom.Strategies
{
    public class StrategiesUtilities
    {
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
        /// <param name="Account"></param>
        /// <param name="action"></param>
        public static void CalculatePnL(NinjaScriptBase owner, Account Account, Action<string> action)
        {
            try
            {
                var profitloss = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);

                Draw.TextFixed(
                        owner,
                        "RunningAcc",
                        $"Acc: {Account.Name} - {Account.Get(AccountItem.NetLiquidation, Currency.UsDollar):C2}",
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

        public const string StopLoss_SignalName = "Stop loss";
        public const string ProfitTarget_SignalName = "Profit target";

        public const string SignalEntry_ReversalHalf = "Entry-RH";
        public const string SignalEntry_ReversalFull = "Entry-RF";
        public const string SignalEntry_TrendingHalf = "Entry-TH";
        public const string SignalEntry_TrendingFull = "Entry-TF";

        public static HashSet<string> SignalEntries = new HashSet<string>
        {
            SignalEntry_ReversalHalf,
            SignalEntry_ReversalFull,
            SignalEntry_TrendingHalf,
            SignalEntry_TrendingFull
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

        public static bool IsHalfPriceOrder(Order order)
        {
            return order.Name == SignalEntry_ReversalHalf || order.Name == SignalEntry_TrendingHalf;
        }
        public static bool IsFullPriceOrder(Order order)
        {
            return order.Name == SignalEntry_ReversalFull || order.Name == SignalEntry_TrendingFull;
        }
    }

    public class SimpleInfoOrder
    { 
        public string Name { get; set; }

        public string FromEntrySignal { get; set; }
    }
}
