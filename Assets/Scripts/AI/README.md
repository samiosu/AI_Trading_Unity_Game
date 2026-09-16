# 株式市場OHLCVモデルのUnity組み込み

`Assets/Models` のLSTMモデルを使い、11セクターの次の1日分のOHLCVを生成します。
提供された `UNITY_INTEGRATION.md` をもとにした、Unity側の導入手順と実装仕様です。

## 導入

1. シーン内の空のGameObjectに **Stock Market ONNX Runner** を追加します。
2. 追加時の `Reset()` で、`Assets/Models` のモデル、metadata、初期窓、カタログ掲載の12個の窓をInspectorへ自動設定します。既存コンポーネントならコンポーネントメニューの **Reset** で再設定できます。
3. **Backend** は最初は **CPU**、**Use Stochastic** は必要に応じて有効にします。
4. **Initial Scenario Id** は空欄で最新の60日窓を使います。相場を指定する場合は `bull_01`、`bear_01`、`sideways_01`、`volatile_01` など、`catalog.json` 掲載のIDを指定します。各タイプは `01`～`03` です。
5. Play Modeに入り、ゲーム側から `GenerateNextBar()` を呼び出します。1回で1日進みます。手動確認にはコンポーネントメニューの **Generate And Log Next Bar** も使えます。

`Initialize On Start` は初期化のみを行います。自動的には日付を進めません。無効にしても、最初のAPI呼び出し時に初期化できます。
モデル等を実行中に変更した場合は **Reinitialize Simulation** で反映します。生成状態は初期条件へ戻ります。

## ゲームからの使用例

```csharp
using AITrading.AI;
using UnityEngine;

public class MarketController : MonoBehaviour
{
    [SerializeField] private StockMarketOnnxRunner market;

    // ゲームの「次の日」処理などから呼び出す。
    public void AdvanceDay()
    {
        float[] bar = market.GenerateNextBar();
        int sectorIndex = 0; // GetSectorIds()でmetadataに記載された順序を確認
        int offset = sectorIndex * 5;
        float open = bar[offset];
        float high = bar[offset + 1];
        float low = bar[offset + 2];
        float close = bar[offset + 3];
        float volume = bar[offset + 4];
        Debug.Log($"{market.GeneratedDayCount}日目: O={open}, H={high}, L={low}, C={close}, V={volume}");
    }

    public void RestartAsBearMarket()
    {
        market.ResetSimulation("bear_01", randomSeed: 123);
    }

    // catalog.jsonのIDを指定して初期窓を切り替える。
    public void RestartAsScenario(string scenarioId)
    {
        market.ResetSimulation(scenarioId);
    }

    // 空文字ならinitial_ohlcv_window.jsonへ戻る。
    public void RestartToDefaultWindow()
    {
        market.ResetSimulation("");
    }
}
```

複数日分は `GenerateNextBar()` を繰り返して生成します。同期APIでCPUへの読み戻し完了まで待つため、一度に大量の日数を生成する場合はコルーチン等でフレームを分けてください。`BarGenerated` イベントでも生成後の55値を受け取れます。

| API | 動作 |
| --- | --- |
| `Initialize()` | Inspector設定で初期化。初期化済みなら何もしない |
| `GenerateNextBar()` | 1日生成し、60日窓を更新して55値を返す |
| `PredictNextBar(window, residual)` | 外部の60日窓を推論。管理中の窓・乱数列は進めない |
| `ResetSimulation(scenarioId, randomSeed)` | 初期窓・生成日数・乱数列をリセット。省略した引数は現在値を継続 |
| `ResetSimulation("")` | `Initial Window Json` に戻す |
| `GetRawWindow()` / `LatestBar` | 生OHLCVのコピーを取得 |
| `GetSectorIds()` / `GetScenarios()` | セクター順／カタログ掲載シナリオを取得 |
| `LastResidualIndex` | 直前に抽出した残差の日インデックス。通常版・抽出前は `-1` |
| `Release()` | Workerを解放。次の初期化はInspector設定から開始 |

`InitialWindowEndDate` は履歴窓の最終日です。生成した日の営業日カレンダーはこの実装では付与せず、`GeneratedDayCount` で管理します。

`initial_windows/catalog.json` にある `bull_01`～`bull_03`、`bear_01`～`bear_03`、`sideways_01`～`sideways_03`、`volatile_01`～`volatile_03` を `ResetSimulation(scenarioId)` に渡すと、実行中に初期窓を切り替えられます。切り替え時は生成日数と確率モデルの残差列もリセットされます。空文字列を渡すと `Initial Window Json` に戻ります。

## 推論だけを使用する場合

GameObjectや初期窓の自動管理が不要なら、`StockMarketPredictor` を直接使えます。以下の引数はいずれもInspector等で保持したアセット参照です。

