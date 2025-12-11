using System;
using UnityEngine;

namespace Stelle3D.AgentAPI
{
    /// <summary>
    /// LLM 返回的 JSON 原始结构（字段名要和 JSON 完全一致）
    /// </summary>
    [Serializable]
    public class LLMJsonPayload
    {
        public string reply_text;
        public int emotion_id;
    }

    /// <summary>
    /// 在项目内部使用的整合后的结果。
    /// </summary>
    public class LLMResponse
    {
        public string ReplyText { get; }
        public int EmotionId { get; }

        public LLMResponse(string replyText, int emotionId)
        {
            ReplyText = replyText;
            EmotionId = emotionId;
        }
    }
}
