using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.Strategies
{
    public enum ChickenWayToTrade
    {       
        EMA2951,
        BollingerBand
    }

    public enum ChickenStatus
    {
        Idle, // Đang không có lệnh 
        PendingFill, // Lệnh đã submit nhưng chưa được fill do giá chưa đúng
        OrderExists  // Lệnh đã được filled 
    }

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
        FillOrderPendingDuck,
        FillOrderPendingTrending,
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
        ChooseBollinger,
    }


}
