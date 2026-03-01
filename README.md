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

### SpriteGroup — SpriteRenderer 一括色制御

`CanvasGroup` の `SpriteRenderer` 版。子孫の `SpriteRenderer` の色・透明度を一括制御する。
`SpriteRenderer.color` は変更せず、`MaterialPropertyBlock` で描画時の色のみ上書きする。

#### セットアップ

キャラクターのルート `GameObject` に `Morn2DSpriteGroupMono` を追加する。
子孫の `SpriteRenderer` が自動的に制御対象になる。
子孫の動的な追加・削除も自動検知される。

#### ランタイム制御

```csharp
var group = GetComponent<Morn2DSpriteGroupMono>();

// フェードアウト
group.Alpha = 0.5f;

// 赤く染める（Rate で適用率を制御）
group.MultiplyColor = Color.red;
group.MultiplyRate = 0.5f; // 50% だけ適用

// 青みを加算
group.AdditiveColor = new Color(0f, 0f, 0.3f);
group.AdditiveRate = 1f;
```

| パラメータ | 説明 |
|-----------|------|
| `Alpha` | 透明度の乗算係数（0–1） |
| `MultiplyColor` | ベース色に乗算する色（デフォルト白） |
| `MultiplyRate` | 乗算の適用率（0–1、0 で変化なし） |
| `AdditiveColor` | 結果の RGB に加算する色（デフォルト黒） |
| `AdditiveRate` | 加算の適用率（0–1、0 で変化なし） |

#### ネスト

グループの中にグループを配置すると、効果が累積する。
各 `SpriteRenderer` は最も近い祖先グループが担当し、親チェーンの効果をすべて累積して適用する。
