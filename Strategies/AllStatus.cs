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
    public enum WayToTrade
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

    /// <summary>
    /// Hành động gì tiếp theo đây? Trade theo xu hướng, ngược xu hướng, không trade, etc.
    /// </summary>
    public enum TradeAction
    {         
        NoTrade,

        /// <summary>
        /// Đặt lệnh bán tại upper Bollinger (5m) band hoặc EMA29/51 (1m) tùy vào setting
        /// </summary>
        Sell_Reversal,

        /// <summary>
        /// Đặt lệnh bán tại upper Bollinger (5m) band hoặc EMA29/51 (1m) tùy vào setting
        /// </summary>
        Buy_Reversal, 

        /// <summary>
        /// Đặt lệnh bán tại EMA29/51
        /// </summary>
        Sell_Trending,

        /// <summary>
        /// Đặt lệnh mua tại EMA29/51
        /// </summary>
        Buy_Trending,
    }    

    public class WEA_ValueSet
    { 
        public double DeadZoneVal { get; set; }
        public double ExplosionVal { get; set; }

        public double UpTrendVal { get; set; }

        public double DownTrendVal { get; set; }

        private bool? _canTrade = null;
        public bool CanTrade 
        { 
            get 
            {
                if (_canTrade == null)
                {
                    _canTrade = (UpTrendVal > ExplosionVal && UpTrendVal > DeadZoneVal) || (DownTrendVal > ExplosionVal && DownTrendVal > DeadZoneVal);
                }
                return _canTrade.Value;
            }  
        }
    }

    public enum Trends
    {
        Unknown,
        Bullish, 
        Bearish
    }
    
}
