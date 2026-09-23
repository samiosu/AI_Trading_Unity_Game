using Microsoft.CodeAnalysis.CSharp.Syntax;

public class StockHoldings
{
    public int Amount { get; private set; }
    public int CostPerUnit { get; private set; }
    public int Price { get; private set; }
    public int ProfitAndLoss { get; private set; }
    public float ProfitAndLossRation { get; private set; }

    public StockHoldings()
    {
        Amount = 0;
        CostPerUnit = 0;
        Price = 0;
        ProfitAndLoss = 0;
        ProfitAndLossRation = 0;
    }

    // 新規購入時・売却時に変化させるデータ
    public void ChangeStockHoldings(int amount, float costPerUnit)
    {
        if(Amount + amount == 0)
        {
            Amount = 0;
            CostPerUnit = 0;
        }
        // 既に保有分がある
        else if(Amount != 0)
        {
            CostPerUnit = (int)(Amount * CostPerUnit + amount * costPerUnit) / (Amount + amount);
            Amount += amount;
        }
        else
        {
            Amount = amount;
            CostPerUnit = (int)costPerUnit;
        }
        if(Amount == 0)
        {
            CostPerUnit = 0;
        }
        ChangeValue();   
    }

    // 株価の更新があるたびに変化させるデータ
    public void ChangeValue()
    {
        StockPrice stockPrice = StockPrice.getInstance();
        GlobalStatus globalStatus = GlobalStatus.GetInstance();
        float[] bar = stockPrice.getBar();
        // 指定セクターの最新のCloseデータを取得する。
        float pricePerUnit = bar[3248 + globalStatus.SectorId * 5];
        if(Amount != 0)
        {
            Price = (int)(Amount * pricePerUnit);
            ProfitAndLoss = Price - Amount * CostPerUnit;
            ProfitAndLossRation = (pricePerUnit - CostPerUnit) / CostPerUnit * 100;
        }
        else
        {
            Price = 0;
            ProfitAndLoss = 0;
            ProfitAndLossRation = 0;
        }
    }
}