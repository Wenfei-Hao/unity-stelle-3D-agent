using System.Collections;
using UnityEngine;

namespace Stelle3D.Character
{
    /// <summary>
    /// 严格参考 starry_unity_agent 的 StarryController：
    /// - 使用 Humanoid 头像骨骼 (HumanBodyBones.Head)
    /// - 在 LateUpdate 中根据鼠标射线得到的点旋转头部
    /// - 使用水平 / 垂直角度限制 + 插值平滑
    ///
    /// 额外加了：OnUserMessageSent / OnReplyStarted / OnReplyFinished
    /// 用于和 DialogManager 对接。
    /// </summary>
    public class StelleController : MonoBehaviour
    {
        public enum StelleState
        {
            Idle = 0,
            Talking = 1,
            Thinking = 2,
            Touching = 3
        }

        [Header("State")]
        public StelleState stelleState = StelleState.Idle;

        [Header("Audio")]
        public AudioSource audioSource;

        // === 字段 ===
        private Transform stelleHead;
        private Animator stelleAnimator;

        // 头部旋转相关
        private float angleX;
        private float angleY;

        [SerializeField]
        private Vector2 horizontalAngleLimit = new Vector2(-50f, 50f);

        [SerializeField]
        private Vector2 verticalAngleLimit = new Vector2(-40f, 40f);

        [SerializeField]
        private float lerpSpeed = 5f;

        [Header("Face / Expression")]
        public StelleFaceController faceController;

        [Header("Mouth Animation (Fallback)")]
        public SimpleMouthAnimator mouthAnimator;



        // 为了兼容你之前的 Animator 参数（可选）
        private int isThinkingId;
        private int isTalkingId;

        private void Awake()
        {
            // 完全参考 Starry：在挂有 Animator 的对象上获取
            stelleAnimator = GetComponent<Animator>();
            if (stelleAnimator == null)
            {
                stelleAnimator = GetComponentInChildren<Animator>();
            }

            if (stelleAnimator == null)
            {
                Debug.LogError("[StelleController] Animator not found on this GameObject or its children.");
                return;
            }

            // 使用 Humanoid Head 骨骼
            stelleHead = stelleAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (stelleHead == null)
            {
                Debug.LogWarning("[StelleController] Head bone (HumanBodyBones.Head) not found. Look-at will not work.");
            }

            // 如果你 Animator 里有这两个参数，就会被一起驱动
            isThinkingId = Animator.StringToHash("IsThinking");
            isTalkingId  = Animator.StringToHash("IsTalking");
        }

        private void Start()
        {
            // 初始为 Idle
            stelleState = StelleState.Idle;
        }

        private void Update()
        {
            // 原 StarryController 在这里检测鼠标点击头部进行「摸头」交互
            // 我们先保留空壳，有需要可以再补：
            // if (Input.GetMouseButtonDown(0)) { DetectMeshCollision(); }
        }

        private void LateUpdate()
        {
            // 完全参考 Starry：只在 Idle 状态下做头部视线跟随
            if (stelleState == StelleState.Idle)
            {
                LookAtPosition(GetMouseWorldPosition());
            }
        }

        // ===================== 视线跟随核心逻辑 =====================

