using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Stelle3D.Config;
using Stelle3D.Dialog;

namespace Stelle3D.AgentAPI
{
    // ===== 请求/响应 DTO =====

    [Serializable]
    public class ChatMessageDto
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ResponseFormatDto
    {
        public string type; // "json_object"
    }

    [Serializable]
    public class ChatRequestDto
    {
        public string model;
        public ChatMessageDto[] messages;
        public float temperature;
        public ResponseFormatDto response_format; // 为 JSON mode 预留
    }

    [Serializable]
    public class ChoiceMessageDto
    {
        public string content;
    }

    [Serializable]
    public class ChoiceDto
    {
        public ChoiceMessageDto message;
    }

    [Serializable]
    public class ChatResponseDto
    {
        public ChoiceDto[] choices;
    }

    /// <summary>
    /// 负责和真正的 LLM 通信：
    /// - 从 ChatHistoryManager 拿历史
    /// - 拼接 System Prompt（角色 + 语言 + JSON 约束）
    /// - 调用 OpenAI 风格的 chat/completions（Gemini OpenAI 兼容）
    /// - 解析 content 中的 JSON -> LLMResponse
    /// </summary>
    public class LLMAPI : MonoBehaviour
    {
        public static LLMAPI Instance { get; private set; }

        private static readonly HttpClient httpClient = new HttpClient();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LLMAPI] Duplicate instance, destroying this one.");
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        #region Public Entry

