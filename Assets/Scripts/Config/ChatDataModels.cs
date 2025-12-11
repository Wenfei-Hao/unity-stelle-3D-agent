using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stelle3D.Config
{
    /// <summary>
    /// 单条消息的数据结构
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        // role 可以是 "user" / "assistant" / "system"
        public string role;
        public string content;

        // 时间戳（字符串存就好，方便序列化）
        public string timestamp;

        // 预留的情绪 / 动作标签（后面可以给动画控制用）
        public string emotion;

        public ChatMessage(string role, string content, string emotion = "")
        {
            this.role = role;
            this.content = content;
            this.emotion = emotion;
            this.timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601
        }
    }

    /// <summary>
    /// 一整个会话（用于存盘）
    /// </summary>
    [Serializable]
    public class ChatSession
    {
        public List<ChatMessage> messages = new List<ChatMessage>();

        // 可以扩展一些元数据：比如 sessionId / userName 等
        public string sessionId = Guid.NewGuid().ToString();
    }
}
