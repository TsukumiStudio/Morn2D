using UnityEngine;

namespace MornLib
{
    /// <summary>Morn2DSpriteGroupMonoが子孫のヒエラルキー変更を検知するための内部コンポーネント</summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    internal sealed class Morn2DSpriteGroupChildTracker : MonoBehaviour
    {
        private void OnTransformChildrenChanged()
        {
            var current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent<Morn2DSpriteGroupMono>(out var group))
                {
                    group.SetDirty();
                }

                current = current.parent;
            }
        }
    }
}
