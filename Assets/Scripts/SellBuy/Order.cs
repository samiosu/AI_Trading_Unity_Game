using UnityEngine;

public class Order : MonoBehaviour
{
    [SerializeField] private bool isBuy;
    [SerializeField] private Amount amount;
    [SerializeField] private SectorHoldUI sectorHoldUI;
    private GlobalStatus globalStatus;
    private StockPrice stockPrice;

    
    public void Awake()
    {
        globalStatus = GlobalStatus.GetInstance();
        stockPrice = StockPrice.getInstance();
    }

    public void OnButton()
    {
        float[] bar = stockPrice.getBar();
        // 指定セクターの最新のCloseデータを取得する。
        float pricePerUnit = bar[3248 + globalStatus.SectorId * 5];
        if (isBuy)
        {
        globalStatus.StockHoldings[globalStatus.SectorId].ChangeStockHoldings(amount.StockAmount, pricePerUnit);
            globalStatus.funds -= (int)(amount.StockAmount * pricePerUnit);
        }
        else
        {
        globalStatus.StockHoldings[globalStatus.SectorId].ChangeStockHoldings(-amount.StockAmount, pricePerUnit);
            globalStatus.funds += (int)(amount.StockAmount * pricePerUnit);
        }            
        amount.ResetAmount();
        sectorHoldUI.UpdateUI();
    }
}