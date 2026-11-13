"""一次性：给 Aris/Noa/Nagisa 的 AnimatorController 追加 IsCrouch/IsProne 两个 bool 参数。
只做"在 m_AnimatorParameters 列表末尾插入两项"，不动其它任何字节；保留原 BOM 与换行风格。"""
import io
import os
import sys

ROOT = r"e:\Bluedivers\Assets\Art\Anim\Student"
FILES = [r"Aris\Aris.controller", r"Noa\Noa.controller", r"Nagisa\Nagisa.controller"]

PARAMS = """  - m_Name: IsCrouch
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  - m_Name: IsProne
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
"""


def patch(path):
    with open(path, "rb") as f:
        raw = f.read()

    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"

    if "m_Name: IsCrouch" in text:
        return path, "skip: already has IsCrouch"

    if "  m_AnimatorParameters: []" in text:
        return path, "ERROR: empty parameter list (needs manual format)"

    idx = text.find("  m_AnimatorParameters:")
    if idx < 0:
        return path, "ERROR: no m_AnimatorParameters"

    end = text.find("  m_AnimatorLayers:", idx)
    if end < 0:
        return path, "ERROR: no m_AnimatorLayers after params"

    block = PARAMS.replace("\n", nl)
    new_text = text[:end] + block + text[end:]

    out = b"\xef\xbb\xbf" if has_bom else b""
    out += new_text.encode("utf-8")
    with open(path, "wb") as f:
        f.write(out)
    return path, "ok (bom=%s, nl=%s, +%d chars)" % (has_bom, repr(nl), len(new_text) - len(text))


def main():
    for rel in FILES:
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            print("MISSING", p)
            continue
        print(*patch(p))


if __name__ == "__main__":
    main()