        public void LookAtPosition(Vector3 position)
        {
            if (stelleHead == null) return;

            // 和 Starry 一致：以头骨骼为原点，看向 position
            Quaternion lookRotation = Quaternion.LookRotation(position - stelleHead.position);

            // 计算与当前头部旋转的差值
            Vector3 eulerAngles = lookRotation.eulerAngles - stelleHead.rotation.eulerAngles;

            float x = NormalizeAngle(eulerAngles.x);
            float y = NormalizeAngle(eulerAngles.y);

            // 限制在竖直 / 水平角度范围内
            x = Mathf.Clamp(x, verticalAngleLimit.x, verticalAngleLimit.y);
            y = Mathf.Clamp(y, horizontalAngleLimit.x, horizontalAngleLimit.y);

            // 插值平滑
            angleX = Mathf.Clamp(
                Mathf.Lerp(angleX, x, Time.deltaTime * lerpSpeed),
                verticalAngleLimit.x, verticalAngleLimit.y
            );

            angleY = Mathf.Clamp(
                Mathf.Lerp(angleY, y, Time.deltaTime * lerpSpeed),
                horizontalAngleLimit.x, horizontalAngleLimit.y
            );

            // 和 Starry 完全一致的旋转方式：
            // 先绕头部局部的 up 轴转 Y，再绕局部 right 轴转 X
            Quaternion rotY = Quaternion.AngleAxis(
                angleY,
                stelleHead.InverseTransformDirection(transform.up)
            );
            stelleHead.rotation *= rotY;

            Quaternion rotX = Quaternion.AngleAxis(
                angleX,
                stelleHead.InverseTransformDirection(transform.TransformDirection(Vector3.right))
            );
            stelleHead.rotation *= rotX;
        }

        private float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            else if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// 完全照搬 StarryController：其实这里拿到的是 Camera 的位置（t = 0）
        /// 所以原项目的“看鼠标”本质上是“看向摄像机”。
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // 注意：ray.GetPoint(0) = ray.origin = Camera 位置
            // 原项目就是这么写的，我们保持一致。
            Vector3 point = ray.GetPoint(0);
            return point;
        }

        // ===================== 状态控制（对话联动） =====================

        /// <summary>
        /// DialogManager 调用：用户发消息时
        /// </summary>
        public void OnUserMessageSent()
        {
            SetThinkingState();
        }

        /// <summary>
        /// DialogManager 调用：开始播 AI 回复时
        /// </summary>
        public void OnReplyStarted(int emotionId)
        {
            // 现在 emotionId 直接来自 LLM（0~4），后面可以在 SetTalkingState 里做更丰富映射
            SetTalkingState(emotionId);
        }

        /// <summary>
        /// DialogManager 调用：AI 回复结束时
        /// </summary>
        public void OnReplyFinished()
        {
            SetIdleState();
        }

        // === 以下三组函数，SetThinkingState / SetTalkingState / SetIdleState ===

        public void SetThinkingState()
        {
            stelleState = StelleState.Thinking;

            if (stelleAnimator != null)
            {
                stelleAnimator.SetBool(isThinkingId, true);
                stelleAnimator.SetBool(isTalkingId, false);
            }

            // Thinking 时可以先保持 neutral 表情
            if (faceController != null)
            {
                faceController.ApplyEmotion(0); // 或者后面做一个专门的“困惑/思考”表情
            }

            if (mouthAnimator != null)
            {
                mouthAnimator.StopTalking();
            }
        }

        /*
        public OVRLipSyncContextMorphTarget lipSyncMorph;
        */

        public void SetTalkingState(int emoIndex)
        {
            stelleState = StelleState.Talking;

            if (stelleAnimator != null)
            {
                stelleAnimator.SetBool(isThinkingId, false);
                stelleAnimator.SetBool(isTalkingId, true);
            }

            // 根据 emotionId 设置面部表情
            if (faceController != null)
            {
                faceController.ApplyEmotion(emoIndex);
            }

            // Oculus Lipsync Unity插件控制说话嘴形
            // if (lipSyncMorph != null) lipSyncMorph.enabled = true;
            if (mouthAnimator != null)
            {
                mouthAnimator.StartTalking();
            }
        }
        

        public void SetIdleState()
        {
            stelleState = StelleState.Idle;

            if (stelleAnimator != null)
            {
                stelleAnimator.SetBool(isThinkingId, false);
                stelleAnimator.SetBool(isTalkingId, false);
            }

            if (faceController != null)
            {
                faceController.ResetEmotion(); // 回到 neutral（全部 0）
            }

            // 关闭Oculus Lipsync Unity
            // if (lipSyncMorph != null) lipSyncMorph.enabled = false;

            if (mouthAnimator != null)
            {
                mouthAnimator.StopTalking();
            }
        }
    }
}
