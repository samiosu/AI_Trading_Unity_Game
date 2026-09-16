using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace AITrading.AI
{
    /// <summary>通常版・確率的版ONNXを実行します。Unityのメインスレッドから使用してください。</summary>
    public sealed class StockMarketPredictor : IDisposable
    {
        private Worker worker;
        private readonly float[] normalized = new float[StockMarketMetadata.WindowSize];
        private static readonly TensorShape InputShape = new TensorShape(1, 60, 55);
        private static readonly TensorShape BarShape = new TensorShape(1, 11, 5);

        public StockMarketMetadata Metadata { get; }
        public bool IsStochastic { get; }

        public StockMarketPredictor(ModelAsset modelAsset, TextAsset metadataJson, BackendType backend = BackendType.CPU)
        {
            if (modelAsset == null) throw new ArgumentNullException(nameof(modelAsset));
            if (backend != BackendType.CPU && backend != BackendType.GPUCompute)
                throw new ArgumentException("This LSTM model requires CPU or GPUCompute; GPUPixel is unsupported.", nameof(backend));
            if (backend == BackendType.GPUCompute && !SystemInfo.supportsComputeShaders)
                throw new NotSupportedException("Compute shaders are unavailable. Select the CPU backend.");
            Metadata = StockMarketMetadata.Parse(metadataJson);
            Model model = ModelLoader.Load(modelAsset);
            IsStochastic = model.inputs.Count == 2;
            if (model.inputs.Count != 1 && !IsStochastic)
                throw new ArgumentException("Expected features, or features + residual model inputs.");
            ValidateInput(model, Metadata.onnx.inputName, InputShape);
            if (IsStochastic)
            {
                Metadata.ValidateStochastic();
                ValidateInput(model, Metadata.stochasticOnnx.residualInput, BarShape);
            }
            if (model.outputs.Count != 1 || model.outputs[0].name != Metadata.onnx.outputName)
                throw new ArgumentException("Expected a single model output named ohlcv.");
            worker = new Worker(model, backend);
        }

        private static void ValidateInput(Model model, string name, TensorShape expected)
        {
            foreach (var input in model.inputs)
                if (input.name == name)
                {
                    if (input.dataType != DataType.Float || !input.shape.IsStatic() || input.shape.ToTensorShape() != expected)
                        throw new ArgumentException($"{name}: Expected float input shape {expected}; got {input.shape}.");
                    return;
                }
            throw new ArgumentException($"Model is missing input '{name}'.");
        }

        /// <param name="rawWindow">古い日付から並ぶ60×55個の生OHLCV。</param>
        /// <param name="residual">確率的モデルでは必須。倍率適用済みの11×5残差。</param>
        /// <returns>11×5個の生OHLCV。追加の逆標準化は不要です。</returns>
        public float[] PredictNextBar(float[] rawWindow, float[] residual = null)
        {
            if (worker == null) throw new ObjectDisposedException(nameof(StockMarketPredictor));
            if (IsStochastic)
            {
                StockMarketData.RequireLength(residual, StockMarketMetadata.FeatureSize, nameof(residual));
                foreach (float value in residual)
                    if (!StockMarketData.IsFinite(value)) throw new ArgumentException("Residual must be finite.", nameof(residual));
            }
            else if (residual != null)
                throw new ArgumentException("The deterministic model does not accept a residual input.", nameof(residual));

            StockMarketData.NormalizeWindow(rawWindow, Metadata, normalized);
            using (var input = new Tensor<float>(InputShape, normalized))
            using (var noise = IsStochastic ? new Tensor<float>(BarShape, residual) : null)
            {
                worker.SetInput(Metadata.onnx.inputName, input);
                if (noise != null) worker.SetInput(Metadata.stochasticOnnx.residualInput, noise);
                worker.Schedule();
                // PeekOutputのTensorはWorkerが所有するので、ここではDisposeしません。
                var output = worker.PeekOutput(Metadata.onnx.outputName) as Tensor<float>;
                if (output == null || output.shape != BarShape)
                    throw new InvalidOperationException("Expected float output shape [1, 11, 5].");
                float[] rawBar = output.DownloadToArray();
                StockMarketData.ValidateOhlcv(rawBar, StockMarketMetadata.FeatureSize, "model output");
                return rawBar;
            }
        }

        public void Dispose()
        {
            worker?.Dispose();
            worker = null;
        }
    }
}
