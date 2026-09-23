using System;

public class GlobalStatus
{
    private static GlobalStatus globalStatus;
    private int sectorId;
    public int funds; // 買付余力
    public event Action IdChanged;
    public StockHoldings[] StockHoldings = new StockHoldings[11];
    public static GlobalStatus GetInstance()
    {
        if(globalStatus == null)
        {
            globalStatus = new GlobalStatus();
            for(int i = 0; i < globalStatus.StockHoldings.Length; i++)
            {
                globalStatus.StockHoldings[i] = new StockHoldings();
            }
        }
        return globalStatus;
    }
    public int SectorId
    {
        get => sectorId;
        set
        {
            if(sectorId == value)
            {
                return;
            }
            sectorId = value;
            IdChanged?.Invoke();
        }
    }
}