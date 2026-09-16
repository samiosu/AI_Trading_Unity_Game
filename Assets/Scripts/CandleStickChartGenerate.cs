using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;

/// <summary>
/// 生成したOHLCデータを、指定した本数のキャンドルとしてXChartsに表示します。
/// </summary>
public class CandleStickChartGenerate : MonoBehaviour
{
    // 既存のボタン接続や外部スクリプトから参照できるよう公開する。
    public CandlestickChart chart;
    [SerializeField] private MarketController marketController;

    [Header("表示設定")]
    [Tooltip("表示するキャンドル本数。超えた場合は古いデータから削除します。")]
    [Min(1)]
    [SerializeField] private int visibleCandleCount = 30;

    [Tooltip("各カテゴリ幅に対するキャンドル本体の割合。1でカテゴリ幅いっぱいです。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float candleWidthRatio = 0.9f;

    private readonly List<float[]> candleData = new List<float[]>();

    public int VisibleCandleCount => visibleCandleCount;
    public int RenderedCandleCount => candleData.Count;

    private void Start()
    {
        // CandlestickChartのサンプルデータを消し、生成データだけを表示する。
        ClearChart();
    }

    /// <summary>
    /// ボタンから呼び出し、1日分を追加して最新N本を再描画します。
    /// </summary>
    public void OnButton()
    {
        if (!TryGetSerie(out Candlestick serie))
        {
            return;
        }

        float[] bar = marketController.AdvanceDay(0);
        candleData.Add(bar);

        int maxCount = Mathf.Max(1, visibleCandleCount);
        int removeCount = candleData.Count - maxCount;
        if (removeCount > 0)
        {
            candleData.RemoveRange(0, removeCount);
        }

        Redraw(serie);
    }

    /// <summary>
    /// Inspectorや別UIから表示本数を変更する場合に呼び出します。
    /// </summary>
    public void SetVisibleCandleCount(int count)
    {
        visibleCandleCount = Mathf.Max(1, count);

        int removeCount = candleData.Count - visibleCandleCount;
        if (removeCount > 0)
        {
            candleData.RemoveRange(0, removeCount);
        }

        if (TryGetSerie(out Candlestick serie))
        {
            Redraw(serie);
        }
    }

    /// <summary>
    /// 表示中のデータを消去します。
    /// </summary>
    public void ClearChart()
    {
        candleData.Clear();
        if (chart != null)
        {
            chart.ClearData();
        }
    }

    private bool TryGetSerie(out Candlestick serie)
    {
        serie = null;
        if (chart == null || marketController == null)
        {
            Debug.LogError("ChartとMarket Controllerを設定してください。", this);
            return false;
        }

        serie = chart.GetSerie(0) as Candlestick;
        if (serie == null)
        {
            Debug.LogError("Chartの系列0にCandlestickを設定してください。", this);
            return false;
        }

        return true;
    }

    private void Redraw(Candlestick serie)
    {
        // カテゴリ軸の1区画が、チャートの横幅をデータ数で均等分割する。
        XAxis xAxis = chart.GetChartComponent<XAxis>(serie.xAxisIndex);
        if (xAxis != null)
        {
            xAxis.type = Axis.AxisType.Category;
            xAxis.boundaryGap = true;
        }

        // 0より大きい値はカテゴリ幅に対する割合として解釈される。
        // 固定ピクセル値を使わないため、チャートのリサイズにも追従する。
        serie.barWidth = Mathf.Clamp01(candleWidthRatio);
        serie.barMaxWidth = 0f;

        YAxis yAxis = chart.GetChartComponent<YAxis>(serie.yAxisIndex);
        if (yAxis != null && yAxis.minMaxType == Axis.AxisMinMaxType.Default)
        {
            yAxis.minMaxType = Axis.AxisMinMaxType.MinMax;
        }

        chart.ClearData();
        for (int index = 0; index < candleData.Count; index++)
        {
            float[] bar = candleData[index];

            // AddDataの並びは X, Open, Close, Low, High。
            // MarketControllerの戻り値は Open, High, Low, Close, Volume。
            chart.AddXAxisData("x" + index, serie.xAxisIndex);
            chart.AddData(serie.index, index, bar[0], bar[3], bar[2], bar[1]);
        }
    }
}
