using UnityEngine;
using AITrading.AI;

// Singleton
public class StockPrice
{
    [SerializeField] private StockMarketOnnxRunner market;
    // 60日 * 55(11セクター * 5情報) の１次元データを受け取る。
    private float[] bar;
    private float[,,] sectorBar = new float[11,60,5];

    private static StockPrice stockPrice;



    public static StockPrice getInstance()
    {
        if(stockPrice == null)
        {
            stockPrice = new StockPrice();
        }
        return stockPrice;        
    }
    public void setMarket(StockMarketOnnxRunner market)
    {
        this.market = market;        
    }

    public void destroyInstance()
    {
        stockPrice = null;
    }

    private void setSectorBar()
    {
        for(int i = 0; i < sectorBar.GetLength(0); i++)
        {
            for(int j = 0; j < sectorBar.GetLength(1); j++)
            {
                for(int k = 0; k < sectorBar.GetLength(2); k++)
                {
                    // rawWindowは「日付 -> セクター -> OHLCV」の順で並ぶ。
                    int sourceIndex =
                        j * StockMarketMetadata.FeatureSize +
                        i * StockMarketMetadata.FieldCount +
                        k;
                    sectorBar[i,j,k] = bar[sourceIndex];
                }
            }
        }
    }

    public void changeBarAndSectorBar()
    {
        if (market == null)
        {
            Debug.LogError("StockMarketOnnxRunnerをStockPriceのInspectorに設定してください。");
            return;
        }

        // GetRawWindow()は必要ならランナーを初期化し、コピーを返す。
        bar = market.GetRawWindow();
        if (bar == null || bar.Length != StockMarketMetadata.WindowSize)
        {
            Debug.LogError($"初期窓が未準備、または長さが不正です。期待値={StockMarketMetadata.WindowSize}");
            return;
        }

        setSectorBar();
    }

    public float[,] getSectorBar(int visibleCandleCount)
    {
        float[,] ans = new float[visibleCandleCount,5];
        int offset = sectorBar.GetLength(1) - visibleCandleCount;
        for(int i = 0; i < visibleCandleCount; i++)
        {
            for(int j = 0; j < sectorBar.GetLength(2); j++)
            {
                ans[i,j] = sectorBar[GlobalStatus.SectorId, offset + i, j];
            }
            
        }
        return ans;
    }
    public float[] getBar()
    {
        return bar;
    }
}
