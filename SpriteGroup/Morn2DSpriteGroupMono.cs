using System.Collections.Generic;
using UnityEngine;

namespace MornLib
{
    /// <summary>子孫のSpriteRendererの色・透明度を一括制御する（CanvasGroupのSprite版）</summary>
    [ExecuteAlways]
    public sealed class Morn2DSpriteGroupMono : MonoBehaviour
    {
        private static readonly int s_colorId = Shader.PropertyToID("_Color");

        [Header("アルファ")]
        [SerializeField] [Range(0, 1)] private float _alpha = 1f;

        [Header("乗算")]
        [SerializeField] private Color _multiplyColor = Color.white;
        [SerializeField] [Range(0, 1)] private float _multiplyRate = 1f;

        [Header("加算")]
        [SerializeField] private Color _additiveColor = Color.clear;
        [SerializeField] [Range(0, 1)] private float _additiveRate = 1f;

        private SpriteRenderer[] _ownedRenderers;
        private Morn2DSpriteGroupMono _parentGroup;
        private MaterialPropertyBlock _propertyBlock;
        private bool _cacheDirty = true;

        public float Alpha
        {
            get => _alpha;
            set => _alpha = Mathf.Clamp01(value);
        }

        public Color MultiplyColor
        {
            get => _multiplyColor;
            set => _multiplyColor = value;
        }

        public float MultiplyRate
        {
            get => _multiplyRate;
            set => _multiplyRate = Mathf.Clamp01(value);
        }

        public Color AdditiveColor
        {
            get => _additiveColor;
            set => _additiveColor = value;
        }

        public float AdditiveRate
        {
            get => _additiveRate;
            set => _additiveRate = Mathf.Clamp01(value);
        }

        /// <summary>子孫の構成が変わった際に呼び出し、レンダラーリストを再構築させる</summary>
        public void SetDirty()
        {
            _cacheDirty = true;
        }

        private void OnEnable()
        {
            _cacheDirty = true;
        }

        private void OnDisable()
        {
            ClearPropertyBlocks();
        }

        private void OnTransformChildrenChanged()
        {
            _cacheDirty = true;
        }

        private void LateUpdate()
        {
            if (_cacheDirty)
            {
                RefreshCache();
                _cacheDirty = false;
            }

            ApplyAll();
        }

        private void RefreshCache()
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            // 親グループをキャッシュ
            _parentGroup = null;
            var current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent<Morn2DSpriteGroupMono>(out var group) && group.enabled)
                {
                    _parentGroup = group;
                    break;
                }

                current = current.parent;
            }

            // 子孫の全Transformにトラッカーを自動設置（ヒエラルキー変更の検知用）
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == transform)
                {
                    continue;
                }

                if (!t.TryGetComponent<Morn2DSpriteGroupChildTracker>(out _))
                {
                    var tracker = t.gameObject.AddComponent<Morn2DSpriteGroupChildTracker>();
                    tracker.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }
            }

            // 子孫のSpriteRendererを収集（ネストされた子グループ配下は除外）
            var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            var owned = new List<SpriteRenderer>();
            foreach (var renderer in allRenderers)
            {
                if (GetClosestGroup(renderer.transform) == this)
                {
                    owned.Add(renderer);
                }
            }

            _ownedRenderers = owned.ToArray();
        }

        /// <summary>指定Transformから上方向に探索し、最も近い有効なグループを返す</summary>
        private static Morn2DSpriteGroupMono GetClosestGroup(Transform t)
        {
            var current = t.parent;
            while (current != null)
            {
                if (current.TryGetComponent<Morn2DSpriteGroupMono>(out var group) && group.enabled)
                {
                    return group;
                }

                current = current.parent;
            }

            return null;
        }

        private void ApplyAll()
        {
            if (_ownedRenderers == null)
            {
                return;
            }

            // 親チェーンから累積モディファイアを計算
            var accAlpha = 1f;
            var accMultiply = Color.white;
            var accAdditiveR = 0f;
            var accAdditiveG = 0f;
            var accAdditiveB = 0f;
            AccumulateModifiers(ref accAlpha, ref accMultiply, ref accAdditiveR, ref accAdditiveG, ref accAdditiveB);

            foreach (var renderer in _ownedRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var final = renderer.color * accMultiply;
                final.r += accAdditiveR;
                final.g += accAdditiveG;
                final.b += accAdditiveB;
                final.a *= accAlpha;

                _propertyBlock.SetColor(s_colorId, final);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>このグループと全祖先グループのモディファイアを累積する</summary>
        private void AccumulateModifiers(
            ref float alpha, ref Color multiply,
            ref float additiveR, ref float additiveG, ref float additiveB)
        {
            // 親を先に累積（ルート→リーフの順）
            if (_parentGroup != null)
            {
                _parentGroup.AccumulateModifiers(ref alpha, ref multiply, ref additiveR, ref additiveG, ref additiveB);
            }

            // このグループの効果を適用
            alpha *= _alpha;
            multiply *= Color.Lerp(Color.white, _multiplyColor, _multiplyRate);
            additiveR += _additiveColor.r * _additiveRate;
            additiveG += _additiveColor.g * _additiveRate;
            additiveB += _additiveColor.b * _additiveRate;
        }

        private void ClearPropertyBlocks()
        {
            if (_ownedRenderers == null)
            {
                return;
            }

            foreach (var renderer in _ownedRenderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }
        }
    }
}
