using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    /// <summary>
    /// Lựa chọn điểm vào lệnh (Theo EMA29/51 hay theo Bollinger bands)
    /// </summary>
    public enum PlaceToSetOrder
    {       
        EMA2951,
        BollingerBand
    }

    /// <summary>
    /// Trạng thái hiện tại của giải thuật
    /// </summary>
    public enum TradingStatus
    {
        Idle, // Đang không có lệnh 
        PendingFill, // Lệnh đã submit nhưng chưa được fill do giá chưa đúng
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
    public enum DuckStatus
    {
        Idle,
        WaitingForGoodPrice, // Có tín hiệu B-line nhưng giá vẫn chưa pass EMA29/51 
        FillOrderPending,
        OrderExist
    }

    /// <summary>
    /// Lựa chọn để vào lệnh: Vào theo ATM cố định, vào theo bollinger bands
    /// </summary>
    public enum LossGainStrategy
    {
        /// <summary>
        /// Dùng ATM để vào lệnh, stop loss/gain dựa theo ATM (Default_MNQ, Half_MNQ) 
        /// </summary>
        ChooseATM, 

        /// <summary>
        /// Tính toán khoảng cách của BB (std1 và std2) để quyết định sizing và stop loss/gain
        /// </summary>
        BasedOnBollinger,
    }

    

    public class WAE_ValueSet
    { 
        public double DeadZoneVal { get; set; }
        public double ExplosionVal { get; set; }
        public double UpTrendVal { get; set; }
        public double DownTrendVal { get; set; }

        private bool? hasBullVolume = null;

        /// <summary>
        /// Định nghĩa điều kiện để có bear volume. Điều kiện hiện tại: UpTrendVal > DeadZoneVal
        /// </summary>
        public bool HasBULLVolume
        { 
            get 
            {
                if (hasBullVolume == null)
                {
                    hasBullVolume = UpTrendVal > DeadZoneVal;
                }
                return hasBullVolume.Value;
            }  
        }

        private bool? hasBearVolume = null;

        /// <summary>
        /// Định nghĩa điều kiện để có bear volume.  Điều kiện hiện tại: DownTrendVal > DeadZoneVal
        /// </summary>
        public bool HasBEARVolume
        {
            get
            {
                if (hasBearVolume == null)
                {
                    hasBearVolume = DownTrendVal > DeadZoneVal;
                }
                return hasBearVolume.Value;
            }
        }
    }

    public enum Trends
    {
        Unknown,
        Bullish, 
        Bearish
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

    public enum FVGTradeAction
    {
        NoTrade =0,

        // Start from 5, do [TradeAction] đã có từ 1-4
        Buy = 5, 

        Sell = 6,
    }
    
    
}
