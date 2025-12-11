# Stelle3D – Unity 3D Virtual Human Agent

A 3D virtual human built in Unity that you can talk to via text or voice.  
The character responds with LLM-generated text, expressive facial animation, and neural TTS voices (cloud or local GPT-SoVITS), and can remember past conversations using a lightweight RAG backend.

---

## ⭐ Key Features

- **3D virtual avatar with gaze & emotions**
  - Humanoid rigged character controlled by a `StelleController` state machine (Idle / Thinking / Talking / Touching).
  - Head look-at follows the mouse/camera, with configurable angle limits and smooth interpolation.
  - Animator parameters & blendshapes driven by an `emotion_id` from the LLM.

- **LLM-driven dialogue (JSON schema)**
  - Unity `LLMAPI` talks to any OpenAI-compatible chat endpoint.
  - System prompt enforces a JSON response schema:
    ```json
    { "reply_text": "...", "emotion_id": 0–4 }
    ```
  - Sliding window of chat history to stay within context limits and keep latency low.

- **Text & voice interaction**
  - Text input via TextMeshPro UI, output bubble in the top-left corner.
  - Cloud TTS via SiliconFlow `audio/speech` endpoint (e.g. `fnlp/MOSS-TTSD-v0.5:anna`).
  - Pluggable backend design: switch between **Cloud TTS** and **local GPT-SoVITS** with a single enum in `GlobalConfig`.

- **Mini-RAG knowledge & “memory”**
  - Python microservice using **Gemini embeddings + ChromaDB** for retrieval-augmented generation.
  - For each user query, Unity calls the RAG service for top-k relevant persona/world chunks, then injects them into the LLM prompt as structured context.
  - Supports long-term character background, worldbuilding notes, or player-specific memories.

- **Conversation history & replay**
  - JSON chat logs persisted to disk for LLM context (`ChatHistoryManager`).
  - Separate UI history (`StelleHistoryManager`) with scrollable History panel.
  - Each assistant turn optionally carries an `AudioClip` so you can replay past voice lines.

- **Bilingual support**
  - `GlobalConfig` exposes a `Language` mode (Auto / English / Chinese).
  - LLM is instructed to either follow the user’s last language or enforce a specific one.

---

## 🧩 Architecture Overview

### Unity side (C# / .NET / Unity 2022 LTS)

- **Configuration**
  - `GlobalConfig` (scene singleton) holds:
    - LLM endpoint, model name, API key.
    - TTS endpoint, API key, backend type (Cloud / Local GPT-SoVITS).
    - RAG service URL and top-k.
    - Language mode and persona prompt.

- **Agent & dialogue pipeline**
  - `DialogManager`
    - Bridges UI ↔ LLM/TTS/avatar.
    - On send: logs user message, flips avatar to Thinking, disables input, awaits `LLMAPI`.
    - On reply: updates UI, writes history, triggers avatar Talking + TTS.
  - `LLMAPI`
    - Builds `messages[] = system + recent history + current user`.
    - Adds language rule + JSON schema to the system prompt.
    - Optionally calls the RAG HTTP service to get `[CONTEXT]` chunks.
    - Posts to an OpenAI-style `/chat/completions` endpoint and parses `reply_text` + `emotion_id`.

- **TTS & audio**
  - `TTSAPI`
    - For Cloud backend: posts `{ model, input, voice, ... }` to SiliconFlow `audio/speech` and downloads MP3 into an `AudioClip`.
    - For Local backend: reserved hook for GPT-SoVITS (HTTP or local process).
    - Plays audio via a shared `AudioSource` and reports:
      - `onClipReady(clip)` → attaches clip to the latest assistant message in UI history.
      - `onFinished()` → avatar state returns to Idle.

- **Avatar & animation**
  - `StelleController`
    - Maintains an enum `StelleState` (Idle / Talking / Thinking / Touching).
    - Uses Humanoid `HumanBodyBones.Head` for head look-at.
    - Drives Animator booleans (`IsThinking`, `IsTalking`) and optionally emotion-specific layers.
    - Exposed methods:
      - `OnUserMessageSent()` → Thinking
      - `OnReplyStarted(emotionId)` → Talking with emotion
      - `OnReplyFinished()` → Idle

- **History & persistence**
  - `ChatHistoryManager`
    - Stores a rolling `List<Message>` (role/content/metadata).
    - Loads from and saves to JSON in `Application.persistentDataPath`.
    - Used purely for LLM context & persistence across sessions.
  - `StelleHistoryManager` + History UI
    - Builds UI items per turn (role name, text, optional play button).
    - Supports replay of historic `AudioClip`s via a shared `AudioSource`.
    - Includes a “Clear history” button to reset both disk logs and UI.

### Python side (Mini-RAG microservice)

