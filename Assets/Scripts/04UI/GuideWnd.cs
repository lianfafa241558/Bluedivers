using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using static WndTools.WndRootTool;

public class GuideWnd : Window
{
    [SerializeField]
    private Image _guideImage;
    [SerializeField]
    private TMP_Text _pageText;
    [SerializeField]
    private Transform _prevButton;
    [SerializeField]
    private Transform _nextButton;

    private int _currentIndex;
    private int _totalCount;
    private bool _loaded;
    private List<Sprite> _sprites = new List<Sprite>();
    private Coroutine _loadCoroutine;


    public void Init()
    {
        WndManager.Instance.guideWnd = this;
    }
    public void Uninit()
    {
        WndManager.Instance.guideWnd = null;
    }
    protected override void FirstShowWnd()
    {
        SetCilck(_prevButton, () => SwitchPage(-1));
        SetCilck(_nextButton, () => SwitchPage(1));
    }

    protected override void ShowWnd()
    {
        WindowState = WindowStateEnum.UI;
        InputManager.AddListenerCancel(Cancel);
        _currentIndex = 0;
        if (_loaded)
        {
            UpdateDisplay();
        }
        else
        {
            _loadCoroutine = StartCoroutine(LoadTips());
        }
    }

    protected override void HideWnd()
    {
        WindowState = WindowStateEnum.Game;
        InputManager.RemoveListenerCancel(Cancel);
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }
    }

    public override void OnDestroy()
    {
        // 只在窗口真正销毁时释放贴图缓存
        ClearSprites();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
            SwitchPage(-1);
        else if (Input.GetKeyDown(KeyCode.D))
            SwitchPage(1);
    }


    private IEnumerator LoadTips()
    {
        string tipDir = Path.Combine(Application.streamingAssetsPath, "tip");
        if (!Directory.Exists(tipDir))
        {
            Debug.LogWarning($"GuideWnd: 目录不存在 {tipDir}");
            yield break;
        }

        string[] files = Directory.GetFiles(tipDir, "Tip*.png");
        _totalCount = files.Length;
        if (_totalCount == 0) yield break;

        System.Array.Sort(files);

        for (int i = 0; i < files.Length; i++)
        {
            using var www = UnityWebRequestTexture.GetTexture(files[i]);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                _sprites.Add(sprite);
            }
            else
            {
                Debug.LogWarning($"GuideWnd: 加载图片失败 {files[i]}: {www.error}");
            }
        }

        _loaded = true;
        UpdateDisplay();
    }

    private void SwitchPage(int delta)
    {
        if (_totalCount == 0) return;

        _currentIndex = (_currentIndex + delta + _totalCount) % _totalCount;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_currentIndex >= 0 && _currentIndex < _sprites.Count)
        {
            _guideImage.sprite = _sprites[_currentIndex];
        }
        _pageText.text = $"< {_currentIndex + 1} / {_totalCount} >";
    }

    private void ClearSprites()
    {
        foreach (Sprite sprite in _sprites)
        {
            if (sprite != null && sprite.texture != null)
                Destroy(sprite.texture);
            if (sprite != null)
                Destroy(sprite);
        }
        _sprites.Clear();
    }

    private bool Cancel()
    {
        if (this == null || !State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        SetWndState(false);
        return true;
    }
}
