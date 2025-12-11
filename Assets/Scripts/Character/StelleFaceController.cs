using UnityEngine;

namespace Stelle3D.Character
{
    /// <summary>
    /// 管理 Stelle 面部表情（BlendShapes）。
    /// 根据 emotionId 设置几个常用表情：
    /// 0 = neutral
    /// 1 = happy
    /// 2 = sad
    /// 3 = angry
    /// 4 = surprised
    ///
    /// BlendShape 名称在 Inspector 中配置，代码通过名字查 index，
    /// 方便你之后调整（比如改成「笑い」「困る」「怒り」「びっくり」等）。
    /// </summary>
    public class StelleFaceController : MonoBehaviour
    {
        [Header("Target Renderer (SkinnedMeshRenderer)")]
        public SkinnedMeshRenderer faceRenderer;

        [Header("BlendShape Names (可在 Inspector 中改)")]
        public string happyShapeName      = "笑い";   // or "にこり"
        public string sadShapeName        = "困る";
        public string angryShapeName      = "怒り";
        public string surprisedShapeName  = "びっくり";

        [Header("BlendShape Weights")]
        [Range(0, 100f)] public float happyWeight     = 80f;
        [Range(0, 100f)] public float sadWeight       = 70f;
        [Range(0, 100f)] public float angryWeight     = 80f;
        [Range(0, 100f)] public float surprisedWeight = 80f;

        // 缓存 index，避免每帧用字符串查找
        private int happyIndex      = -1;
        private int sadIndex        = -1;
        private int angryIndex      = -1;
        private int surprisedIndex  = -1;

        private int[] controlledIndices;

        private void Awake()
        {
            CacheBlendShapeIndices();
        }

        private void CacheBlendShapeIndices()
        {
            if (faceRenderer == null)
            {
                Debug.LogError("[StelleFaceController] faceRenderer is not assigned.");
                return;
            }

            var mesh = faceRenderer.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("[StelleFaceController] faceRenderer.sharedMesh is null.");
                return;
            }

            happyIndex      = GetIndex(mesh, happyShapeName, "happy");
            sadIndex        = GetIndex(mesh, sadShapeName, "sad");
            angryIndex      = GetIndex(mesh, angryShapeName, "angry");
            surprisedIndex  = GetIndex(mesh, surprisedShapeName, "surprised");

            controlledIndices = new[]
            {
                happyIndex, sadIndex, angryIndex, surprisedIndex
            };
        }

        private int GetIndex(Mesh mesh, string name, string label)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"[StelleFaceController] {label} shape name is empty.");
                return -1;
            }

            int idx = mesh.GetBlendShapeIndex(name);
            if (idx < 0)
            {
                Debug.LogWarning($"[StelleFaceController] BlendShape '{name}' ({label}) not found on mesh '{mesh.name}'.");
            }
            else
            {
                Debug.Log($"[StelleFaceController] {label} -> '{name}' (index {idx})");
            }

            return idx;
        }

        private void SetWeight(int index, float value)
        {
            if (faceRenderer == null) return;
            if (index < 0) return;
            faceRenderer.SetBlendShapeWeight(index, value);
        }

        private void ResetAllControlled()
        {
            if (faceRenderer == null || controlledIndices == null) return;

            foreach (var idx in controlledIndices)
            {
                if (idx >= 0)
                {
                    faceRenderer.SetBlendShapeWeight(idx, 0f);
                }
            }
        }

        /// <summary>
        /// 根据 emotionId 应用表情。
        /// 0=neutral：全部清零
        /// 1=happy, 2=sad, 3=angry, 4=surprised
        /// </summary>
        public void ApplyEmotion(int emotionId)
        {
            if (faceRenderer == null) return;

            ResetAllControlled();

            switch (emotionId)
            {
                case 1: // happy
                    SetWeight(happyIndex, happyWeight);
                    break;

                case 2: // sad
                    SetWeight(sadIndex, sadWeight);
                    break;

                case 3: // angry
                    SetWeight(angryIndex, angryWeight);
                    break;

                case 4: // surprised
                    SetWeight(surprisedIndex, surprisedWeight);
                    break;

                case 0:
                default:
                    // neutral: 全部 0
                    break;
            }
        }

        /// <summary>
        /// 回到 neutral（清零受控 BlendShapes）。
        /// </summary>
        public void ResetEmotion()
        {
            ResetAllControlled();
        }
    }
}
