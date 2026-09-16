using System;

namespace AITrading.AI
{
    /// <summary>市場全体の同じ日を5日続けて抽出する、seed付き残差サンプラー。</summary>
    public sealed class StockMarketResidualSampler
    {
        private readonly StockMarketMetadata metadata;
        private readonly Random random;
        private int nextIndex;
        private int remainingInBlock;

        public int LastSampledIndex { get; private set; } = -1;

        public StockMarketResidualSampler(StockMarketMetadata metadata, int seed)
        {
            this.metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            metadata.ValidateStochastic();
            random = new Random(seed);
        }

        public float[] Next()
        {
            if (remainingInBlock == 0)
            {
                nextIndex = random.Next(metadata.residualBank.Length);
                remainingInBlock = metadata.generation.residualBlockLength;
            }
            float[] result = SampleAt(nextIndex);
            LastSampledIndex = nextIndex;
            nextIndex = (nextIndex + 1) % metadata.residualBank.Length;
            remainingInBlock--;
            return result;
        }

        /// <summary>Pythonとの比較用。指定日の残差に倍率を掛けます。乱数列は進めません。</summary>
        public float[] SampleAt(int index)
        {
            if (index < 0 || index >= metadata.residualBank.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            var result = new float[StockMarketMetadata.FeatureSize];
            for (int sector = 0; sector < StockMarketMetadata.SectorCount; sector++)
                for (int field = 0; field < StockMarketMetadata.FieldCount; field++)
                {
                    float value = metadata.residualBank[index][sector][field] * metadata.generation.stochasticScale;
                    if (field == 4) value *= metadata.generation.volumeStochasticScale;
                    if (!StockMarketData.IsFinite(value))
                        throw new ArgumentException("Scaled residual is not finite.");
                    result[sector * StockMarketMetadata.FieldCount + field] = value;
                }
            return result;
        }
    }
}
