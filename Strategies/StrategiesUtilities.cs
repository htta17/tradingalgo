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
    }
}
