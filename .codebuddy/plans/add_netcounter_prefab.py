# -*- coding: utf-8 -*-
"""往 PlayerWnd.prefab 里加一个 NetCounter 文本节点（帧数旁边的网络帧数/延迟显示）。

- FramerateCounter 是现成的 TMP 节点，这里**复制**它的 Node 结构（GO/RectTransform/CanvasRenderer/TMP），
  换新 fileID、改名字与默认文本、往父节点 m_Children 里追加、并给 PlayerWnd 组件补 netCounter 字段。
- 字节级读写：保持 LF 换行、无 BOM（replace_in_file 有把整份文件转成 CRLF 的坑）。
"""
import io
import os

PREFAB = os.path.join(os.path.dirname(__file__), "..", "..",
                      "Assets", "Resources", "UI", "Wnd", "PlayerWnd.prefab")

GO_ID = 7712993417266445121
RT_ID = 7712993417266445122
CR_ID = 7712993417266445123
TMP_ID = 7712993417266445124

FRAMERATE_RT = "7187979962102941013"
FRAMERATE_FATHER = "534783193439482779"

NEW_BLOCKS = f"""--- !u!1 &{GO_ID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {RT_ID}}}
  - component: {{fileID: {CR_ID}}}
  - component: {{fileID: {TMP_ID}}}
  m_Layer: 5
  m_Name: NetCounter
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{RT_ID}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ID}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {FRAMERATE_FATHER}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -30}}
  m_SizeDelta: {{x: 160, y: 30}}
  m_Pivot: {{x: 0, y: 1}}
--- !u!222 &{CR_ID}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ID}}}
  m_CullTransparentMesh: 0
"""

TMP_BLOCK_TEMPLATE = """--- !u!114 &{tmp_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: 0Hz --
  m_isRightToLeft: 0
  m_fontAsset: {{fileID: 11400000, guid: 05abcb1fd3030854b8e966557a3ae494, type: 2}}
  m_sharedMaterial: {{fileID: -7320757765811917448, guid: 05abcb1fd3030854b8e966557a3ae494,
    type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4294967295
  m_fontColor: {{r: 1, g: 1, b: 1, a: 1}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 16
  m_fontSizeBase: 16
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_enableWordWrapping: 1
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 1
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 1
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
"""


def main():
    path = os.path.abspath(PREFAB)
    with io.open(path, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8")

    assert "771299341726644" not in text, "fileID 已存在，脚本重复执行过？"

    # ① 父节点 m_Children 里把新 RectTransform 挂在 FramerateCounter 之后
    child_anchor = "  - {fileID: %s}\n" % FRAMERATE_RT
    assert text.count(child_anchor) == 1
    text = text.replace(child_anchor, child_anchor + "  - {fileID: %d}\n" % RT_ID)

    # ② PlayerWnd 组件字段
    field_anchor = "  framerateCounter: {fileID: %s}\n" % FRAMERATE_RT
    assert text.count(field_anchor) == 1
    text = text.replace(field_anchor, field_anchor + "  netCounter: {fileID: %d}\n" % RT_ID)

    # ③ 节点本体：插在 FramerateCounter 那段之后（以其后第一个 GameObject 头为锚点）
    node_anchor = "--- !u!1 &2299265587685578026\n"
    assert text.count(node_anchor) == 1
    blocks = NEW_BLOCKS + TMP_BLOCK_TEMPLATE.format(tmp_id=TMP_ID, go_id=GO_ID)
    text = text.replace(node_anchor, blocks + node_anchor)

    with io.open(path, "wb") as f:
        f.write(text.encode("utf-8"))
    print("ok: NetCounter 已写入", path)


if __name__ == "__main__":
    main()
