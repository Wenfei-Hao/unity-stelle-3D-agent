using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Stelle3D.Dialog
{
    [Serializable]
    public class StelleDialogueData
    {
        public string role;      // "user" / "assistant"
        public string content;   // 文本
        public AudioClip clip;   // 以后 TTS 生成的语音，可为 null
    }

    /// <summary>
    /// 管理历史对话：
    /// - 静态 List<StelleDialogueData> 供 UI 使用
    /// - 控制历史面板和当前对话面板的显隐
    /// - 预留 AttachClipToLastAssistant，方便 TTS 填充语音
    /// </summary>
    public class StelleHistoryManager : MonoBehaviour
    {
        public static readonly List<StelleDialogueData> dialogueHistory = new List<StelleDialogueData>();

        [Header("Panels")]
        public GameObject dialogUIRoot;     // 当前对话 UI 面板根节点（DialogPanel）
        public GameObject historyPanelRoot; // HistoryCanvas

        [Header("Buttons (optional)")]
        public Button openHistoryButton;    // “历史”按钮
        public Button closeHistoryButton;   // HistoryCanvas 上的退出按钮

        [Header("Hotkeys (optional)")]
        public bool enableScrollOpen = false;

        private bool historyActive = false;

        private void Start()
        {
            if (openHistoryButton != null)
                openHistoryButton.onClick.AddListener(OpenHistoryPanel);
            if (closeHistoryButton != null)
                closeHistoryButton.onClick.AddListener(CloseHistoryPanel);

            if (historyPanelRoot != null)
                historyPanelRoot.SetActive(false); // 初始关闭历史面板
        }

        private void Update()
        {
            if (!enableScrollOpen) return;

            if (Input.mouseScrollDelta.y > 0.2f && !historyActive)
                OpenHistoryPanel();

            if (Input.GetKeyDown(KeyCode.Escape) && historyActive)
                CloseHistoryPanel();
        }

        public void OpenHistoryPanel()
        {
            if (historyPanelRoot != null) historyPanelRoot.SetActive(true);
            if (dialogUIRoot != null) dialogUIRoot.SetActive(false);
            historyActive = true;
        }

        public void CloseHistoryPanel()
        {
            if (historyPanelRoot != null) historyPanelRoot.SetActive(false);
            if (dialogUIRoot != null) dialogUIRoot.SetActive(true);
            historyActive = false;
        }

        /// <summary>
        /// 写入一条历史（文本 + 可选语音）。
        /// </summary>
        public static void AddDialogue(string role, string content, AudioClip clip = null)
        {
            dialogueHistory.Add(new StelleDialogueData
            {
                role    = role,
                content = content,
                clip    = clip
            });
        }

        /// <summary>
        /// 预留给 TTS：为最后一条 assistant 条目挂上 AudioClip。
        /// </summary>
        public static void AttachClipToLastAssistant(AudioClip clip)
        {
            if (clip == null) return;

            for (int i = dialogueHistory.Count - 1; i >= 0; i--)
            {
                var d = dialogueHistory[i];
                if (d.role == "assistant")
                {
                    d.clip = clip;
                    break;
                }
            }
        }

        public void ClearHistory()
        {
            dialogueHistory.Clear();
        }
    }
}
