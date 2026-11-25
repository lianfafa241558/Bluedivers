using UnityEngine;
using UnityEngine.Rendering;

namespace FPSGame.Effect
{
/// <summary>
/// 导演模式的落点标记：地面圆环 + 竖直光柱，单实例复用。
/// <para>▍为什么程序化建而不是挂 prefab：本工具链要求 prefab 零改动
/// （MCP 的 <c>SaveAsPrefabAsset</c> 会按内存旧值回写、清掉没触碰的字段）。</para>
/// </summary>
[AddComponentMenu("特效/导演落点标记")]
public class DirectorMarker : MonoBehaviour
{
    private const int Segments = 48;
    private const float Radius = 2.5f;
    private const float BeamHeight = 30f;

    private static readonly Color MarkerColor = new(0.35f, 0.95f, 1f, 0.85f);

    private Material _material;
    private LineRenderer _ring;
    private LineRenderer _beam;

    /// <summary>是否已设置落点（与"当前是否显示"解耦：H 隐藏面板时只藏显示、不丢落点）。</summary>
    public bool HasPoint { get; private set; }

    public Vector3 Point => transform.position;

    private bool _visualVisible = true;

    private void Awake()
    {
        Build();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    public void Show(Vector3 point)
    {
        if (_ring == null) return;
        transform.position = point;
        HasPoint = true;
        ApplyVisible();
    }

    public void Hide()
    {
        HasPoint = false;
        gameObject.SetActive(false);
    }

    /// <summary>只切显隐、保留落点数据（导演按 H 隐藏 UI 时一起藏）。</summary>
    public void SetVisualVisible(bool visible)
    {
        _visualVisible = visible;
        ApplyVisible();
    }

    private void ApplyVisible() => gameObject.SetActive(HasPoint && _visualVisible);

    private void Build()
    {
        _material = CreateLineMaterial();
        if (_material == null) return;

        _ring = CreateLine("Ring", Segments, 0.08f);
        _ring.loop = true;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0.05f, Mathf.Sin(angle) * Radius));
        }

        _beam = CreateLine("Beam", 2, 0.05f);
        _beam.SetPosition(0, new Vector3(0f, 0.05f, 0f));
        _beam.SetPosition(1, new Vector3(0f, BeamHeight, 0f));
    }

    private LineRenderer CreateLine(string goName, int count, float width)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = MarkerColor;
        line.endColor = MarkerColor;
        line.material = _material;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 0;
        return line;
    }

    /// <summary>
    /// URP 下用内置管线 shader 会显示成粉色，所以先取 URP Unlit 再退回 Sprites/Default。
    /// </summary>
    private static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            Debug.LogError("[导演] 找不到可用的线条 shader，落点标记不显示");
            return null;
        }

        var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", MarkerColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", MarkerColor);
        return mat;
    }
}
}
