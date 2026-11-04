# -*- coding: utf-8 -*-
"""通过技能脚本的内部 API 提交 LowPoly 图生3D，并把结果暂存到 Temp。

用法: gen3d_lowpoly.py submit|status --token <token>
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
OUT_DIR = r"E:\Bluedivers\Temp\gen3d_lowpoly_out"
JOB_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gen3d_lowpoly_job.json")

# 3.1 不支持 LowPoly；FaceCount 的合法下限是 10000，正好压在用户给的上限
BODY = {
    "Model": "3.0",
    "GenerateType": "LowPoly",
    "PolygonType": "triangle",
    "FaceCount": 10000,
    "ResultFormat": "FBX",
}


def _load_skill():
    spec = importlib.util.spec_from_file_location("buddy_cloud", SKILL_SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _call(mod, action, body, token):
    cfg = mod._PROVIDER_MAP["3d"]
    with contextlib.redirect_stdout(sys.stderr):
        return mod._call_api(
            mod._DEFAULT_ENDPOINT, cfg["provider"], cfg["service"],
            cfg["version"], action, body, token,
        )


def cmd_submit(mod, token):
    with open(IMAGE_PATH, "rb") as fh:
        body = dict(BODY, ImageBase64=base64.b64encode(fh.read()).decode("ascii"))
    try:
        resp = _call(mod, mod._PROVIDER_MAP["3d"]["submit_action"], body, token)
    except SystemExit:
        print(json.dumps({"error": "SUBMIT_FAILED"}, ensure_ascii=False))
        sys.exit(1)
    job_id = resp.get("JobId") if resp else None
    if not job_id:
        print(json.dumps({"error": "NO_JOB_ID", "submit": resp}, ensure_ascii=False))
        sys.exit(1)
    with open(JOB_FILE, "w", encoding="utf-8") as fh:
        json.dump({"job_id": job_id}, fh)
    print(json.dumps({"job_id": job_id, "status": "SUBMITTED", "body": BODY}, ensure_ascii=False))


def _download(mod, result):
    files = result.get("ResultFile3Ds") or []
    os.makedirs(OUT_DIR, exist_ok=True)
    saved = []
    seen = {}
    for item in files:
        url = item.get("Url")
        if not url:
            continue
        # 按 URL 真实扩展名存：Type=OBJ 的那个其实常是 zip 包
        ext = urllib.parse.urlparse(url).path.rsplit(".", 1)[-1].lower() or "bin"
        seen[ext] = seen.get(ext, 0) + 1
        name = "SciFiShotgun_LowPoly{}.{}".format("" if seen[ext] == 1 else seen[ext], ext)
        resp = mod.requests.get(url, timeout=180)
        resp.raise_for_status()
        path = os.path.join(OUT_DIR, name)
        with open(path, "wb") as fh:
            fh.write(resp.content)
        saved.append({"path": path, "type": item.get("Type"), "size": len(resp.content)})

        preview = item.get("PreviewImageUrl")
        preview_path = os.path.join(OUT_DIR, "SciFiShotgun_LowPoly_Preview.png")
        if preview and not os.path.exists(preview_path):
            rp = mod.requests.get(preview, timeout=120)
            rp.raise_for_status()
            with open(preview_path, "wb") as fh:
                fh.write(rp.content)
            saved.append({"path": preview_path, "type": "PREVIEW", "size": len(rp.content)})
    return saved


def cmd_status(mod, token):
    with open(JOB_FILE, "r", encoding="utf-8") as fh:
        job_id = json.load(fh)["job_id"]
    result = _call(mod, mod._PROVIDER_MAP["3d"]["query_action"], {"JobId": job_id}, token)
    raw = result.get("JobStatusCode")
    status = result.get("Status") or {1: "QUEUED", 2: "PROCESSING", 4: "FAIL", 5: "DONE"}.get(
        int(raw) if raw is not None else 0, "UNKNOWN"
    )
    out = {"job_id": job_id, "status": status}
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