```csharp
using (var predictor = new StockMarketPredictor(modelAsset, metadataJson))
{
    var initial = StockMarketInitialWindow.Parse(initialWindowJson, predictor.Metadata);
    float[] bar = predictor.PredictNextBar(initial.values);
}
```

確率的モデルでは、同じmetadataで作成した `StockMarketResidualSampler` の `Next()` を第2引数へ渡します。手動で全てゼロの55値を渡せばノイズなしとの比較ができます。`Predictor.Metadata` は読み込み後の情報参照用です。推論開始後に書き換えないでください。

## データ処理の仕様

- 入力は `[1,60,55]`、出力は `[1,11,5]`。入力名は `features`、出力名は `ohlcv`。
- 列順はmetadataの `ohlcvColumns`。セクターごとの順序は `open, high, low, close, volume`。
- **入力だけ**を `(raw - scaler.mean) / scaler.scale` で標準化します。標準化後の外部入力に±6のクリップは行いません。
- 出力は既に再構成済みの生OHLCVです。逆標準化や価格・出来高の再構成をUnity側で重ねて行いません。
- 生成した生OHLCVを末尾へ追加し、最古の55値を除いて常に60日分を保持します。
- 初期窓は3,300値、metadataと同じ列順、昇順・重複なしの日付が必要です。OHLCVの有限・正値と高値／安値の包含関係も確認します。休日カレンダーによる連続営業日の再検査は行いません。
- 現在の `schemaVersion: 4` / `LSTM_RELATIVE_OHLCV` を対象とします。異なる仕様のモデル・metadataは明示的にエラーにします。
- シナリオはカタログ掲載分だけを選択し、metadataのSHA-256と窓の日付範囲を照合します。実行時にフォルダ全体を列挙しません。

## 確率的生成

`lstm_model.stochastic.onnx` には `residual [1,11,5]` を渡します。

1. metadataの `residualBank` から開始日を1つ抽出します。
2. その日から5日連続で、市場全体の11×5残差を読み出します。末尾では先頭へ折り返します。
3. 5日ごとに開始日を再抽出します。
4. 全項目に `generation.stochasticScale`、出来高の項目にはさらに `generation.volumeStochasticScale` を掛けます。

セクターや項目ごとに抽出日を変えません。Unity内では同じ初期窓・モデル・backend・seedを使うと残差抽出を再現できます。`System.Random` とPythonの乱数生成器は異なるため、**同じseedだけでPythonの出力と一致するとは限りません**。比較時は `SampleAt(index)` で抽出日を指定し、倍率適用後の同じ残差を両側へ渡してください。

## ファイルと依存関係

| ファイル | 役割 |
| --- | --- |
| `StockMarketOnnxRunner.cs` | Inspector設定、初期窓、日次更新、イベント、解放 |
| `StockMarketPredictor.cs` | Model/Worker、名前付き入力、出力検証 |
| `StockMarketModelData.cs` | metadata・初期窓・カタログのJSON読込と検証、標準化 |
| `StockMarketResidualSampler.cs` | seed付き5日ブロック抽出 |
| `link.xml` | JSON読込対象をIL2CPPのstrippingから保護 |
| `Tests/Editor/StockMarketTests.cs` | データ検証、残差抽出、実モデル推論、窓更新のテスト |

プロジェクトに導入済みの Unity **6000.3.21f1**、`com.unity.ai.inference` **2.6.1**、`com.unity.nuget.newtonsoft-json` **3.2.2** を使用します。JSONの3次元配列 `residualBank` を読むため、Newtonsoft.Jsonを明示参照しています。別プロジェクトへ移す場合もこのパッケージが必要です。

ランナーにシリアライズされたアセット参照を通じてビルドへモデル・JSONを含めます。実行時のファイルパス読込や `Resources` への移動は不要です。`generated_ohlcv.parquet` はPython側の確認用で、Unity推論には使用しません。

LSTMにはCPUまたはGPUComputeを指定します。GPUPixelは使用できません。GPUComputeにはCompute Shader対応環境が必要です。Workerは `OnDestroy` / `Release()` / `Dispose()` で解放し、入力Tensorは推論ごとに解放します。Workerが所有する出力Tensorを呼び出し側で解放しないでください。

参考: [Unity Worker API 2.6](https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/api/Unity.InferenceEngine.Worker.html)、[対応演算子](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html)。

## テスト

Unityの **Window > General > Test Runner > EditMode** で `AITrading.AI.EditorTests` を実行できます。実モデルによるCPU推論も含みます。

2026-09-15に、このプロジェクトの起動中Unity Editorでコンパイル成功・**13件すべて成功**を確認しました。通常版と残差ゼロの確率的版の一致、残差ありの推論、13個の初期窓（標準1個＋シナリオ12個）、連続生成、同じseedでのリセット再現、不正入力の拒否を確認しています。

GPUCompute、Play Modeのゲーム／チャート連携、WebGL・IL2CPPビルドの動作確認は別途必要です。
