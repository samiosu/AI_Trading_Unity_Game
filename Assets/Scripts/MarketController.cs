using AITrading.AI;
using UnityEngine;

public class MarketController : MonoBehaviour
{
    [SerializeField] private StockMarketOnnxRunner market;

    // ゲームの「次の日」処理などから呼び出す。
    public void AdvanceDay()
    {
        market.GenerateNextBar();  
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

    public StockMarketOnnxRunner getMarket()
    {
        return market;
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
