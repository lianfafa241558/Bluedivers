using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Core;
using FPSGame.Gameplay;
using static FPSGame.WndTools.WndRootTool;

namespace FPSGame.UI
{
    /// <summary>
    /// 【单行输入弹窗】通用的一次性文本输入（**房间密码**、**首次进入设置玩家名** …）。
    ///
    /// <para>▍为什么可复用：把"标题 / 说明 / 占位符 / 上限 / 是否密码 / 确认回调"打包成
    /// <see cref="PasswordWndInfo"/>，调用方只写 <c>WndHub.Password.Creat(new PasswordWndInfo{...})</c>，
    /// 不需要各自造输入界面。</para>
    ///
    /// <para>▍摆布参考 <c>TipWnd</c>（标题 + 说明 + 两个按钮），多了一个输入框：
    /// <c>Title / Desc / Input(InputField) / Opter{Yes=确定, No=取消}</c>。</para>
    ///
    /// <para>▍⚠ 子类/MonoBehaviour 纪律：本类**不定义 Awake/OnEnable**（会静默顶掉
    /// <c>Window.Awake</c> 的自注册，见 <see cref="Window"/> 的禁令），初始化写 <see cref="FirstShowWnd"/>。</para>
    /// </summary>
    [AddComponentMenu("UI/窗口/输入弹窗")]
    public class PasswordWnd : Window
    {
        private readonly Queue<PasswordWndInfo> _queue = new Queue<PasswordWndInfo>();

        [InspectorName("标题")]
        [SerializeField] private Transform title;

        [InspectorName("说明")]
        [SerializeField] private Transform desc;

        [InspectorName("输入框")]
        [SerializeField] private TMP_InputField input;

        [InspectorName("输入框占位符（可选）")]
        [SerializeField] private Transform inputPlaceholder;

        [InspectorName("确定按钮")]
        [SerializeField] private Transform optA;

        [InspectorName("取消按钮")]
        [SerializeField] private Transform optB;

        private PasswordWndInfo _now;

        /// <summary>弹出一次输入请求（正在显示其它请求时排队，等前一条关掉再弹）。</summary>
        public void Creat(PasswordWndInfo info)
        {
            if (info == null) return;
            _queue.Enqueue(info);
            SetWndState(true);
            if (_now == null && gameObject.activeSelf) LoadNext();
        }

        protected override void FirstShowWnd()
        {
            if (optA != null) ClearButton(optA);
            if (optB != null) ClearButton(optB);
            if (optA != null) SetCilck(optA, Confirm);
            if (optB != null) SetCilck(optB, Cancel);

            if (input != null)
            {
                // 回车 = 确定
                input.onSubmit.RemoveAllListeners();
                input.onSubmit.AddListener(_ => Confirm());
            }
        }

        protected override void ShowWnd()
        {
            InputManager.AddListenerCancel(CancelListener);
        }

        protected override void HideWnd()
        {
        }

        private void LoadNext()
        {
            if (_queue.Count == 0)
            {
                _now = null;
                SetWndState(false);
                return;
            }

            _now = _queue.Dequeue();

            SetText(title, _now.title);
            SetText(desc, _now.desc);
            SetText(optA.transform.GetChild(0), string.IsNullOrEmpty(_now.optA_Text) ? "确定" : _now.optA_Text);
            SetText(optB.transform.GetChild(0), string.IsNullOrEmpty(_now.optB_Text) ? "取消" : _now.optB_Text);

            if (input != null)
            {
                input.characterLimit = _now.maxLength > 0 ? _now.maxLength : 0;
                input.contentType = _now.isPassword
                    ? TMP_InputField.ContentType.Password
                    : TMP_InputField.ContentType.Standard;
                input.text = _now.presetText ?? string.Empty;
                input.ForceLabelUpdate();
                input.ActivateInputField();
                input.Select();
            }

            if (inputPlaceholder != null && !string.IsNullOrEmpty(_now.placeholder))
            {
                SetText(inputPlaceholder, _now.placeholder);
            }
        }

        /// <summary>确定：非空校验 → 回调 → 关一条。</summary>
        private void Confirm()
        {
            if (_now == null) return;

            string value = input != null ? (input.text ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                // 空输入不收：直接给状态提示（不新增节点，复用说明行）
                SetText(desc, string.IsNullOrEmpty(_now.emptyTips) ? "内容不能为空" : _now.emptyTips);
                return;
            }

            var cb = _now.onConfirm;
            _now = null;
            LoadNext();
            cb?.Invoke(value);
        }

        /// <summary>取消（按钮 / ESC）。</summary>
        private void Cancel()
        {
            var cb = _now != null ? _now.onCancel : null;
            _now = null;
            LoadNext();
            cb?.Invoke();
        }

        /// <summary>ESC 监听：消费掉这次 ESC（返回 true），否则窗口层级会继续退。</summary>
        private bool CancelListener()
        {
            if (!State) return false;
            Cancel();
            return true;
        }
    }

    /// <summary>一次输入请求的参数。</summary>
    [Serializable]
    public class PasswordWndInfo
    {
        /// <summary>标题（空 = 显示"请输入"）。</summary>
        public string title = "请输入";
        /// <summary>说明行（也会被"内容不能为空"这类校验提示复用）。</summary>
        public string desc = "";
        /// <summary>输入框占位符（可选）。</summary>
        public string placeholder = "";
        /// <summary>预填文本（可选）。</summary>
        public string presetText = "";
        /// <summary>最大字数（0 = 不限）。</summary>
        public int maxLength = 16;
        /// <summary>是否按密码显示（<c>***</c>）。</summary>
        public bool isPassword;
        /// <summary>两个按钮的文案（留空 = 确定 / 取消）。</summary>
        public string optA_Text, optB_Text;
        /// <summary>空输入时的提示文案（留空 = "内容不能为空"）。</summary>
        public string emptyTips;
        /// <summary>确认回调（参数 = 去掉首尾空格后的输入）。</summary>
        public UnityAction<string> onConfirm;
        /// <summary>取消回调（可选）。</summary>
        public UnityAction onCancel;
    }
}
