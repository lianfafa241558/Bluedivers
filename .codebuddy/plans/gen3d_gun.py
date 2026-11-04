# -*- coding: utf-8 -*-
"""按概念图生成 3D 模型并落到 Assets/Art/Modle/Weapon。

技能自带 CLI 只暴露文生3D，这里复用它的签名与 API 函数补上 ImageBase64 路径。
用法: gen3d_gun.py submit|status --token <token>
"""
import argparse
import base64
import contextlib
import importlib.util
import json
import os
import sys
import urllib.parse

SKILL_SCRIPT = r"c:\users\admin\appdata\local\microsoft\visualstudio\17.0_ac2db69c\extensions\cne4j2jv.usy\Resources\vsix\extension\out\extension\builtin\buddy-multimodal-generation\scripts\buddy-cloud.py"
IMAGE_PATH = r"C:\Users\admin\AppData\Local\CodeBuddyExtension\Data\745eef27-68fa-4891-82d2-f37630042ec6\VisualStudio\745eef27-68fa-4891-82d2-f37630042ec6\history\b52f89de48dac559bdc39636e3dceded\5c10c401565547dcba723e5d728994ed\assets\image.6926462121.png"
OUT_DIR = r"E:\Bluedivers\Assets\Art\Modle\Weapon"
NAME = "SciFiShotgun"
JOB_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gen3d_gun_job.json")

PROMPT = (
    "一把科幻风格双管霰弹枪道具，硬表面机械设计，深灰黑色哑光金属机匣，长方形厚实机身，"
    "侧面有黄色斜纹警示条和小骷髅徽记，前部是两根并列粗大枪管与带矩形散热槽的泵动护木，"
    "顶部后方竖起折叠天线式机械瞄具，下方是带防滑纹理的手枪握把与方形镂空扳机护圈，"
    "分件清晰、倒角锐利，游戏道具级建模，纯色浅灰背景侧视图"
)


def _load_skill():
    spec = importlib.util.spec_from_file_location("buddy_cloud", SKILL_SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _call(mod, action, body, token):
    cfg = mod._PROVIDER_MAP["3d"]
    # _call_api 出错时会把错误 JSON 打到 stdout，重定向以免污染结果解析
    with contextlib.redirect_stdout(sys.stderr):
        return mod._call_api(
            mod._DEFAULT_ENDPOINT, cfg["provider"], cfg["service"],
            cfg["version"], action, body, token,
        )


def _submit_once(mod, body, token):
    try:
        return _call(mod, mod._PROVIDER_MAP["3d"]["submit_action"], body, token)
    except SystemExit:
        return None


def cmd_submit(mod, token):
    with open(IMAGE_PATH, "rb") as fh:
        image_b64 = base64.b64encode(fh.read()).decode("ascii")

    body = {"ImageBase64": image_b64, "Model": "3.1", "ResultFormat": "FBX"}
    resp = _submit_once(mod, body, token)
    mode = "image"
    if not resp or not resp.get("JobId"):
        print("[WARN] image-to-3d rejected, fallback to text-to-3d", file=sys.stderr)
        resp = _submit_once(mod, mod._build_3d_body(PROMPT, "3.1"), token)
        mode = "text"
    if not resp or not resp.get("JobId"):
        print(json.dumps({"error": "NO_JOB_ID", "submit": resp}, ensure_ascii=False))
        sys.exit(1)

    job_id = resp["JobId"]
    with open(JOB_FILE, "w", encoding="utf-8") as fh:
        json.dump({"job_id": job_id, "mode": mode}, fh)
    print(json.dumps({"job_id": job_id, "mode": mode, "status": "SUBMITTED"}, ensure_ascii=False))


def _save(mod, url, path):
    resp = mod.requests.get(url, timeout=180)
    resp.raise_for_status()
    with open(path, "wb") as fh:
        fh.write(resp.content)
    return path


def _download(mod, result):
    files = list(result.get("ResultFile3Ds") or [])
    if not files:
        for key in ("ModelUrl", "ResultModelUrl", "ResultUrl"):
            val = result.get(key)
            if isinstance(val, str) and val:
                files = [{"Url": val, "Type": ""}]
                break
            if isinstance(val, list):
                files = [{"Url": u, "Type": ""} for u in val if isinstance(u, str)]
                break
    if not files:
        return []

    os.makedirs(OUT_DIR, exist_ok=True)
    saved = []
    preview_saved = False
    for index, item in enumerate(files):
        url = item.get("Url")
        if not url:
            continue
        raw_type = (item.get("Type") or "").lower().lstrip(".")
        if not raw_type:
            raw_type = urllib.parse.urlparse(url).path.rsplit(".", 1)[-1].lower()
        ext = "fbx" if "fbx" in raw_type else raw_type
        if not ext:
            ext = "glb"
        suffix = "" if index == 0 else str(index)
        saved.append(_save(mod, url, os.path.join(OUT_DIR, "{}{}.{}".format(NAME, suffix, ext))))

        preview = item.get("PreviewImageUrl")
        if preview and not preview_saved:
            saved.append(_save(mod, preview, os.path.join(OUT_DIR, NAME + "_Preview.png")))
            preview_saved = True
    return saved


def cmd_status(mod, token):
    with open(JOB_FILE, "r", encoding="utf-8") as fh:
        job = json.load(fh)
    result = _call(mod, mod._PROVIDER_MAP["3d"]["query_action"], {"JobId": job["job_id"]}, token)
    raw_code = result.get("JobStatusCode")
    status = result.get("Status") or {1: "QUEUED", 2: "PROCESSING", 4: "FAIL", 5: "DONE"}.get(
        int(raw_code) if raw_code is not None else 0, "UNKNOWN"
    )
    out = {"job_id": job["job_id"], "mode": job["mode"], "status": status}
    if status == "DONE":
        out["saved"] = _download(mod, result)
        out["result"] = result
    elif status == "FAIL":
        out["error"] = result.get("ErrorMessage", result.get("JobErrorMsg", "generation failed"))
    print(json.dumps(out, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["submit", "status"])
    parser.add_argument("--token", required=True)
    args = parser.parse_args()
    mod = _load_skill()
    if args.command == "submit":
        cmd_submit(mod, args.token)
    else:
        cmd_status(mod, args.token)


if __name__ == "__main__":
    main()
