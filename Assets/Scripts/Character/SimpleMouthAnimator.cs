using System.Collections;
using UnityEngine;

namespace Stelle3D.Character
{
    /// <summary>
    /// 非精确唇形的简易嘴型动画：
    /// - 只在“说话中”随机切换几个 mouth BlendShape（例如 あ/い/う/え/お）
    /// - 停止时全部归零
    ///
    /// 由 StelleController 调用：
    /// - StartTalking() 在 TTS 开始播放时
    /// - StopTalking()  在 TTS 播放结束时
    /// </summary>
    public class SimpleMouthAnimator : MonoBehaviour
    {
        [Header("Target Renderer")]
        public SkinnedMeshRenderer faceRenderer;

        [Header("Mouth BlendShape Names")]
        // 口型模型名称
        public string[] mouthShapeNames = { "あ", "い", "う", "え", "お" };

        [Header("Mouth Weights")]
        [Range(0, 100f)] public float openWeight = 80f;
        [Range(0, 100f)] public float closedWeight = 0f;

        [Header("Timing (seconds)")]
        public float intervalMin = 0.06f;  // 最快间隔
        public float intervalMax = 0.14f;  // 最慢间隔

        private int[] mouthIndices;
        private Coroutine mouthRoutine;

        private void Awake()
        {
            CacheMouthIndices();
        }

        private void CacheMouthIndices()
        {
            if (faceRenderer == null)
            {
                Debug.LogError("[SimpleMouthAnimator] faceRenderer is not assigned.");
                mouthIndices = new int[0];
                return;
            }

            var mesh = faceRenderer.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("[SimpleMouthAnimator] faceRenderer.sharedMesh is null.");
                mouthIndices = new int[0];
                return;
            }

            mouthIndices = new int[mouthShapeNames.Length];

            for (int i = 0; i < mouthShapeNames.Length; i++)
            {
                string name = mouthShapeNames[i];
                if (string.IsNullOrEmpty(name))
                {
                    mouthIndices[i] = -1;
                    continue;
                }

                int idx = mesh.GetBlendShapeIndex(name);
                mouthIndices[i] = idx;

                if (idx < 0)
                {
                    Debug.LogWarning($"[SimpleMouthAnimator] BlendShape '{name}' not found on mesh '{mesh.name}'.");
                }
                else
                {
                    Debug.Log($"[SimpleMouthAnimator] '{name}' -> index {idx}");
                }
            }
        }

        /// <summary>
        /// 开始“说话嘴型”动画（如果还没在播的话）。
        /// </summary>
        public void StartTalking()
        {
            if (mouthRoutine != null) return;
            if (mouthIndices == null || mouthIndices.Length == 0) return;

            mouthRoutine = StartCoroutine(MouthRoutine());
        }

        /// <summary>
        /// 停止嘴型动画并重置嘴型。
        /// </summary>
        public void StopTalking()
        {
            if (mouthRoutine != null)
            {
                StopCoroutine(mouthRoutine);
                mouthRoutine = null;
            }

            ResetMouth();
        }

        private IEnumerator MouthRoutine()
        {
            while (true)
            {
                // 先全部收拢（闭嘴）
                SetAllMouthWeights(closedWeight);

                // 决定这一次是“张嘴”还是“闭嘴”
                bool open = Random.value > 0.3f; // 大约 70% 时间是张嘴
                if (open)
                {
                    // 随机选一个 mouth 形状来用
                    int trials = 0;
                    while (trials < 10)
                    {
                        int idx = Random.Range(0, mouthIndices.Length);
                        int blendIndex = mouthIndices[idx];
                        if (blendIndex >= 0)
                        {
                            SetWeight(blendIndex, openWeight);
                            break;
                        }
                        trials++;
                    }
                }

                // 随机等待一小段时间，再切下一帧嘴型
                float wait = Random.Range(intervalMin, intervalMax);
                yield return new WaitForSeconds(wait);
            }
        }

        private void SetAllMouthWeights(float value)
        {
            if (faceRenderer == null || mouthIndices == null) return;

            foreach (int idx in mouthIndices)
            {
                if (idx >= 0)
                {
                    faceRenderer.SetBlendShapeWeight(idx, value);
                }
            }
        }

        private void SetWeight(int index, float value)
        {
            if (faceRenderer == null || index < 0) return;
            faceRenderer.SetBlendShapeWeight(index, value);
        }

        private void ResetMouth()
        {
            SetAllMouthWeights(closedWeight);
        }
    }
}
