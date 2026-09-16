using AITrading.AI;
using UnityEngine;

public class MarketController : MonoBehaviour
{
    [SerializeField] private StockMarketOnnxRunner market;

    // ゲームの「次の日」処理などから呼び出す。
    public float[] AdvanceDay(int sectorIndex)
    {
        float[] bar = market.GenerateNextBar();
        int offset = sectorIndex * 5;
        float open = bar[offset];
        float high = bar[offset + 1];
        float low = bar[offset + 2];
        float close = bar[offset + 3];
        float volume = bar[offset + 4];
        float[] sectorBar = new float[] { open, high, low, close, volume };
        Debug.Log(sectorBar[0]);
        return sectorBar;    
    }

    public void RestartAsBearMarket()
    {
        RestartAsScenario("bear_01", 123);
    }

    /// <summary>
    /// Assets/Models/initial_windows/catalog.jsonにあるIDの初期窓へ切り替えます。
    /// 空欄ならinitial_ohlcv_window.jsonへ戻ります。
    /// </summary>
    public void RestartAsScenario(string scenarioId)
    {
        RestartAsScenario(scenarioId, null);
    }

    /// <summary>
    /// 初期窓を切り替え、必要なら乱数seedも同時に指定します。
    /// UnityEventのButtonからは1引数版を使用してください。
    /// </summary>
    public void RestartAsScenario(string scenarioId, int? randomSeed)
    {
        if (market == null)
        {
            Debug.LogError("StockMarketOnnxRunnerを設定してください。", this);
            return;
        }

        string selectedScenario = string.IsNullOrWhiteSpace(scenarioId) ? string.Empty : scenarioId.Trim();
        market.ResetSimulation(selectedScenario, randomSeed);
    }

    /// <summary>InspectorのInitial Window Jsonに設定した初期窓へ戻します。</summary>
    public void RestartToDefaultWindow()
    {
        RestartAsScenario(string.Empty);
    }
}
