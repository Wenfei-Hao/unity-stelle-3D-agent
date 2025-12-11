using System.Collections;
using System.Text;
using Stelle3D.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace Stelle3D.AgentAPI
{
    /// <summary>
    /// 统一的 TTS 调用入口：
    /// - 根据 GlobalConfig.ttsBackend 选择 Cloud / LocalGPTSoVITS
    /// - 云端 TTS：POST JSON -> 返回音频 (OGG/WAV 等)
    /// - 播放到 audioSource
    /// - 通过回调把 AudioClip 交给上层（写入历史、状态机收尾）
    /// </summary>
    public class TTSAPI : MonoBehaviour
    {
        public static TTSAPI Instance { get; private set; }

        [Header("Audio Output")]
        public AudioSource audioSource;

        [Header("Debug")]
        public bool logRequestBody = false;

        /// <summary>
        /// 云端 TTS 请求体（text + text_language）
        /// 后面可换其它服务
        /// </summary>
        /*
        [System.Serializable]
        private class CloudTtsRequest
        {
            public string text;
            public string text_language;
            // 预留语音 ID
            public string voice;
        }
        */

        [System.Serializable]
        private class SiliconFlowTtsRequest
        {
            public string model;
            public string input;
            public int    max_tokens;
            public string voice;
            public string response_format;
            public int    sample_rate;
            public bool   stream;
            public float  speed;
            public float  gain;
        }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// DialogManager 调用入口：
        /// - text：要合成的文本
        /// - onClipReady：成功拿到 AudioClip 时调用（用于挂到历史记录）
        /// - onFinished：无论成功/失败，整个播放流程结束时调用（用于角色回到 Idle）
        /// </summary>
        public IEnumerator PlayReplyTTS(
            string text,
            System.Action<AudioClip> onClipReady,
            System.Action onFinished)
        {
            var config = GlobalConfig.Instance;
            if (config == null)
            {
                Debug.LogError("[TTSAPI] GlobalConfig.Instance is null.");
                onFinished?.Invoke();
                yield break;
            }

            if (!config.textToSpeechEnabled)
            {
                // TTS 关闭时，直接结束，让上层自己决定如何回 Idle
                onFinished?.Invoke();
                yield break;
            }

            if (audioSource == null)
            {
                Debug.LogError("[TTSAPI] audioSource is not assigned.");
                onFinished?.Invoke();
                yield break;
            }

            string cleanText = CleanText(text);

            switch (config.ttsBackend)
            {
                case GlobalConfig.TtsBackend.Cloud:
                    yield return StartCoroutine(
                        PlayWithCloudTTS(cleanText, config, onClipReady, onFinished));
                    break;

                case GlobalConfig.TtsBackend.LocalGPTSoVITS:
                    // 接 GPT-SoVITS：可以是本地 HTTP 服务，也可以是启动 Python 进程
                    Debug.LogWarning("[TTSAPI] Local GPT-SoVITS backend not implemented yet.");
                    onFinished?.Invoke();
                    break;
            }
        }

        private IEnumerator PlayWithCloudTTS(
            string text,
            GlobalConfig config,
            System.Action<AudioClip> onClipReady,
            System.Action onFinished)
        {
            if (string.IsNullOrEmpty(config.ttsServiceUrl))
            {
                Debug.LogError("[TTSAPI] ttsServiceUrl is empty.");
                onFinished?.Invoke();
                yield break;
            }

            /*
            // 构造请求体（text + text_language）
            var body = new CloudTtsRequest
            {
                text = text,
                text_language = config.ttsLanguage,
                voice = config.ttsVoiceId
            };
            */

            // 按 MOSS-TTSD 要求，最好带 [S1] 标记（单说话人就一直用 S1）
            string mossInput = "[S1]" + text;

            var body = new SiliconFlowTtsRequest
            {
                model           = string.IsNullOrEmpty(config.ttsModelName)
                                    ? "fnlp/MOSS-TTSD-v0.5"
                                    : config.ttsModelName,
                input           = mossInput,
                max_tokens      = 4096, // 可以按需调整
                voice           = string.IsNullOrEmpty(config.ttsVoiceId)
                                    ? "fnlp/MOSS-TTSD-v0.5:anna"
                                    : config.ttsVoiceId,
                response_format = string.IsNullOrEmpty(config.ttsResponseFormat)
                                    ? "mp3"
                                    : config.ttsResponseFormat,
                sample_rate     = config.ttsSampleRate > 0 ? config.ttsSampleRate : 44100,
                stream          = false,  // 我们先用非流式，拿到完整文件
                speed           = config.ttsSpeed,
                gain            = config.ttsGain
            };


            string json = JsonUtility.ToJson(body);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            if (logRequestBody)
            {
                Debug.Log("[TTSAPI] SiliconFlow TTS body: " + json);
            }

            using (var request = new UnityWebRequest(
                       config.ttsServiceUrl,
                       UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);

                /*
                // AudioType 需和服务返回格式匹配
                // 如果服务返回 wav，就改成 AudioType.WAV；mp3 则改为 AudioType.MPEG。
                request.downloadHandler =
                    new DownloadHandlerAudioClip(config.ttsServiceUrl, AudioType.OGGVORBIS);

                */

                // response_format=mp3 to AudioType.MPEG
                request.downloadHandler = new DownloadHandlerAudioClip(
                    config.ttsServiceUrl,
                    AudioType.MPEG);

                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(config.ttsApiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {config.ttsApiKey}");
                }

                request.timeout = 60;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[TTSAPI] SiliconFlow HTTP error: {request.result}, {request.error}");
                    onFinished?.Invoke();
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    Debug.LogError("[TTSAPI] Downloaded AudioClip is null.");
                    onFinished?.Invoke();
                    yield break;
                }

                audioSource.clip = clip;
                audioSource.Play();
                onClipReady?.Invoke(clip);

                Debug.Log($"[TTSAPI] Playing SiliconFlow TTS audio, length = {clip.length:F2}s");

                // 等待播放结束
                yield return new WaitWhile(() => audioSource.isPlaying);

                onFinished?.Invoke();
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 简单清洗：去掉换行 & 多余空格，避免有些 TTS 不喜欢长换行
            text = text.Replace("\r\n", " ").Replace("\n", " ");
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return text.Trim();
        }
    }
}
