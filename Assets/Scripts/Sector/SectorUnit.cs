using UnityEngine;
using TMPro;

public class SectorUnit:MonoBehaviour
{
    [SerializeField] private int id;
    [SerializeField] private TMP_Text priceTMP;
    [SerializeField] private TMP_Text percentageTMP;
    private StockPrice stockPrice;
    private NextDayEvent nextDayEvent;
    private GlobalStatus globalStatus;
    public void Awake()
    {
        stockPrice = StockPrice.getInstance();
        nextDayEvent = NextDayEvent.GetInstance();
        globalStatus = GlobalStatus.GetInstance();
    }
    public void Start()
    {
        updateBar();
    }

    private void OnEnable()
    {
        nextDayEvent.nextDay += updateBar;
    }
    private void OnDisable()
    {
        nextDayEvent.nextDay -= updateBar;
    }
    public void updateBar()
    {
        float[] bar = stockPrice.getBar();
        // 指定セクターの最新のCloseデータを取得する。
        float price = bar[3248 + id * 5];
        priceTMP.SetText($"{price:F0}");
        // 指定セクターの一つ前のCloseデータを取得する。
        float forwardPrice = bar[3193 + id * 5];
        float percentage = (price - forwardPrice) / forwardPrice * 100;
        percentageTMP.SetText($"{percentage:F2}%");
    }
    // sectorのPanelが押されたときの処理
    public void OnButton()
    {
        globalStatus.SectorId = id;
    }
}