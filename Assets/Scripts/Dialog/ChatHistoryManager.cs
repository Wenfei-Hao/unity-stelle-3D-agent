using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Stelle3D.Config;

namespace Stelle3D.Dialog
{
    /// <summary>
    /// 当前会话的对话历史管理：
    /// - 内存中的 ChatSession
    /// - 保存/加载到 JSON 文件
    /// - 提供清空历史的接口（包括删除文件）
    /// </summary>
    public class ChatHistoryManager : MonoBehaviour
    {
        public static ChatHistoryManager Instance { get; private set; }

        [SerializeField]
        private ChatSession currentSession = new ChatSession();

        public IReadOnlyList<ChatMessage> Messages => currentSession.messages;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Duplicate ChatHistoryManager detected, destroying this one.");
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            // 在 Awake 阶段就加载历史
            LoadFromDisk();
        }

        /*
        private void Start()
        {
            LoadFromDisk();
        }
        */

        #region Public API

        public void AddUserMessage(string content, string emotion = "")
        {
            var msg = new ChatMessage("user", content, emotion);
            currentSession.messages.Add(msg);
            Debug.Log($"[ChatHistory] Added user message, total = {currentSession.messages.Count}");
            SaveToDiskSafe();
        }

        public void AddAssistantMessage(string content, string emotion = "")
        {
            var msg = new ChatMessage("assistant", content, emotion);
            currentSession.messages.Add(msg);
            Debug.Log($"[ChatHistory] Added assistant message, total = {currentSession.messages.Count}");
            SaveToDiskSafe();
        }

        /// <summary>
        /// 清空当前会话（内存）并可选删除磁盘文件。
        /// </summary>
        public void ClearSession(bool deleteFile = true)
        {
            currentSession = new ChatSession();
            Debug.Log("[ChatHistory] Session cleared (in memory).");

            if (deleteFile)
            {
                var path = GetHistoryFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[ChatHistory] History file deleted: {path}");
                }
                else
                {
                    Debug.Log($"[ChatHistory] No history file to delete at: {path}");
                }
            }
        }

        /// <summary>
        /// 给 UI 按钮直接调用的版本。
        /// </summary>
        public void ClearSessionFromButton()
        {
            ClearSession(true);
        }

        #endregion

        #region Save & Load

        private string GetHistoryFilePath()
        {
            string fileName = "chat_history.json";
            if (GlobalConfig.Instance != null && !string.IsNullOrEmpty(GlobalConfig.Instance.historyFileName))
            {
                fileName = GlobalConfig.Instance.historyFileName;
            }

            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private void SaveToDiskSafe()
        {
            try
            {
                SaveToDisk();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChatHistory] Failed to save history: {e}");
            }
        }

        private void SaveToDisk()
        {
            string path = GetHistoryFilePath();
            string json = JsonUtility.ToJson(currentSession, true);

            File.WriteAllText(path, json, Encoding.UTF8);
            Debug.Log($"[ChatHistory] Saved {currentSession.messages.Count} messages to: {path}");
        }

        private void LoadFromDisk()
        {
            string path = GetHistoryFilePath();

            if (!File.Exists(path))
            {
                currentSession = new ChatSession();
                Debug.Log($"[ChatHistory] No history file found, start new session. Path: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    currentSession = new ChatSession();
                    Debug.LogWarning("[ChatHistory] History file is empty, start new session.");
                    return;
                }

                var loaded = JsonUtility.FromJson<ChatSession>(json);
                if (loaded != null && loaded.messages != null)
                {
                    currentSession = loaded;
                    Debug.Log($"[ChatHistory] Loaded {currentSession.messages.Count} messages from disk.");
                }
                else
                {
                    currentSession = new ChatSession();
                    Debug.LogWarning("[ChatHistory] Failed to parse history JSON, start new session.");
                }
            }
            catch (Exception e)
            {
                currentSession = new ChatSession();
                Debug.LogError($"[ChatHistory] Failed to load history: {e}");
            }
        }

        #endregion
    }
}