        /// <summary>
        /// DialogManager 调用入口：
        /// 输入当前这条 user 消息，返回 LLMResponse（包含文本 + emotion_id）。
        /// </summary>
        public async Task<LLMResponse> RequestChatAsync(string userText)
        {
            var config = GlobalConfig.Instance;
            if (config == null)
            {
                Debug.LogError("[LLMAPI] GlobalConfig.Instance is null.");
                return null;
            }

            if (ChatHistoryManager.Instance == null)
            {
                Debug.LogError("[LLMAPI] ChatHistoryManager.Instance is null.");
                return null;
            }

            if (string.IsNullOrEmpty(config.llmApiKey))
            {
                Debug.LogError("[LLMAPI] llmApiKey is empty, please fill it in GlobalConfig.");
                return null;
            }

            // 构造 messages：System + 历史 + 当前用户这一条
            var messages = BuildMessagesForLLM(userText, config);

            // 构造请求体
            var requestDto = new ChatRequestDto
            {
                model = config.llmModelName,
                messages = messages.ToArray(),
                temperature = config.temperature,
                // 开启 JSON mode，强制返回 JSON 对象
                response_format = new ResponseFormatDto { type = "json_object" }
            };

            try
            {
                string json = JsonUtility.ToJson(requestDto);
                string baseUrl = config.llmBaseUrl; // https://generativelanguage.googleapis.com/v1beta/openai/chat/completions

                using (var request = new HttpRequestMessage(HttpMethod.Post, baseUrl))
                {
                    request.Headers.Clear();
                    // 注意：Inspector 里 llmApiKey 不要带 "Bearer "
                    request.Headers.Add("Authorization", $"Bearer {config.llmApiKey}");
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await httpClient.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.LogError($"[LLMAPI] HTTP error: {response.StatusCode}\n{responseBody}");
                        return null;
                    }

                    // 解析 LLM 返回
                    var chatResponse = JsonUtility.FromJson<ChatResponseDto>(responseBody);
                    if (chatResponse?.choices == null || chatResponse.choices.Length == 0)
                    {
                        Debug.LogError("[LLMAPI] No choices in response.");
                        return null;
                    }

                    string content = chatResponse.choices[0].message.content?.Trim();
                    Debug.Log($"[LLMAPI] Raw content from LLM: {content}");

                    if (string.IsNullOrEmpty(content))
                    {
                        Debug.LogError("[LLMAPI] content is null or empty.");
                        return null;
                    }

                    // content 预期是 JSON，对应 LLMJsonPayload
                    LLMJsonPayload payload = null;
                    try
                    {
                        payload = JsonUtility.FromJson<LLMJsonPayload>(content);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LLMAPI] Failed to parse JSON payload, fallback to plain text. Exception: {ex}");
                    }

                    // 优先使用 JSON 解析成功的结果
                    if (payload != null && !string.IsNullOrEmpty(payload.reply_text))
                    {
                        return new LLMResponse(payload.reply_text, payload.emotion_id);
                    }

                    // 否则退化为：整段当普通文本，emotion = 0
                    Debug.LogWarning("[LLMAPI] JSON payload invalid, use plain text with emotion_id = 0.");
                    return new LLMResponse(content, 0);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLMAPI] Exception when calling LLM: {e}");
                return null;
            }
        }

        #endregion

        #region Helpers

        private List<ChatMessageDto> BuildMessagesForLLM(string currentUserText, GlobalConfig config)
        {
            var messages = new List<ChatMessageDto>();

            // 1. System Prompt：角色设定 + 语言规则 + JSON 规则
            string systemContent = BuildSystemPrompt(config);
            messages.Add(new ChatMessageDto
            {
                role = "system",
                content = systemContent
            });

            // 2. 历史对话（裁剪到 maxHistoryMessages）
            var history = ChatHistoryManager.Instance.Messages;
            int startIndex = Mathf.Max(0, history.Count - config.maxHistoryMessages);

            for (int i = startIndex; i < history.Count; i++)
            {
                var h = history[i];
                messages.Add(new ChatMessageDto
                {
                    role = h.role,         // "user" 或 "assistant"
                    content = h.content
                });
            }

            // 3. 当前这一条用户输入
            messages.Add(new ChatMessageDto
            {
                role = "user",
                content = currentUserText
            });

            return messages;
        }

        private string BuildSystemPrompt(GlobalConfig config)
        {
            // 语言规则
            string languageRule;
            switch (config.currentLanguage)
            {
                case GlobalConfig.Language.English:
                    languageRule = "You must always reply in natural, fluent English.";
                    break;
                case GlobalConfig.Language.Chinese:
                    languageRule = "你必须始终使用简体中文回答用户。";
                    break;
                case GlobalConfig.Language.Auto:
                default:
                    languageRule = "Detect the user's language from the latest message and reply in that language.";
                    break;
            }

            // JSON 输出规则 + 情绪映射（简单版）
            string jsonRule =
                "You MUST output ONLY a strict JSON object, without any explanation or code fences.\n" +
                "The JSON schema is:\n" +
                "{\n" +
                "  \"reply_text\": \"<your reply to the user>\",\n" +
                "  \"emotion_id\": <integer from 0 to 4>\n" +
                "}\n\n" +
                "emotion_id mapping:\n" +
                "0 = neutral\n" +
                "1 = happy / excited\n" +
                "2 = sad / disappointed\n" +
                "3 = angry / frustrated\n" +
                "4 = surprised / shocked\n";

            // 组合 persona + language + jsonRule
            var sb = new StringBuilder();
            sb.AppendLine(config.personaPrompt);
            sb.AppendLine();
            sb.AppendLine("Language rule:");
            sb.AppendLine(languageRule);
            sb.AppendLine();
            sb.AppendLine("Output rule (VERY IMPORTANT):");
            sb.AppendLine(jsonRule);

            return sb.ToString();
        }

        #endregion
    }
}


/*
using System;
using System.Collections;
using UnityEngine;
using Stelle3D.Config; // 如果命名空间不同，改成你自己的

namespace Stelle3D.AgentAPI
{
    /// <summary>
    /// 大模型接口的封装。
    /// M0 阶段先做 Mock：假装网络请求，等一秒返回固定格式的字符串。
    /// 后面再把 Mock 实现替换成真正的 HTTP 请求。
    /// </summary>
    public class LLMAPI : MonoBehaviour
    {
        public static LLMAPI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Duplicate LLMAPI detected, destroying this one.");
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        /// <summary>
        /// 对话请求入口（Mock 版）。
        /// userText: 用户输入的内容
        /// onReply: 回调（拿到 AI 回复之后调用）
        /// </summary>
        public void RequestChat(string userText, Action<string> onReply)
        {
            // 这里先不管 GlobalConfig 的配置，直接走 Mock。
            StartCoroutine(MockRequestCoroutine(userText, onReply));
        }

        private IEnumerator MockRequestCoroutine(string userText, Action<string> onReply)
        {
            // 模拟网络延迟
            yield return new WaitForSeconds(1.0f);

            // 生成一个假的回复
            string reply = $"（Mock LLM）我收到了你说的：{userText}";

            Debug.Log($"[LLMAPI] Reply generated: {reply}");

            onReply?.Invoke(reply);
        }
    }
}
*/