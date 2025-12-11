using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Stelle3D.AgentAPI;
using Stelle3D.Character;

namespace Stelle3D.Dialog
{
    public class DialogManager : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField userInputField;
        public Button sendButton;
        public TextMeshProUGUI aiReplyText;

        [Header("Character")]
        public StelleController stelleController;

        private void Start()
        {
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendButtonClicked);
            }
            else
            {
                Debug.LogError("[DialogManager] sendButton is not assigned!");
            }

            if (ChatHistoryManager.Instance == null)
            {
                Debug.LogWarning("[DialogManager] ChatHistoryManager.Instance is null. 请确认 AppRoot 上挂了 ChatHistoryManager。");
                // 没有历史管理器，就干脆清空一下文本
                if (aiReplyText != null)
                {
                    aiReplyText.text = "";
                }
            }
            else
            {
                // 有历史管理器，尝试恢复最后一条 AI 回复
                RestoreLastAssistantReplyToUI();
            }
        }

        private void RestoreLastAssistantReplyToUI()
        {
            if (aiReplyText == null) return;
            var history = ChatHistoryManager.Instance.Messages;
            if (history == null || history.Count == 0) return;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].role == "assistant")
                {
                    aiReplyText.text = history[i].content;
                    Debug.Log("[DialogManager] Restored last assistant reply to UI.");
                    return;
                }
            }
        }


        private async void OnSendButtonClicked()
        {
            if (userInputField == null)
            {
                Debug.LogError("[DialogManager] userInputField is not assigned!");
                return;
            }

            string userText = userInputField.text;

            if (string.IsNullOrWhiteSpace(userText))
            {
                Debug.Log("[DialogManager] Empty input, ignore.");
                return;
            }

            Debug.Log($"[DialogManager] User: {userText}");

            // 写入 LLM/JSON 历史（用户消息）
            if (ChatHistoryManager.Instance != null)
            {
                ChatHistoryManager.Instance.AddUserMessage(userText);
            }

            // 写入 UI 历史（以后挂语音回放也走这套）
            StelleHistoryManager.AddDialogue("user", userText);

            // 通知角色进入“思考中”
            if (stelleController != null)
            {
                stelleController.OnUserMessageSent();
            }

            // 清空输入框
            userInputField.text = "";
            userInputField.interactable = false;
            sendButton.interactable = false;

            if (LLMAPI.Instance == null)
            {
                Debug.LogError("[DialogManager] LLMAPI.Instance is null!");
                userInputField.interactable = true;
                sendButton.interactable = true;
                return;
            }

            // 调用 Mock LLMAPI
            //LLMAPI.Instance.RequestChat(userText, OnLLMReplyReceived);

            // 调用真实 LLM
            var llmResponse = await LLMAPI.Instance.RequestChatAsync(userText);

            // 恢复输入
            userInputField.interactable = true;
            sendButton.interactable = true;

            if (llmResponse == null)
            {
                // 出错时给一个 fallback 文本
                string fallback = "抱歉，我这边好像遇到了一点问题，请稍后再试。";
                if (aiReplyText != null) aiReplyText.text = fallback;

                if (ChatHistoryManager.Instance != null)
                {
                    ChatHistoryManager.Instance.AddAssistantMessage(fallback, "error");
                }

                if (stelleController != null)
                {
                    stelleController.SetIdleState();
                }

                return;
            }

            string replyText = llmResponse.ReplyText;
            int emotionId   = llmResponse.EmotionId;

            // 写入历史（AI 回复，emotion 先存成字符串，后面可以解析枚举）
            if (ChatHistoryManager.Instance != null)
            {
                ChatHistoryManager.Instance.AddAssistantMessage(replyText, emotionId.ToString());
            }

            // 写入 UI 历史文本，语音稍后通过 AttachClip 补上
            StelleHistoryManager.AddDialogue("assistant", replyText);

            // 更新左上角即时对话气泡
            if (aiReplyText != null)
            {
                aiReplyText.text = replyText;
            }

            // 不在这里进入 Talking，而是在 TTS 真正开始播放时进入 Talking
            /*
            // 角色状态控制 + 情绪
            if (stelleController != null)
            {
                stelleController.OnReplyStarted(emotionId);
                // 现在还没有接 TTS，用一个简单的定时回到 Idle。
                // StartCoroutine(ResetTalkingAfterDelay(2f));
            }
            */

            // 调用 TTS：成功时把 clip 塞回历史，并在播放结束后回 Idle
            if (TTSAPI.Instance != null)
            {
                StartCoroutine(
                    TTSAPI.Instance.PlayReplyTTS(
                        replyText,
                        clip =>
                        {
                            // 为最后一条 assistant 历史记录挂上 AudioClip
                            StelleHistoryManager.AttachClipToLastAssistant(clip);

                            // 切talking
                            if (stelleController != null)
                            {
                                stelleController.OnReplyStarted(emotionId);
                            }
                        },
                        () =>
                        {
                            // 播放完毕，角色回 Idle
                            if (stelleController != null)
                            {
                                stelleController.OnReplyFinished();
                            }
                        })
                );
            }
            else
            {
                // 没有 TTSAPI时的兜底：简单定时回 Idle
                if (stelleController != null)
                {
                    stelleController.OnReplyStarted(emotionId);
                    StartCoroutine(ResetTalkingAfterDelay(2f));
                }
            }

        }

        /*
        private void OnLLMReplyReceived(string replyText)
        {
            Debug.Log($"[DialogManager] AI: {replyText}");

            // 写入历史（AI 回复）
            if (ChatHistoryManager.Instance != null)
            {
                ChatHistoryManager.Instance.AddAssistantMessage(replyText);
            }

            if (aiReplyText != null)
            {
                aiReplyText.text = replyText;
            }

            // 角色状态控制
            if (stelleController != null)
            {
                // 进入说话状态
                stelleController.OnReplyStarted();
                // 简单：2 秒后回到 Idle（后面接 TTS 时可以用语音时长）
                StartCoroutine(ResetTalkingAfterDelay(2f));
            }
        }
        */

        // “说话结束”控制器
        private System.Collections.IEnumerator ResetTalkingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (stelleController != null)
            {
                stelleController.OnReplyFinished();
            }
        }
    }
}
