# Morn2D

## 概要

Unity 6 URP 2D 向けの描画拡張ライブラリ。

## 依存関係

| 種別 | 名前 |
|------|------|
| 外部パッケージ | Universal RP |

## 使い方

### Outline — 統合スプライトアウトライン

複数の `SpriteRenderer` で構成されたキャラクターの外周に、統合されたアウトラインを描画する。
同一レイヤーのスプライト群を一塊として扱い、外周にのみアウトライン・Glow を表示する。

#### セットアップ

1. Project Settings → Tags & Layers で空きレイヤーを追加（例: `SpriteOutline`）
2. アウトライン対象のスプライトをすべてそのレイヤーに変更
3. `Morn2D/OutlineComposite` シェーダーでマテリアルを作成
4. 使用する Renderer2D Asset に `Morn2DOutlineRendererFeature` を追加
5. Feature の Settings を設定:
   - `TargetLayerMask` → 対象レイヤーを選択
   - `CompositeMaterial` → 作成したマテリアルを設定

#### ランタイム制御

`Morn2DOutlineControllerMono` を任意の `GameObject` に追加し、Composite マテリアルを参照させる。

```csharp
// コード側からの制御例
var outline = GetComponent<Morn2DOutlineControllerMono>();
outline.OutlineColor = Color.red;
outline.OutlineWidth = 5f;
outline.GlowColor = new Color(1f, 0.5f, 0.5f, 1f);
outline.GlowWidth = 10f;
```

| パラメータ | 説明 |
|-----------|------|
| `OutlineColor` | アウトラインの色（HDR対応） |
| `OutlineWidth` | アウトラインの太さ（px、0–20） |
| `GlowColor` | Glow の色（HDR対応） |
| `GlowWidth` | アウトライン外側に広がる Glow の幅（px、0–30、0 で無効） |
