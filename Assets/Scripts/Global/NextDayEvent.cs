using System;
using System.Diagnostics;

public class NextDayEvent
{
    public event Action nextDay;
    private static NextDayEvent nextDayEvent;

    public static NextDayEvent GetInstance()
    {
        if(nextDayEvent == null)
        {
            nextDayEvent = new NextDayEvent();
        }
        return nextDayEvent;
    }
    
    public void DoThing()
    {
        nextDay?.Invoke();
        UnityEngine.Debug.Log("a");
    }
}