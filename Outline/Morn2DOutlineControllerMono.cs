using UnityEngine;

namespace MornLib
{
    /// <summary>スプライトアウトラインのパラメータをランタイムで制御する</summary>
    [ExecuteAlways]
    public sealed class Morn2DOutlineControllerMono : MonoBehaviour
    {
        private static readonly int s_outlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int s_glowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int s_glowWidthId = Shader.PropertyToID("_GlowWidth");

        [Header("マテリアル参照")]
        [SerializeField] private Material _outlineCompositeMaterial;

        [Header("アウトライン")]
        [SerializeField] [ColorUsage(true, true)] private Color _outlineColor = Color.white;
        [SerializeField] [Range(0, 20)] private float _outlineWidth = 3f;

        [Header("Glow（境界付近の発光）")]
        [SerializeField] [ColorUsage(true, true)] private Color _glowColor = Color.white;
        [SerializeField] [Range(0, 30)] private float _glowWidth;

        public Color OutlineColor
        {
            get => _outlineColor;
            set
            {
                _outlineColor = value;
                ApplyParameters();
            }
        }

        public float OutlineWidth
        {
            get => _outlineWidth;
            set
            {
                _outlineWidth = value;
                ApplyParameters();
            }
        }

        public Color GlowColor
        {
            get => _glowColor;
            set
            {
                _glowColor = value;
                ApplyParameters();
            }
        }

        /// <summary>アウトラインの外側に広がるGlowの幅（ピクセル単位、0で無効）</summary>
        public float GlowWidth
        {
            get => _glowWidth;
            set
            {
                _glowWidth = value;
                ApplyParameters();
            }
        }

        private void Update()
        {
            ApplyParameters();
        }

        private void ApplyParameters()
        {
            if (_outlineCompositeMaterial == null)
            {
                return;
            }

            _outlineCompositeMaterial.SetColor(s_outlineColorId, _outlineColor);
            _outlineCompositeMaterial.SetFloat(s_outlineWidthId, _outlineWidth);
            _outlineCompositeMaterial.SetColor(s_glowColorId, _glowColor);
            _outlineCompositeMaterial.SetFloat(s_glowWidthId, _glowWidth);
        }
    }
}
