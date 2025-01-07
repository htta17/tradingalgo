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

    
}
