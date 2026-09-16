using System;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Android;

namespace AITrading.AI
{
    /// <summary>シーンから使う窓管理付きランナー。生成はGenerateNextBarを呼ぶたびに1日進みます。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Trading/Stock Market ONNX Runner")]
    public sealed class StockMarketOnnxRunner : MonoBehaviour
    {
        [Header("Models")]
        [SerializeField] private ModelAsset modelAsset;
        [SerializeField] private ModelAsset stochasticModelAsset;
        [SerializeField] private TextAsset metadataJson;
        [SerializeField] private BackendType backend = BackendType.CPU;
        [SerializeField] private bool useStochastic = true;

        [Header("Initial window")]
        [SerializeField] private TextAsset initialWindowJson;
        [SerializeField] private TextAsset scenarioCatalogJson;
        [SerializeField] private TextAsset[] scenarioWindows;
        [Tooltip("空欄ならInitial Window Json。例: bull_01, bear_02, sideways_03, volatile_01")]
        [SerializeField] private string initialScenarioId = "";

        [Header("Generation")]
        [Tooltip("Unity内の残差抽出を再現するseed。Pythonとは乱数生成器が異なります。")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool initializeOnStart = true;

        private StockMarketPredictor predictor;
        private StockMarketResidualSampler sampler;
        private float[] rawWindow;
        private float[] latestBar;


        public bool IsInitialized => predictor != null;
        public bool IsStochastic => predictor != null && predictor.IsStochastic;
        public int GeneratedDayCount { get; private set; }
        public int CurrentSeed { get; private set; }
        public string CurrentScenarioId { get; private set; } = "";
        public string InitialWindowEndDate { get; private set; }
        public int LastResidualIndex => sampler?.LastSampledIndex ?? -1;
        public float[] LatestBar => latestBar == null ? null : (float[])latestBar.Clone();

        /// <summary>窓更新後に発火します。値はmetadata列順の55個の生OHLCVです。</summary>
        public event Action<float[]> BarGenerated;

        private void Start()
        {
            if (initializeOnStart) Initialize();
        }

        public void Initialize()
        {
            if (predictor != null) return;
            Reinitialize();
        }

        /// <summary>Inspectorのモデル・backend・初期条件を再読込します。</summary>
        [ContextMenu("Reinitialize Simulation")]
        public void Reinitialize()
        {
            var candidate = new StockMarketPredictor(useStochastic ? stochasticModelAsset : modelAsset, metadataJson, backend);
            try
            {
                if (candidate.IsStochastic != useStochastic)
                    throw new ArgumentException("Assign the deterministic and stochastic ONNX assets to their corresponding fields.");
                var initial = LoadInitialWindow(candidate.Metadata, initialScenarioId);
                var nextSampler = candidate.IsStochastic ? new StockMarketResidualSampler(candidate.Metadata, seed) : null;
                predictor?.Dispose();
                predictor = candidate;
                sampler = nextSampler;
                SetWindow(initial, initialScenarioId, seed);
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        /// <summary>初期窓と乱数列をリセット。scenarioIdが空文字なら単一の初期窓を選びます。</summary>
        public void ResetSimulation(string scenarioId = null, int? randomSeed = null)
        {
            Initialize();
            string nextScenario = scenarioId ?? CurrentScenarioId;
            int nextSeed = randomSeed ?? CurrentSeed;
            var initial = LoadInitialWindow(predictor.Metadata, nextScenario);
            var nextSampler = predictor.IsStochastic ? new StockMarketResidualSampler(predictor.Metadata, nextSeed) : null;
            sampler = nextSampler;
            SetWindow(initial, nextScenario, nextSeed);
        }

        private void SetWindow(StockMarketInitialWindow initial, string scenarioId, int randomSeed)
        {
            rawWindow = (float[])initial.values.Clone();
            latestBar = null;
            GeneratedDayCount = 0;
            CurrentSeed = randomSeed;
            CurrentScenarioId = scenarioId ?? "";
            InitialWindowEndDate = initial.dates[initial.dates.Length - 1];
        }

        private StockMarketInitialWindow LoadInitialWindow(StockMarketMetadata metadata, string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return StockMarketInitialWindow.Parse(initialWindowJson, metadata);
            var catalog = StockMarketScenarioCatalog.Parse(scenarioCatalogJson, metadataJson);
            var scenario = Array.Find(catalog.scenarios, entry => entry.id == scenarioId);
            if (scenario == null)
                throw new ArgumentException($"Scenario '{scenarioId}' is not listed in catalog.json.", nameof(scenarioId));
            string assetName = scenario.file.Substring(0, scenario.file.Length - ".json".Length);
            TextAsset selected = null;
            if (scenarioWindows != null)
                foreach (var asset in scenarioWindows)
                    if (asset != null && asset.name == assetName)
                    {
                        if (selected != null) throw new ArgumentException($"Duplicate scenario TextAsset: {assetName}.");
                        selected = asset;
                    }
            if (selected == null)
                throw new ArgumentException($"Assign '{scenario.file}' to Scenario Windows.");
            var result = StockMarketInitialWindow.Parse(selected, metadata);
            if (result.dates[0] != scenario.startDate || result.dates[result.dates.Length - 1] != scenario.endDate)
                throw new ArgumentException($"{scenario.file}: Date range does not match catalog.json.");
            return result;
        }

        public StockMarketScenarioCatalog.Scenario[] GetScenarios()
        {
            return StockMarketScenarioCatalog.Parse(scenarioCatalogJson, metadataJson).scenarios;
        }

        /// <summary>1日推論し、最古の行を削除して生OHLCVを末尾へ追加します。同期処理です。</summary>
        public float[] GenerateNextBar()
        {
            Initialize();
            float[] result = predictor.PredictNextBar(rawWindow, sampler?.Next());
            Array.Copy(rawWindow, StockMarketMetadata.FeatureSize, rawWindow, 0,
                rawWindow.Length - StockMarketMetadata.FeatureSize);
            Array.Copy(result, 0, rawWindow, rawWindow.Length - StockMarketMetadata.FeatureSize, result.Length);
            latestBar = (float[])result.Clone();
            GeneratedDayCount++;
            BarGenerated?.Invoke(result);
            return result;
        }

        /// <summary>外部の生OHLCV窓を推論します。管理中の窓と乱数列は進めません。</summary>
        public float[] PredictNextBar(float[] window, float[] residual = null)
        {
            Initialize();
            return predictor.PredictNextBar(window, residual);
        }

        public float[] GetRawWindow()
        {
            Initialize();
            return (float[])rawWindow.Clone();
        }

        public string[] GetSectorIds()
        {
            Initialize();
            return (string[])predictor.Metadata.sectorIds.Clone();
        }

        [ContextMenu("Generate And Log Next Bar")]
        private void GenerateAndLogNextBar()
        {
            float[] bar = GenerateNextBar();
            Debug.Log($"Day {GeneratedDayCount}, {predictor.Metadata.sectorIds[0]}: " +
                $"O={bar[0]} H={bar[1]} L={bar[2]} C={bar[3]} V={bar[4]}", this);
        }

        public void Release()
        {
            predictor?.Dispose();
            predictor = null;
            sampler = null;
            rawWindow = null;
            latestBar = null;
            GeneratedDayCount = 0;
            InitialWindowEndDate = null;
            CurrentScenarioId = "";
        }

        private void OnDestroy() => Release();

#if UNITY_EDITOR
        // アセット参照をSerializeしてビルドへ含めます。実行時にAssetsのパスは読みません。
        private void Reset()
        {
            const string root = "Assets/Models/";
            modelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(root + "lstm_model.onnx");
            stochasticModelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(root + "lstm_model.stochastic.onnx");
            metadataJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(root + "lstm_model.metadata.json");
            initialWindowJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(root + "initial_ohlcv_window.json");
            scenarioCatalogJson = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(root + "initial_windows/catalog.json");
            if (scenarioCatalogJson == null || metadataJson == null) return;
            var catalog = StockMarketScenarioCatalog.Parse(scenarioCatalogJson, metadataJson);
            scenarioWindows = new TextAsset[catalog.scenarios.Length];
            for (int i = 0; i < scenarioWindows.Length; i++)
                scenarioWindows[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(root + "initial_windows/" + catalog.scenarios[i].file);
        }
#endif
    }
}
