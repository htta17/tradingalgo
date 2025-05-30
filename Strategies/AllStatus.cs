﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

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

    public enum ImplementedAlgorithm
    {
        FVG,
        Kitty,
        Rooster
    }

    /// <summary>
    /// Waddah Attar Explosion
    /// </summary>
    public class WAE_ValueSet
    {
        public WAE_ValueSet() : this(ImplementedAlgorithm.Rooster)
        {
        }
        /// <summary>
        /// Khởi tạo theo các Algorithm khác nhau để có [SafetyRatio] khác nhau
        /// </summary>
        /// <param name="algorithm">Mặc định là Rooster</param>
        public WAE_ValueSet(ImplementedAlgorithm algorithm)
        {
            if (algorithm == ImplementedAlgorithm.Kitty)
            {
                SafetyRatio = 1.0;
            }
            else
            {
                SafetyRatio = 1.4;
            }
        }
        // Là hệ số đảm bảo cho UpTrendVal hoặc DownTrendVal phải lớn hơn [DeadZoneVal] *  [SafetyRatio]
        private double SafetyRatio { get; set; }

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

    /// <summary>
    /// Vị trí của nến so với (các) đường EMA(s) <br/>
    /// 
    /// </summary>
    public enum GeneralEMAsPosition
    {
        Unknown,
        Above,
        Below,
        Crossing
    }
    public class EMA2129Status
    {
        public EMA2129Status()
        {
            Position = GeneralEMAsPosition.Unknown;

            ResetEnteredOrder();

            ResetCounters();
        }

        public void ResetEnteredOrder()
        {
            SetAt_EMA21 = false;
            SetAt_EMA29 = false;
            SetAt_EMA10_5m = false;
            SetAt_EMA10_5m = false;
        }
        public GeneralEMAsPosition Position { get; private set; }

        /// <summary>
        /// Dùng để đánh dấu đã enter order hay chưa <br/>
        /// Có 2 điểm đặt lệnh (EMA21/29) <br/>
        /// Chỉ tính cho EMA21
        /// </summary>
        public bool EnteredOrder21
        {
            get
            {
                return SetAt_EMA21;
            }
        }

        /// <summary>
        /// Dùng để đánh dấu đã enter order hay chưa <br/>
        /// Có 2 điểm đặt lệnh (EMA21/29) <br/>
        /// Chỉ tính cho EMA21
        /// </summary>
        public bool EnteredOrder29
        {
            get
            {
                return SetAt_EMA29;
            }
        }

        public void SetPosition(GeneralEMAsPosition position, int? barIndex = null, bool resetEnterOrder = false)
        {
            Position = position;

            if (resetEnterOrder && (position == GeneralEMAsPosition.Below || position == GeneralEMAsPosition.Above))
            {
                ResetEnteredOrder();

                ResetCounters();
            }
        }

        /// <summary>
        /// Nếu đã vào lệnh rồi thì mark EnteredOrder = true
        /// </summary>
        public void SetEnteredOrder(EMA2129OrderPostition postition = EMA2129OrderPostition.EMA21)
        {
            //EnteredOrder = true;
            if (postition == EMA2129OrderPostition.EMA21)
            {
                SetAt_EMA21 = true;
            }
            else if (postition == EMA2129OrderPostition.EMA29)
            {
                SetAt_EMA29 = true;
            }
            else if (postition == EMA2129OrderPostition.EMA10_5m)
            {
                SetAt_EMA10_5m = true;
            }
        }

        public void ResetCounters()
        {
            CountTouch_EMA21 = 0;
            CountTouch_EMA29 = 0;
            CountTouch_EMA10_5m = 0;
        }

        public int CountTouch_EMA21 { get; set; }
        public int CountTouch_EMA29 { get; set; }
        public int CountTouch_EMA10_5m { get; set; }

        /// <summary>
        /// Set order ở EMA21 +/- Adjust
        /// </summary>
        private bool SetAt_EMA21 { get; set; }

        /// <summary>
        /// Set order ở EMA29 +/- Adjust
        /// </summary>
        private bool SetAt_EMA29 { get; set; }

        private bool SetAt_EMA10_5m { get; set; }

        private bool SetAt_Adjusted_EMA29 { get; set; }


        /// <summary>
        /// Khi có cây nến chạm vào đường nào thì count touch lên 1
        /// </summary>
        /// <param name="position"></param>
        public void Touch(EMA2129OrderPostition position, int? barIndex = null)
        {
            if (position == EMA2129OrderPostition.AdjustedEMA21)
            {
                // Vị trí này không có touch LoL
            }
            else if (position == EMA2129OrderPostition.EMA21)
            {
                CountTouch_EMA21++;
            }
            else if (position == EMA2129OrderPostition.EMA29)
            {
                CountTouch_EMA29++;
            }
            else if (position == EMA2129OrderPostition.EMA10_5m)
            {
                CountTouch_EMA10_5m++;
            }
        }
    }

    public enum TradeSizingEnum
    {
        Small,
        Medium,
        Big
    }

    public enum EMA2129OrderPostition
    {
        NoTrade = 0,

        /// <summary>
        /// EMA21 có +/- Adjustment
        /// </summary>
        AdjustedEMA21,

        /// <summary>
        /// EMA 21 khung 1 phút
        /// </summary>
        EMA21,

        /// <summary>
        /// EMA 29 khung 1 phút
        /// </summary>        
        EMA29,

        /// <summary>
        /// EMA 10 khung 5 phút
        /// </summary>
        EMA10_5m
    }

    public class EMA2129OrderDetail : TradeOrderDetail
    {
        public EMA2129OrderDetail()
        {
            Postition = EMA2129OrderPostition.NoTrade;
        }

        public EMA2129OrderPostition Postition { get; set; }
    }

    public enum TradeDirection
    {
        Reverse,

        TrendFollowing
    }

    public class TradeOrderDetail
    {
        public GeneralTradeAction Action { get; set; }
        public TradeSizingEnum Sizing { get; set; }
    }

    public class FishTrendTradeDetail : TradeOrderDetail
    { 
        public TradeDirection Direction { get; set; }
    }

    public class AtmSavedInfo
    { 
        public string OrderId { get; set; }

        public string AtmStrategyId { get; set; }
    }
}
