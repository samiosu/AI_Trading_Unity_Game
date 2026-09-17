using System;

public class GlobalStatus
{
    private static GlobalStatus globalStatus;
    private int sectorId;
    public event Action IdChanged;
    public static GlobalStatus GetInstance()
    {
        if(globalStatus == null)
        {
            globalStatus = new GlobalStatus();
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