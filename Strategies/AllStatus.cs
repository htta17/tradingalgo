using System;
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
    public enum PlaceToSetOrder
    {       
        EMA2951,
        BollingerBand
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
        Idle, // Đang không có lệnh 
        PendingFill, // Lệnh đã submit nhưng chưa được fill do giá chưa đúng
        WatingForConfirmation,
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

    /// <summary>
    /// Waddah Attar Explosion
    /// </summary>
    public class WAE_ValueSet
    {
        // Là hệ số đảm bảo cho UpTrendVal hoặc DownTrendVal phải lớn hơn [DeadZoneVal] *  [SafetyRatio]
        public const double SafetyRatio = 1.4;
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
    }

    public enum Trends
    {
        Unknown,
        Bullish,
        Bearish
    }
    #endregion    

    #region Monkey (FVG) enums 

    public enum FVGTradeAction
    {
        NoTrade =0,

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
    public enum RSIBollingerAction
    {
        /// <summary>
        /// Không làm gì 
        /// </summary>
        NoTrade,

        Buy, 

        Sell
    }


    #endregion
}
