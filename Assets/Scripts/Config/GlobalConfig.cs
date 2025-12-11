using UnityEngine;

namespace Stelle3D.Config
{
    /// <summary>
    /// 全局配置单例：
    /// - LLM/TTS 的基础配置
    /// - 历史记录文件名
    /// - 当前语言设置
    /// 其他脚本通过 GlobalConfig.Instance 访问
    /// </summary>
    public class GlobalConfig : MonoBehaviour
    {
        public static GlobalConfig Instance { get; private set; }

        [Header("LLM Persona & Output")]
        [Tooltip("Stelle 的角色设定（不含语言与 JSON 规则），可以用中英文写。")]
        [TextArea(4, 12)]
        public string personaPrompt = 
            "你是一个名为 Stelle 的虚拟角色，性格温柔、理性，擅长陪用户聊天与提供轻量的建议。";

        [Tooltip("最多带入多少条历史对话给 LLM。")]
        public int maxHistoryMessages = 20;

        [Tooltip("LLM 采样温度。")]
        [Range(0.0f, 2.0f)]
        public float temperature = 0.7f;


        [Header("LLM Settings")]
        [Tooltip("大模型 API 的基础 URL，例如 https://api.xxx.com/v1/chat/completions")]
        public string llmBaseUrl;

        [Tooltip("大模型使用的模型名，例如 gpt-4.1, deepseek-chat 等")]
        public string llmModelName;

        [Tooltip("大模型的 API Key）")]
        public string llmApiKey;

        /*
        [Header("TTS Settings (M0 可以先不用)")]
        [Tooltip("TTS API 的基础 URL（M0 阶段可以不用填）")]
        public string ttsBaseUrl;

        [Tooltip("TTS 使用的 voice / model 名称")]
        public string ttsVoiceName;
        */

        [Header("Chat History Settings")]
        [Tooltip("历史记录文件名（存放在 Resources/ChatHistory 或 persistentDataPath）")]
        public string historyFileName = "chat_history.json";


        public enum Language
        {
            Chinese,
            English,
            Auto // 后面可以让 LLM 自己判断
        }
        [Header("Language")]
        public Language currentLanguage = Language.Chinese;

        // ===== TTS 设置区域 =====
        [Header("TTS Settings")]
        public bool textToSpeechEnabled = true;

        public enum TtsBackend
        {
            Cloud,          // 云端 HTTP TTS（当前实现）
            LocalGPTSoVITS  // 将来本地/自建 GPT-SoVITS
        }

        [Tooltip("当前使用的 TTS 后端：Cloud 或 LocalGPTSoVITS")]
        public TtsBackend ttsBackend = TtsBackend.Cloud;

        [Tooltip("云端 / 本地 HTTP TTS 服务的 URL，例如 http://127.0.0.1:8000/tts")]
        public string ttsServiceUrl = "https://api.siliconflow.cn/v1/audio/speech";

        [Tooltip("TTS 服务 API Key（如无可留空）")]
        public string ttsApiKey;

        // [Tooltip("TTS 语言代码，例如 zh、en、zh-CN 等")]
        // public string ttsLanguage = "zh";

        // MOSS-TTSD 模型名
        public string ttsModelName = "fnlp/MOSS-TTSD-v0.5";

        // 语音 ID：用 anna
        public string ttsVoiceId = "fnlp/MOSS-TTSD-v0.5:anna";

        // 音频格式 / 采样率 / 语速 / 音量增益
        public string ttsResponseFormat = "mp3";
        public int    ttsSampleRate     = 44100;
        public float  ttsSpeed          = 1.0f;
        public float  ttsGain           = 0.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Debug.LogWarning("Duplicate GlobalConfig detected, destroying this one.");
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
}
