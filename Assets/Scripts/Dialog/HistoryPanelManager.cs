using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Stelle3D.Dialog
{
    /// <summary>
    /// 挂在 HistoryPlane 或 ScrollView 上：
    /// - OnEnable 时根据 StelleHistoryManager.dialogueHistory 重建所有条目
    /// - 负责把数据灌进 HistoryDialogueItem prefab
    /// </summary>
    public class HistoryPanelManager : MonoBehaviour
    {
        [Header("History UI")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentRoot;        // ScrollView/Viewport/Content
        [SerializeField] private GameObject historyItemPrefab; // HistoryDialogueItem prefab

        private void OnEnable()
        {
            RefreshHistory();
        }

        public void RefreshHistory()
        {
            // ★ 先把 ChatHistoryManager 中已有的历史同步过来
            SyncFromChatHistoryIfNeeded();

            if (contentRoot == null || historyItemPrefab == null) return;

            // 清空旧条目
            foreach (Transform child in contentRoot)
            {
                Destroy(child.gameObject);
            }

            // 生成新条目
            foreach (var data in StelleHistoryManager.dialogueHistory)
            {
                var itemGO = Instantiate(historyItemPrefab, contentRoot);
                BindItem(itemGO, data);
            }

            Canvas.ForceUpdateCanvases();

            if (scrollRect != null)
            {
                // 滚动到底部（最新一条）
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 如果 UI 历史列表是空的，就从 ChatHistoryManager 里把现有历史导入进来。
        /// 这样可以把以前多次对话（保存在 JSON 里）的内容一并显示出来。
        /// </summary>
        private void SyncFromChatHistoryIfNeeded()
        {
            if (StelleHistoryManager.dialogueHistory.Count > 0)
            {
                // 已经有数据了（本次运行 append 的），就不用重复导入
                return;
            }

            if (ChatHistoryManager.Instance == null ||
                ChatHistoryManager.Instance.Messages == null)
            {
                return;
            }

            foreach (var msg in ChatHistoryManager.Instance.Messages)
            {
                if (msg == null) continue;

                // 只关心 user / assistant
                if (msg.role != "user" && msg.role != "assistant")
                    continue;

                StelleHistoryManager.dialogueHistory.Add(new StelleDialogueData
                {
                    role    = msg.role,
                    content = msg.content,
                    clip    = null   // 旧记录暂时没语音，后面接入 TTS 时可再补
                });
            }
        }

        private void BindItem(GameObject itemGO, StelleDialogueData data)
        {
            // 根据 prefab 结构查找子节点
            TMP_Text nameText =
                itemGO.transform.Find("Name")?.GetComponent<TMP_Text>();
            TMP_Text contentText =
                itemGO.transform.Find("TextContent")?.GetComponent<TMP_Text>();
            GameObject playButtonGO =
                itemGO.transform.Find("PlayButton")?.gameObject;

            if (nameText != null)
                nameText.text = GetDisplayName(data.role);

            if (contentText != null)
                contentText.text = data.content;

            // 根据文本高度调整条目高度
            var layout = itemGO.GetComponent<LayoutElement>();
            if (layout != null && contentText != null)
            {
                layout.preferredHeight = contentText.preferredHeight;
            }

            // 语音按钮：只有在有 clip 时才显示
            if (playButtonGO != null)
            {
                playButtonGO.SetActive(data.clip != null);
            }

            var controller = itemGO.GetComponent<HistoryItemController>();
            if (controller != null)
            {
                controller.clip = data.clip;
            }
        }

        private string GetDisplayName(string role)
        {
            // 这里先简单写死，你之后可以改成从 GlobalConfig 读角色名 / 多语言
            if (role == "user") return "You";
            if (role == "assistant") return "Stelle"; // 或 “星尘”等
            return role;
        }
    }
}
