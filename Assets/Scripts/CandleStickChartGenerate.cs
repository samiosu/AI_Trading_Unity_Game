using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;

/// <summary>
/// 生成したOHLCデータを、指定した本数のキャンドルとしてXChartsに表示します。
/// </summary>
public class CandleStickChartGenerate : MonoBehaviour
{
    private static readonly string[] SectorNames =
    {
        "エネルギー",
        "素材",
        "資本財",
        "一般消費財",
        "生活必需品",
        "ヘルスケア",
        "金融",
        "情報技術",
        "情報サービス",
        "公益事業",
        "不動産"
    };

    // 既存のボタン接続や外部スクリプトから参照できるよう公開する。
    public CandlestickChart chart;
    [SerializeField] private MarketController marketController;
    private StockPrice stockPrice;

    [Header("表示設定")]
    [Tooltip("表示するキャンドル本数。超えた場合は古いデータから削除します。")]
    [Min(1)]
    [SerializeField] private int visibleCandleCount = 30;

    [Tooltip("各カテゴリ幅に対するキャンドル本体の割合。1でカテゴリ幅いっぱいです。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float candleWidthRatio = 0.9f;

    private float[,] selectSectorBar;

    public int VisibleCandleCount => visibleCandleCount;

    private NextDayEvent nextDayEvent;
    private GlobalStatus globalStatus;
    private void Awake()
    {
        stockPrice = StockPrice.getInstance();
        nextDayEvent = NextDayEvent.GetInstance();
        globalStatus = GlobalStatus.GetInstance();
        stockPrice.setMarket(marketController.getMarket());
    }
    private void Start()
    {
        // CandlestickChartのサンプルデータを消し、生成データだけを表示する。
        ClearChart();
        SetVisibleCandleCount();
        UpdateChartTitle();
    }
    private void OnEnable()
    {
        globalStatus.IdChanged += UpdateChartTitle;
        globalStatus.IdChanged += SetVisibleCandleCount;
    }
    private void OnDisable()
    {
        globalStatus.IdChanged -= UpdateChartTitle;
        globalStatus.IdChanged -= SetVisibleCandleCount;
    }

    private void UpdateChartTitle()
    {
        if (chart == null)
        {
            Debug.LogError("Chartを設定してください。", this);
            return;
        }

        int sectorId = globalStatus.SectorId;
        if (sectorId < 0 || sectorId >= SectorNames.Length)
        {
            Debug.LogWarning($"未対応のセクターIDです: {sectorId}", this);
            return;
        }

        Title title = chart.EnsureChartComponent<Title>();
        title.text = SectorNames[sectorId];
    }

    /// <summary>
    /// ボタンから呼び出し、1日分を追加して最新N本を再描画します。
    /// </summary>
    public void OnButton()
    {
        AdvanceGraph();
        nextDayEvent.DoThing();
    }

    // 1日進め・描画を更新する
    private void AdvanceGraph()
    {
        if (!TryGetSerie(out Candlestick serie))
        {
            return;
        }

        marketController.AdvanceDay();

        stockPrice.changeBarAndSectorBar();
        selectSectorBar = stockPrice.getSectorBar(visibleCandleCount);
        Redraw(serie);
    }

    /// <summary>
    /// Inspectorや別UIから表示本数を変更する場合に呼び出します。
    /// </summary>
    public void SetVisibleCandleCount()
    {
        selectSectorBar = stockPrice.getSectorBar(visibleCandleCount);
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
        selectSectorBar = null;
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
        if (yAxis != null)
        {
            yAxis.axisLabel.numericFormatter = "F0";
            if (yAxis.minMaxType == Axis.AxisMinMaxType.Default)
            {
                yAxis.minMaxType = Axis.AxisMinMaxType.MinMax;
            }
        }

        chart.ClearData();
        for (int index = 0; index < selectSectorBar.GetLength(0); index++)
        {
            // AddDataの並びは X, Open, Close, Low, High。
            // MarketControllerの戻り値は Open, High, Low, Close, Volume。
            chart.AddXAxisData("x" + index, serie.xAxisIndex);
            chart.AddData(serie.index, index,
                selectSectorBar[index, 0],
                selectSectorBar[index, 3],
                selectSectorBar[index, 2],
                selectSectorBar[index, 1]);
        }
    }
}
