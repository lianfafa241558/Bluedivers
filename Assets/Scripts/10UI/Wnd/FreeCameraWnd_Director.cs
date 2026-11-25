using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using FPSGame.Core;
using FPSGame.Data;
using FPSGame.Effect;
using FPSGame.Gameplay;
using FPSGame.Managers;

namespace FPSGame.UI
{
/// <summary>
/// 自由相机窗口的导演部分：装配 <see cref="FPSGame.Effect.DirectorMode"/> 与面板、转发热键。
/// <para>⚠ 本分部同样**不能**定义 <c>Awake</c>/<c>OnEnable</c>（会顶掉 <see cref="Window"/> 的自注册）。</para>
/// </summary>
public partial class FreeCameraWnd
{
    private const string DirectorUnitDataPath = "GameData/Director/DirectorUnitData";
    private const float PanelRefreshInterval = 0.2f;

    private DirectorMode _director;
    private DirectorPanel _panel;
    private float _nextPanelRefresh;
    private bool _cameraOverride;
    /// <summary>本次按住 Alt 已经用于"Alt+数字存档位"⇒ 不再抢去做相机微调（否则松手前一直接管相机）。</summary>
    private bool _altUsedForShot;

    /// <summary>首次开窗装配。挂在 <c>FirstShowWnd</c> 下执行，避开 <c>Awake</c> 禁令。</summary>
    private void InitDirector()
    {
        var cam = camera.GetComponent<Camera>();
        var controller = camera.GetComponent<FreeCameraController>();

        _director = gameObject.AddComponent<DirectorMode>();
        _director.Init(controller, cam);

        var unitData = ResSvc.Instance != null
            ? ResSvc.Instance.LoadObject<DirectorUnitData_SO>(DirectorUnitDataPath)
            : null;
        if (unitData == null)
        {
            Debug.LogWarning($"[导演] 找不到单位白名单 Resources/{DirectorUnitDataPath}.asset，单位列表为空");
        }

        _panel = gameObject.AddComponent<DirectorPanel>();
        _panel.Build(transform, ResolveFont(), unitData != null ? unitData.units : null, LoadAirdrops());
        _panel.ShotClicked = index => _director.GotoShot(index);
        _panel.PauseClicked = () => _director.TogglePause();
        _panel.SlowerClicked = () => _director.CycleRate(-1);
        _panel.FasterClicked = () => _director.CycleRate(1);
        _panel.BlendChanged = seconds => _director.SetBlendBase(seconds);

        // 列表拖到场景放置（Unity 场景视图那种手感）：拖拽中实时跟落点，松手才生成
        _panel.RowDragBegan = (data, screenPos) => _director.BeginPlacement();
        _panel.RowDragging = (data, screenPos) =>
        {
            if (IsPointerOverUI()) return;
            if (_director.TryPickScreenPoint(screenPos, out var point)) _director.SetMarker(point);
        };
        _panel.RowDragEnded = (data, screenPos) =>
        {
            _director.EndPlacement();

            // 松手还在面板上 = 取消
            bool overUI = IsPointerOverUI();
            Vector3 point = default;
            bool picked = !overUI && _director.TryPickScreenPoint(screenPos, out point);

            if (picked)
            {
                _director.SetMarker(point);
                if (data is DirectorUnitEntry entry) _director.SpawnUnit(entry);
                else if (data is AirdropData_SO airdrop) _director.ReleaseAirdrop(airdrop);
            }
            else if (!overUI)
            {
                Debug.Log("[导演] 拖拽松手没打中地面");
            }

            // 落点圆环只做"拖动中"的预览：松手即收，不给画面留残影
            // （Tab 点选设的落点不走这里，仍会显示到下次清除）
            _director.ClearMarker();
        };

        _panel.Refresh(_director);
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>中文字体直接复用 showTime 上的 TMP（那份是 MSYH SDF），不额外 Resources.Load 字体。</summary>
    private TMP_FontAsset ResolveFont()
    {
        if (showTime == null) return null;
        var text = showTime.GetComponent<TMP_Text>();
        return text != null ? text.font : null;
    }

    private static List<AirdropData_SO> LoadAirdrops()
    {
        var result = new List<AirdropData_SO>();
        var dic = ResSvc.airdropDic;
        if (dic == null) return result;

        foreach (var kv in dic)
        {
            if (kv.Value != null) result.Add(kv.Value);
        }
        result.Sort((a, b) => a.ID.CompareTo(b.ID));
        return result;
    }

    private void UpdateDirectorHotkeys()
    {
        if (_director == null) return;

        if (Input.GetKeyDown(KeyCode.Tab)) _director.TogglePointer();

        // 固定机位没有"出口"会让人以为卡死：F 随时回自由飞行（与 Unity Scene 视图的 F 同义）
        if (Input.GetKeyDown(KeyCode.F)) _director.EnterFly();

        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)) _altUsedForShot = false;

        // 1..9 跳机位；Alt+1..9 把当前位姿存进去（Shift 与自由视角的「下降」冲突，已让位）
        for (int i = 0; i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
            if (altHeld)
            {
                _director.SaveShot(i);
                _altUsedForShot = true;
            }
            else _director.GotoShot(i);
        }

        // 按住中键或 Alt 临时接管相机：固定机位微调、选点态转视角（松开回到该状态常态）
        // ⚠ 本次 Alt 已用于存档位就不再接管，避免"Alt+1 存档位"顺手把玩家输入收走
        bool cameraOverride = Input.GetMouseButton(2) || (altHeld && !_altUsedForShot);
        if (cameraOverride != _cameraOverride)
        {
            _cameraOverride = cameraOverride;
            _director.SetCameraOverride(cameraOverride);
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket)) _director.CycleRate(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket)) _director.CycleRate(1);
        if (Input.GetKeyDown(KeyCode.Comma)) _director.StepOneTick();

        // 生成/释放已统一走拖拽；只留一个"在已设置的落点上补生成"的快捷入口
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            _director.SpawnUnit(_panel != null ? _panel.SelectedUnit : null);
        if (Input.GetKeyDown(KeyCode.Backspace))
            _director.ClearMarker();

        // 只有指针态吃左键；点面板不能穿透到地面
        if (_director.State == DirectorState.Pointer && InputManager.GetDown(InputState.Fire))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                if (_director.TryPickScreenPoint(Input.mousePosition, out var point)) _director.SetMarker(point);
                else Debug.Log("[导演] 左键没打中地面：机位可能朝天或超过 2000m");
            }
        }
    }

    private void RefreshDirectorPanel()
    {
        if (_panel == null || _director == null) return;
        if (Time.unscaledTime < _nextPanelRefresh) return;
        _nextPanelRefresh = Time.unscaledTime + PanelRefreshInterval;
        _panel.Refresh(_director);
    }
}
}
