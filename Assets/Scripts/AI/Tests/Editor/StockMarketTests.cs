using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AITrading.AI.Tests
{
    public sealed class StockMarketTests
    {
        private const string Root = "Assets/Models/";
        private static TextAsset MetadataJson => Load<TextAsset>("lstm_model.metadata.json");

        private static T Load<T>(string name) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(Root + name);
            Assert.That(asset, Is.Not.Null, Root + name);
            return asset;
        }

        private static float[] Initial(StockMarketMetadata metadata)
            => StockMarketInitialWindow.Parse(Load<TextAsset>("initial_ohlcv_window.json"), metadata).values;

        [Test]
        public void BundledMetadataAndAllCatalogWindowsAreCompatible()
        {
            var metadata = StockMarketMetadata.Parse(MetadataJson);
            metadata.ValidateStochastic();
            Assert.That(Initial(metadata).Length, Is.EqualTo(3300));
            var catalog = StockMarketScenarioCatalog.Parse(Load<TextAsset>("initial_windows/catalog.json"), MetadataJson);
            Assert.That(catalog.scenarios.Length, Is.EqualTo(12));
            foreach (var scenario in catalog.scenarios)
            {
                var window = StockMarketInitialWindow.Parse(Load<TextAsset>("initial_windows/" + scenario.file), metadata);
                Assert.That(window.dates[0], Is.EqualTo(scenario.startDate));
                Assert.That(window.dates[59], Is.EqualTo(scenario.endDate));
            }
        }

        [Test]
        public void NormalizationPreservesRawWindowAndValuesBeyondSix()
        {
            var metadata = StockMarketMetadata.Parse(MetadataJson);
            metadata.scaler.mean = Enumerable.Repeat(10f, 55).ToArray();
            metadata.scaler.scale = Enumerable.Repeat(2f, 55).ToArray();
            var bar = new[] { 100f, 120f, 80f, 110f, 40f };
            float[] raw = Enumerable.Range(0, 660).SelectMany(_ => bar).ToArray();
            float[] snapshot = (float[])raw.Clone();
            var normalized = new float[3300];
            StockMarketData.NormalizeWindow(raw, metadata, normalized);
            Assert.That(normalized.Take(5), Is.EqualTo(new[] { 45f, 55f, 35f, 50f, 15f }));
            Assert.That(normalized.Skip(3295), Is.EqualTo(new[] { 45f, 55f, 35f, 50f, 15f }));
            Assert.That(raw, Is.EqualTo(snapshot));
            Assert.Throws<ArgumentException>(() => StockMarketData.NormalizeWindow(raw, metadata, raw));
        }

        [TestCase("columns")]
        [TestCase("dates")]
        [TestCase("length")]
        [TestCase("negative")]
        [TestCase("high")]
        public void InvalidInitialDataIsRejected(string corruption)
        {
            var json = JObject.Parse(Load<TextAsset>("initial_ohlcv_window.json").text);
            switch (corruption)
            {
                case "columns": json["ohlcvColumns"][0] = "wrong__open"; break;
                case "dates": json["dates"][1] = json["dates"][0].DeepClone(); break;
                case "length": ((JArray)json["values"]).RemoveAt(0); break;
                case "negative": json["values"][4] = -1; break;
                case "high": json["values"][1] = 1; break;
            }
            var asset = new TextAsset(json.ToString());
            try { Assert.Throws<ArgumentException>(() => StockMarketInitialWindow.Parse(asset, StockMarketMetadata.Parse(MetadataJson))); }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void InvalidScalerAndNonFiniteInputAreRejected()
        {
            var metadata = StockMarketMetadata.Parse(MetadataJson);
            metadata.scaler.scale[0] = 0;
            Assert.Throws<ArgumentException>(() => metadata.Validate());
            float[] raw = Initial(StockMarketMetadata.Parse(MetadataJson));
            raw[0] = float.NaN;
            Assert.Throws<ArgumentException>(() => StockMarketData.ValidateOhlcv(raw, 3300));
        }

        [Test]
        public void CatalogForAnotherMetadataIsRejected()
        {
            var changed = new TextAsset(MetadataJson.text + " ");
            try { Assert.Throws<ArgumentException>(() => StockMarketScenarioCatalog.Parse(Load<TextAsset>("initial_windows/catalog.json"), changed)); }
            finally { Object.DestroyImmediate(changed); }
        }

        [Test]
        public void ResidualSamplingKeepsFiveDayBlocksAndWrapsTheWholeMarket()
        {
            var metadata = StockMarketMetadata.Parse(MetadataJson);
            metadata.residualBank = new[]
            {
                Enumerable.Range(0, 11).Select(sector => new[] { 1f + sector, 2f, 3f, 4f, 8f }).ToArray(),
                Enumerable.Range(0, 11).Select(sector => new[] { 10f + sector, 20f, 30f, 40f, 80f }).ToArray()
            };
            metadata.generation.stochasticScale = 2f;
            metadata.generation.volumeStochasticScale = 0.25f;
            var first = new StockMarketResidualSampler(metadata, 42);
            var replay = new StockMarketResidualSampler(metadata, 42);
            Assert.That(first.SampleAt(0).Take(5), Is.EqualTo(new[] { 2f, 4f, 6f, 8f, 4f }));
            int previous = -1;
            for (int day = 0; day < 15; day++)
            {
                float[] sample = first.Next();
                Assert.That(sample, Is.EqualTo(replay.Next()));
                if (day % 5 != 0) Assert.That(first.LastSampledIndex, Is.EqualTo((previous + 1) % 2));
                Assert.That(sample, Is.EqualTo(first.SampleAt(first.LastSampledIndex)));
                Assert.That(sample[50] - sample[0], Is.EqualTo(20f));
                previous = first.LastSampledIndex;
            }
        }

        [Test]
        public void DeterministicAndZeroResidualModelsAgreeOnCpu()
        {
            using (var deterministic = new StockMarketPredictor(Load<ModelAsset>("lstm_model.onnx"), MetadataJson))
            using (var stochastic = new StockMarketPredictor(Load<ModelAsset>("lstm_model.stochastic.onnx"), MetadataJson))
            {
                float[] raw = Initial(deterministic.Metadata);
                float[] snapshot = (float[])raw.Clone();
                float[] expected = deterministic.PredictNextBar(raw);
                Assert.That(deterministic.PredictNextBar(raw), Is.EqualTo(expected));
                float[] actual = stochastic.PredictNextBar(raw, new float[55]);
                for (int i = 0; i < actual.Length; i++)
                    Assert.That(actual[i], Is.EqualTo(expected[i]).Within(Math.Max(0.0001f, expected[i] * 0.00001f)), $"column {i}");
                Assert.That(raw, Is.EqualTo(snapshot));
                var sampler = new StockMarketResidualSampler(stochastic.Metadata, 42);
                Assert.That(stochastic.PredictNextBar(raw, sampler.Next()), Is.Not.EqualTo(actual));
                Assert.Throws<ArgumentException>(() => stochastic.PredictNextBar(raw));
                Assert.Throws<ArgumentException>(() => deterministic.PredictNextBar(raw, new float[55]));
                deterministic.Dispose();
                Assert.Throws<ObjectDisposedException>(() => deterministic.PredictNextBar(raw));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RunnerRollsRawWindowAndResetReplaysGeneration(bool stochastic)
        {
            var go = new GameObject("StockMarket test (temporary)");
            try
            {
                var runner = go.AddComponent<StockMarketOnnxRunner>();
                var serialized = new SerializedObject(runner);
                serialized.FindProperty("useStochastic").boolValue = stochastic;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                runner.Initialize();
                float[] before = runner.GetRawWindow();
                float[] first = runner.GenerateNextBar();
                float[] after = runner.GetRawWindow();
                Assert.That(after.Take(3245), Is.EqualTo(before.Skip(55)));
                Assert.That(after.Skip(3245), Is.EqualTo(first));
                float[] firstSnapshot = (float[])first.Clone();
                first[0] = -1;
                Assert.That(runner.GetRawWindow(), Is.EqualTo(after));
                Assert.That(runner.LatestBar, Is.EqualTo(firstSnapshot));
                for (int day = 1; day < 8; day++)
                {
                    float[] previous = runner.LatestBar;
                    float[] next = runner.GenerateNextBar();
                    for (int sector = 0; sector < 11; sector++)
                    {
                        int volume = sector * 5 + 4;
                        Assert.That(next[volume], Is.InRange(previous[volume] / 3 * 0.99999f, previous[volume] * 3 * 1.00001f));
                    }
                }
                Assert.That(runner.GeneratedDayCount, Is.EqualTo(8));
                runner.ResetSimulation();
                Assert.That(runner.GeneratedDayCount, Is.Zero);
                Assert.That(runner.GenerateNextBar(), Is.EqualTo(firstSnapshot));
                foreach (var scenario in runner.GetScenarios())
                {
                    runner.ResetSimulation(scenario.id);
                    Assert.That(runner.InitialWindowEndDate, Is.EqualTo(scenario.endDate));
                    StockMarketData.ValidateOhlcv(runner.GenerateNextBar(), 55);
                }
                float[] validState = runner.GetRawWindow();
                Assert.Throws<ArgumentException>(() => runner.ResetSimulation("not_in_catalog"));
                Assert.That(runner.GetRawWindow(), Is.EqualTo(validState));
                runner.Release();
                Assert.That(runner.IsInitialized, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
