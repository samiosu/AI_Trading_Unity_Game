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
            // 購入可能条件
            if(globalStatus.funds >= amount.StockAmount * pricePerUnit)
            {
                globalStatus.StockHoldings[globalStatus.SectorId].ChangeStockHoldings(amount.StockAmount, pricePerUnit);
                globalStatus.funds -= (int)(amount.StockAmount * pricePerUnit);
            }
            else
            {
                Debug.Log("購入不可");
            }
        }
        else
        {
            // 売却可能条件
            if(amount.StockAmount <= globalStatus.StockHoldings[globalStatus.SectorId].Amount)
            {
                globalStatus.StockHoldings[globalStatus.SectorId].ChangeStockHoldings(-amount.StockAmount, pricePerUnit);
                globalStatus.funds += (int)(amount.StockAmount * pricePerUnit);
            }
            else
            {
                Debug.Log("売却不可");
            }
        }            
        amount.ResetAmount();
        sectorHoldUI.UpdateUI();
    }
}