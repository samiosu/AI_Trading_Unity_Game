using System;
using System.Globalization;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;

namespace AITrading.AI
{
    /// <summary>ONNXと一緒に出力されたmetadata。列順と入力scalerの情報源です。</summary>
    [Serializable]
    public sealed class StockMarketMetadata
    {
        [JsonProperty(Required = Required.Always)] public int schemaVersion;
        [JsonProperty(Required = Required.Always)] public string modelType;
        [JsonProperty(Required = Required.Always)] public int sequenceLength;
        [JsonProperty(Required = Required.Always)] public int featureSize;
        [JsonProperty(Required = Required.Always)] public string[] sectorIds;
        [JsonProperty(Required = Required.Always)] public string[] ohlcvFields;
        [JsonProperty(Required = Required.Always)] public string[] ohlcvColumns;
        [JsonProperty(Required = Required.Always)] public OnnxInfo onnx;
        [JsonProperty(Required = Required.Always)] public Scaler scaler;
        public StochasticOnnxInfo stochasticOnnx;
        public GenerationSettings generation;
        public float[][][] residualBank;

        public const int SequenceLength = 60;
        public const int SectorCount = 11;
        public const int FieldCount = 5;
        public const int FeatureSize = SectorCount * FieldCount;
        public const int WindowSize = SequenceLength * FeatureSize;

        [Serializable]
        public sealed class OnnxInfo
        {
            [JsonProperty(Required = Required.Always)] public string inputName;
            [JsonProperty(Required = Required.Always)] public string outputName;
            [JsonProperty(Required = Required.Always)] public int[] inputShape;
            [JsonProperty(Required = Required.Always)] public int[] outputShape;
        }

        [Serializable]
        public sealed class Scaler
        {
            [JsonProperty(Required = Required.Always)] public float[] mean;
            [JsonProperty(Required = Required.Always)] public float[] scale;
        }

        [Serializable]
        public sealed class StochasticOnnxInfo
        {
            [JsonProperty(Required = Required.Always)] public string residualInput;
            [JsonProperty(Required = Required.Always)] public int[] residualShape;
        }

        [Serializable]
        public sealed class GenerationSettings
        {
            [JsonProperty(Required = Required.Always)] public int seed;
            [JsonProperty(Required = Required.Always)] public float stochasticScale;
            [JsonProperty(Required = Required.Always)] public float volumeStochasticScale;
            [JsonProperty(Required = Required.Always)] public int residualBlockLength;
        }

        public static StockMarketMetadata Parse(TextAsset json)
        {
            var result = StockMarketData.ReadJson<StockMarketMetadata>(json);
            result.Validate();
            return result;
        }

        public void Validate()
        {
            if (schemaVersion != 4 || modelType != "LSTM_RELATIVE_OHLCV")
                throw new ArgumentException("Expected schemaVersion 4 / LSTM_RELATIVE_OHLCV metadata.");
            if (sequenceLength != SequenceLength || featureSize != FeatureSize)
                throw new ArgumentException("Expected OHLCV input shape [1, 60, 55].");
            StockMarketData.RequireLength(sectorIds, SectorCount, nameof(sectorIds));
            StockMarketData.RequireEqual(ohlcvFields, new[] { "open", "high", "low", "close", "volume" }, nameof(ohlcvFields));
            StockMarketData.RequireLength(ohlcvColumns, FeatureSize, nameof(ohlcvColumns));
            var uniqueSectors = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int sector = 0; sector < SectorCount; sector++)
            {
                if (string.IsNullOrWhiteSpace(sectorIds[sector]) || !uniqueSectors.Add(sectorIds[sector]))
                    throw new ArgumentException("sectorIds must contain 11 unique names.");
                for (int field = 0; field < FieldCount; field++)
                    if (ohlcvColumns[sector * FieldCount + field] != sectorIds[sector] + "__" + ohlcvFields[field])
                        throw new ArgumentException("ohlcvColumns must match sectorIds / ohlcvFields in sector-major order.");
            }
            if (onnx == null || onnx.inputName != "features" || onnx.outputName != "ohlcv")
                throw new ArgumentException("Expected ONNX names features / ohlcv.");
            StockMarketData.RequireEqual(onnx.inputShape, new[] { 1, SequenceLength, FeatureSize }, "onnx.inputShape");
            StockMarketData.RequireEqual(onnx.outputShape, new[] { 1, SectorCount, FieldCount }, "onnx.outputShape");
            if (scaler == null)
                throw new ArgumentException("Missing input scaler.");
            StockMarketData.RequireLength(scaler.mean, FeatureSize, "scaler.mean");
            StockMarketData.RequireLength(scaler.scale, FeatureSize, "scaler.scale");
            for (int i = 0; i < FeatureSize; i++)
                if (!StockMarketData.IsFinite(scaler.mean[i]) || !StockMarketData.IsFinite(scaler.scale[i]) || scaler.scale[i] <= 0)
                    throw new ArgumentException($"Invalid input scaler at column {i}.");
        }

