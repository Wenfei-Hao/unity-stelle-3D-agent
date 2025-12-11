using UnityEngine;

namespace Stelle3D.Dialog
{
    /// <summary>
    /// 挂在 HistoryDialogueItem prefab 上：
    /// - clip 由 HistoryPanelManager 赋值
    /// - PlaySound 由 PlayButton 的 OnClick 调用
    /// </summary>
    public class HistoryItemController : MonoBehaviour
    {
        public AudioClip clip;

        public void PlaySound()
        {
            if (clip == null) return;

            // 先用场景里一个有 Tag 的 AudioSource 播放
            var go = GameObject.FindGameObjectWithTag("AudioSource");
            if (go == null)
            {
                Debug.LogWarning("[HistoryItemController] No GameObject with tag 'AudioSource' found.");
                return;
            }

            var audioSource = go.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogWarning("[HistoryItemController] GameObject with tag 'AudioSource' has no AudioSource.");
                return;
            }

            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