- **Offline index build**
  - Load one or more Markdown / text files describing:
    - Character backstory / persona.
    - Worldbuilding notes or long-term memories.
  - Chunking strategy:
    - Split by paragraphs and headings.
    - Optionally re-split very long paragraphs by sentence to keep each chunk within a safe token budget.
  - Embed each chunk via **Gemini embeddings**.
  - Store embeddings + text in **ChromaDB** with a persistent collection.

- **Online query API**
  - Simple HTTP server (Flask / FastAPI):
    - `POST /rag/query` with `{ "query": "...", "top_k": 4 }`.
    - Returns `{ "chunks": ["...", "...", ...] }` sorted by cosine similarity.
  - Unity calls this service from `LLMAPI` before each LLM request and injects the returned chunks as `[CONTEXT]` in the system prompt.

---

## 🛠 Tech Stack

- **Engine & Languages**
  - Unity 2022 LTS, C#
  - TextMeshPro for UI
  - Animator + blendshapes for animation & facial expressions

- **AI & Backend**
  - Any OpenAI-compatible LLM API (tested with Gemini-style adapters)
  - SiliconFlow `create-speech` for cloud TTS (MOSS-TTSD voice)
  - GPT-SoVITS (optional, local TTS backend)
  - Python 3, Gemini Embedding API
  - ChromaDB as vector store for RAG

---

## 🔊 Local Voice Model (GPT-SoVITS)

This project is designed to support **two** TTS backends:

1. **Cloud TTS (default)**  
   - Uses SiliconFlow’s `/v1/audio/speech` endpoint.
   - Model: `fnlp/MOSS-TTSD-v0.5`  
   - Voice: `fnlp/MOSS-TTSD-v0.5:anna`  
   - Configured in Unity via `GlobalConfig` (TTS Settings).

2. **Local GPT-SoVITS (optional, advanced)**
   - For users who want a custom neural voice, I have trained GPT-SoVITS model that can be used as a local backend.
   - Pre-trained checkpoint (Baidu Netdisk):  
     **https://pan.baidu.com/s/1HSw0H1J7nsfpYNSVWthTTw?pwd=xm9v**
   - The Unity project exposes a `TtsBackend` enum:
     - `Cloud` – use SiliconFlow.
     - `LocalGPTSoVITS` – reserved for a local HTTP service or Python process that wraps GPT-SoVITS.
   - To integrate GPT-SoVITS:
     - Start a local TTS service (HTTP) that takes text and returns audio (e.g., WAV/MP3).
     - Point `GlobalConfig.ttsServiceUrl` to this local endpoint.
     - Implement the corresponding branch in `TTSAPI` (`PlayWithLocalSovits` style coroutine) mirroring the cloud flow.

---

## 🚀 Getting Started (High-Level)

1. **Clone & open in Unity**
   - Clone this repository.
   - Open it with Unity 2022 LTS.
   - Open the main demo scene (e.g., `Assets/Scenes/SampleScene.unity`).

2. **Configure LLM**
   - Add a `GlobalConfig` object in the scene (or use the provided prefab).
   - Fill in:
     - LLM base URL (OpenAI-style `/v1/chat/completions`).
     - Model name.
     - API key.

3. **Configure TTS**
   - Set `textToSpeechEnabled = true`.
   - Set `ttsBackend = Cloud`.
   - Set `ttsServiceUrl = https://api.siliconflow.cn/v1/audio/speech`.
   - Fill in your SiliconFlow API key (stored only in the Unity scene, not committed to Git).

4. **(Optional) Configure RAG**
   - Run the Python RAG index script to build the ChromaDB collection from your docs.
   - Start the RAG microservice (`uvicorn` / Flask / FastAPI).
   - In Unity, set `ragEnabled = true` and `ragServiceUrl` to the local server URL.

5. **Press Play**
   - Enter some text, watch the avatar think, speak, and react.
   - Open the History panel to scroll through previous conversations and replay audio.

---

## 📌 Roadmap

- Finer-grained emotion mapping:
  - Map `emotion_id` to both Animator layers and specific facial blendshape curves.
- Richer action repertoire:
  - Additional idle motions, gesture sets, and touch interactions.
- More robust memory:
  - RAG over long-term player-specific memories, not just static documents.
- In-editor tools:
  - Simple editor windows to manage persona prompts, knowledge documents, and RAG collections.

---

## ⚖️ License / Usage

This project is intended as a teaching and portfolio example for building LLM-driven virtual humans in Unity.  
Before using it in production, please:

- Review the licenses and terms of all third-party services (LLM, TTS, GPT-SoVITS, embeddings).
- Do not commit any API keys or proprietary model checkpoints to a public repository.
- Ensure your usage complies with local regulations and platform policies.
