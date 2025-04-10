﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    #region Chicken enums
    /// <summary>
    /// Lựa chọn điểm vào lệnh (Theo EMA29/51 hay theo Bollinger bands)
    /// </summary>
    public enum ReversePlaceToSetOrder
    {
        EMA2951,
        BollingerBand
    }

    public enum TrendPlaceToSetOrder
    {
        /// <summary>
        /// EMA 21 khung 1 phút 
        /// </summary>
        EMA21,

        /// <summary>
        /// (EMA29 + EMA51)/2 khung 1 phút 
        /// </summary>
        EMA2951Average,

        /// <summary>
        /// Điểm giữa của cây nến trước
        /// </summary>
        MiddleOfLastCandle,

        BollingerBandHigh,

        BollingerBandLow
    }

    /// <summary>
    /// Hành động gì tiếp theo đây? Trade theo xu hướng, ngược xu hướng, không trade, etc.
    /// </summary>
    public enum TradeAction
    {
        NoTrade = 0,

        /// <summary>
        /// Đặt lệnh bán tại upper Bollinger (5m) band hoặc EMA29/51 (1m) tùy vào setting
        /// </summary>
        Sell_Reversal = 1,

        /// <summary>
        /// Đặt lệnh bán tại upper Bollinger (5m) band hoặc EMA29/51 (1m) tùy vào setting
        /// </summary>
        Buy_Reversal = 2,

        /// <summary>
        /// Đặt lệnh bán tại EMA29/51
        /// </summary>
        Sell_Trending = 3,

        /// <summary>
        /// Đặt lệnh mua tại EMA29/51
        /// </summary>
        Buy_Trending = 4,
    }
    #endregion

    #region Shared enums
    /// <summary>
    /// Trạng thái hiện tại của giải thuật
    /// </summary>
    public enum TradingStatus
    {
        /// <summary>
        /// Trạng thái cơ bản, không làm gì. 
        /// </summary>
        Idle,

        /// <summary>
        /// Lệnh đã submit nhưng chưa được fill
        /// </summary>
        PendingFill,

        /// <summary>
        /// Đợi đủ điều kiện để vào lệnh 
        /// </summary>
        WatingForCondition,

        /// <summary>
        /// Lệnh đã được filled.
        /// </summary>
        OrderExists  // Lệnh đã được filled 
    }

    /// <summary>
    /// Shift: Ca ngày, ca chiều tối, ca đêm
    /// </summary>
    public enum ShiftType
    {
        Moning_0700_1500,
        Afternoon_1700_2300,
        Night_2300_0700
    }

    public enum WAE_Strength
    {
        /// <summary>
        /// &lt; 150
        /// </summary>
        SuperWeak,

        /// <summary>
        /// Range from 150 - 350
        /// </summary>
        Weak,

        /// <summary>
        /// Range from 350 - 600
        /// </summary>
        Medium,

        /// <summary>
        /// Range from 600 - 800
        /// </summary>
        MediumStrong,

        /// <summary>
        /// Range from 800 - 1100
        /// </summary>
        Strong,

        /// <summary>
        /// > 1100
        /// </summary>
        SuperStrong
    }

    /// <summary>
    /// Waddah Attar Explosion
    /// </summary>
    public class WAE_ValueSet
    {
        // Là hệ số đảm bảo cho UpTrendVal hoặc DownTrendVal phải lớn hơn [DeadZoneVal] *  [SafetyRatio]
        public const double SafetyRatio = 1.4;

        /// <summary>
        /// Volume ≤ [WeakRange]: Weak
        /// </summary>
        public const int SuperWeakRange = 150;

        /// <summary>
        /// [WeakRange] &lt; Volume ≤ [MediumRange]: Medium
        /// </summary>
        public const int WeakRange = 350;

        /// <summary>
        /// [MediumRange] &lt; Volume ≤ [StrongRange]: Strong <br/>
        /// [StrongRange] &lt; Volume: SuperStrong &lt; [ExtremelySuperStrong]
        /// </summary>
        public const int MediumRange = 600;

        /// <summary>
        /// [MediumRange] &lt; Volume ≤ [StrongRange]: Strong <br/>
        /// [StrongRange] &lt; Volume: SuperStrong
        /// </summary>
        public const int MediumStrongRange = 800;

        /// <summary>
        /// [MediumRange] &lt; Volume ≤ [StrongRange]: Strong <br/>
        /// [StrongRange] &lt; Volume: SuperStrong
        /// </summary>
        public const int StrongRange = 1100;

        public double DeadZoneVal { get; set; }
        public double ExplosionVal { get; set; }
        public double UpTrendVal { get; set; }
        public double DownTrendVal { get; set; }

        private bool? hasBullVolume = null;

        /// <summary>
        /// Định nghĩa điều kiện để có bear volume. Điều kiện hiện tại: UpTrendVal > DeadZoneVal * SafetyRatio
        /// </summary>
        public bool HasBULLVolume
        {
            get
            {
                if (hasBullVolume == null)
                {
                    hasBullVolume = UpTrendVal > DeadZoneVal * SafetyRatio;
                }
                return hasBullVolume.Value;
            }
        }

        private bool? hasBearVolume = null;

        /// <summary>
        /// Định nghĩa điều kiện để có bear volume.  Điều kiện hiện tại: DownTrendVal > DeadZoneVal * SafetyRatio
        /// </summary>
        public bool HasBEARVolume
        {
            get
            {
                if (hasBearVolume == null)
                {
                    hasBearVolume = DownTrendVal > DeadZoneVal * SafetyRatio;
                }
                return hasBearVolume.Value;
            }
        }

        private bool? inDeadZone = null;
        public bool IsInDeadZone
        {
            get
            {
                if (inDeadZone == null)
                {
                    inDeadZone = DeadZoneVal > UpTrendVal && DeadZoneVal > DownTrendVal;
                }
                return inDeadZone.Value;
            }
        }

        private WAE_Strength? wAE_Strength = null;
        public WAE_Strength WAE_Strength
        {
            get
            {
                if (wAE_Strength == null)
                {
                    var sum = DownTrendVal + UpTrendVal;

                    if (sum <= SuperWeakRange)
                    {
                        wAE_Strength = WAE_Strength.SuperWeak;
                    }
                    else if (SuperWeakRange < sum && sum <= WeakRange)
                    {
                        wAE_Strength = WAE_Strength.Weak;
                    }
                    else if (WeakRange < sum && sum <= MediumRange)
                    {
                        wAE_Strength = WAE_Strength.Medium;
                    }
                    else if (MediumRange < sum && sum <= MediumStrongRange)
                    {
                        wAE_Strength = WAE_Strength.MediumStrong;
                    }
                    else if (MediumStrongRange < sum && sum <= StrongRange)
                    {
                        wAE_Strength = WAE_Strength.Strong;
                    }
                    else 
                    {
                        wAE_Strength = WAE_Strength.SuperStrong;
                    }
                }
                return wAE_Strength.Value;
            }
        }
    }

    public enum Trends
    {
        Unknown,
        Bullish,
        Bearish
    }
    #endregion    

    #region Monkey (FVG) enums 

    public enum GeneralTradeAction
    {
        NoTrade = 0,

        // Start from 5, do [TradeAction] đã có từ 1-4
        Buy = 5,

        Sell = 6,
    }

    /// <summary>
    /// Dùng cho FVG (Tiger)
    /// </summary>
    public enum FVGWayToSetStopLoss
    {
        /// <summary>
        /// Stop loss/gain dựa trên số lượng [Target 1/2 Profit (Ticks)] và [Số lượng contract cho target 1/2]
        /// </summary>
        FixedNumberOfTicks,

        /// <summary>
        /// Chương trình sẽ tính toán và quyết định stop loss/gain dựa trên FVG gap
        /// </summary>
        BasedOnFVGGap,
    }
    #endregion

    #region Tiger (RSI + Bollinger reverse) 
    public enum ADXBollingerAction
    {
        /// <summary>
        /// Không làm gì 
        /// </summary>
        NoTrade,

        SetBuyOrder,

        SetSellOrder
    }

    [Flags]
    public enum ValidateType
    {
        TradingHour = 1,
        MaxDayGainLoss = 2,
    }

    public class NewsTimeReader
    {
        public string Sunday { get; set; }
        public string Monday { get; set; }
        public string Tuesday { get; set; }
        public string Wednesday { get; set; }
        public string Thursday { get; set; }
        public string Friday { get; set; }

        // No-work on Sabbath day, praise Jehovah.
    }
    #endregion

    public enum TimeFrameToTrade
    { 
        OneMinute, 
        ThreeMinutes, 
        FiveMinutes,
        FifteenMinutes
    }
}