        public void ValidateStochastic()
        {
            if (stochasticOnnx == null || stochasticOnnx.residualInput != "residual")
                throw new ArgumentException("Missing stochastic ONNX residual input metadata.");
            StockMarketData.RequireEqual(stochasticOnnx.residualShape, new[] { 1, SectorCount, FieldCount }, "residualShape");
            if (generation == null || generation.residualBlockLength != 5 ||
                !StockMarketData.IsFinite(generation.stochasticScale) || generation.stochasticScale < 0 ||
                !StockMarketData.IsFinite(generation.volumeStochasticScale) || generation.volumeStochasticScale < 0)
                throw new ArgumentException("Expected finite, non-negative stochastic scales and residualBlockLength = 5.");
            if (residualBank == null || residualBank.Length == 0)
                throw new ArgumentException("Missing residualBank [N, 11, 5].");
            for (int day = 0; day < residualBank.Length; day++)
            {
                StockMarketData.RequireLength(residualBank[day], SectorCount, $"residualBank[{day}]");
                for (int sector = 0; sector < SectorCount; sector++)
                {
                    StockMarketData.RequireLength(residualBank[day][sector], FieldCount, $"residualBank[{day}][{sector}]");
                    foreach (float value in residualBank[day][sector])
                        if (!StockMarketData.IsFinite(value))
                            throw new ArgumentException($"Non-finite residualBank value at day {day}, sector {sector}.");
                }
            }
        }
    }

    [Serializable]
    public sealed class StockMarketInitialWindow
    {
        [JsonProperty(Required = Required.Always)] public int sequenceLength;
        [JsonProperty(Required = Required.Always)] public int featureSize;
        [JsonProperty(Required = Required.Always)] public string[] ohlcvColumns;
        [JsonProperty(Required = Required.Always)] public string[] dates;
        [JsonProperty(Required = Required.Always)] public float[] values;

        public static StockMarketInitialWindow Parse(TextAsset json, StockMarketMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            var window = StockMarketData.ReadJson<StockMarketInitialWindow>(json);
            if (window.sequenceLength != metadata.sequenceLength || window.featureSize != metadata.featureSize)
                throw new ArgumentException("Initial window dimensions do not match metadata.");
            StockMarketData.RequireEqual(window.ohlcvColumns, metadata.ohlcvColumns, "initial window ohlcvColumns");
            StockMarketData.RequireLength(window.dates, metadata.sequenceLength, "initial window dates");
            DateTime previous = DateTime.MinValue;
            foreach (string date in window.dates)
            {
                if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) || parsed <= previous)
                    throw new ArgumentException("Initial window dates must be unique, ascending yyyy-MM-dd dates.");
                previous = parsed;
            }
            StockMarketData.ValidateOhlcv(window.values, StockMarketMetadata.WindowSize, "initial window values");
            return window;
        }
    }

    [Serializable]
    public sealed class StockMarketScenarioCatalog
    {
        [JsonProperty(Required = Required.Always)] public int sequenceLength;
        [JsonProperty(Required = Required.Always)] public int featureSize;
        [JsonProperty(Required = Required.Always)] public string metadataSha256;
        [JsonProperty(Required = Required.Always)] public Scenario[] scenarios;

        [Serializable]
        public sealed class Scenario
        {
            [JsonProperty(Required = Required.Always)] public string id;
            [JsonProperty(Required = Required.Always)] public string file;
            [JsonProperty(Required = Required.Always)] public string regime;
            [JsonProperty(Required = Required.Always)] public string label;
            [JsonProperty(Required = Required.Always)] public string startDate;
            [JsonProperty(Required = Required.Always)] public string endDate;
        }

        public static StockMarketScenarioCatalog Parse(TextAsset json, TextAsset metadataJson)
        {
            if (metadataJson == null) throw new ArgumentNullException(nameof(metadataJson));
            var catalog = StockMarketData.ReadJson<StockMarketScenarioCatalog>(json);
            if (catalog.sequenceLength != StockMarketMetadata.SequenceLength || catalog.featureSize != StockMarketMetadata.FeatureSize ||
                catalog.scenarios == null || catalog.scenarios.Length == 0)
                throw new ArgumentException("Invalid scenario catalog dimensions or scenarios.");
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(metadataJson.bytes)).Replace("-", "");
                if (!string.Equals(hash, catalog.metadataSha256, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Scenario catalog metadata SHA-256 does not match the assigned metadata JSON.");
            }
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var files = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var scenario in catalog.scenarios)
                if (scenario == null || string.IsNullOrWhiteSpace(scenario.id) || !ids.Add(scenario.id) ||
                    string.IsNullOrEmpty(scenario.file) || !scenario.file.EndsWith(".json", StringComparison.Ordinal) ||
                    scenario.file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !files.Add(scenario.file))
                    throw new ArgumentException("Scenario catalog contains an invalid or duplicate id/file.");
            return catalog;
        }
    }

    public static class StockMarketData
    {
        internal static T ReadJson<T>(TextAsset asset) where T : class
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset), $"Assign the {typeof(T).Name} JSON TextAsset.");
            try
            {
                return JsonConvert.DeserializeObject<T>(asset.text, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 16
                }) ?? throw new ArgumentException($"{asset.name}: JSON is null.");
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"{asset.name}: Invalid {typeof(T).Name} JSON.", exception);
            }
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static void RequireLength<T>(T[] values, int length, string name)
        {
            if (values == null || values.Length != length)
                throw new ArgumentException($"{name}: Expected {length} values.");
        }

        internal static void RequireEqual<T>(T[] actual, T[] expected, string name)
        {
            RequireLength(actual, expected.Length, name);
            for (int i = 0; i < expected.Length; i++)
                if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(actual[i], expected[i]))
                    throw new ArgumentException($"{name}: Value/order mismatch at index {i}.");
        }

        public static void ValidateOhlcv(float[] values, int expectedLength, string name = "OHLCV")
        {
            RequireLength(values, expectedLength, name);
            if (expectedLength % StockMarketMetadata.FieldCount != 0)
                throw new ArgumentException("OHLCV length must be a multiple of 5.");
            for (int i = 0; i < values.Length; i++)
                if (!IsFinite(values[i]) || values[i] <= 0)
                    throw new ArgumentException($"{name}: Expected a finite positive value at index {i}.");
            for (int i = 0; i < values.Length; i += StockMarketMetadata.FieldCount)
                if (values[i + 1] < Math.Max(values[i], values[i + 3]) || values[i + 2] > Math.Min(values[i], values[i + 3]))
                    throw new ArgumentException($"{name}: High/low must contain open/close at OHLCV offset {i}.");
        }

        /// <summary>生OHLCVを一度だけ標準化します。外部入力へのクリップは行いません。</summary>
        public static void NormalizeWindow(float[] rawWindow, StockMarketMetadata metadata, float[] destination)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            ValidateOhlcv(rawWindow, StockMarketMetadata.WindowSize, nameof(rawWindow));
            RequireLength(destination, StockMarketMetadata.WindowSize, nameof(destination));
            if (ReferenceEquals(rawWindow, destination))
                throw new ArgumentException("Use a separate normalization buffer to preserve the raw window.");
            for (int i = 0; i < rawWindow.Length; i++)
            {
                int column = i % metadata.featureSize;
                destination[i] = (rawWindow[i] - metadata.scaler.mean[column]) / metadata.scaler.scale[column];
                if (!IsFinite(destination[i]))
                    throw new ArgumentException($"Normalized input is not finite at index {i}.");
            }
        }
    }
}
