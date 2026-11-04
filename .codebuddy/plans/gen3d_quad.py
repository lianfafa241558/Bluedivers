# -*- coding: utf-8 -*-
"""远端 LowPoly 四边形生成（可并行提交多组 FaceCount 探测服务端行为）。

用法:
  gen3d_quad.py submit --tag q10k --face-count 10000 --token <token>
  gen3d_quad.py status --tag q10k --token <token>
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
PLAN_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_ROOT = r"E:\Bluedivers\Temp\gen3d_quad_out"


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


def cmd_submit(mod, token, tag, face_count, polygon_type):
    with open(IMAGE_PATH, "rb") as fh:
        body = {
            "ImageBase64": base64.b64encode(fh.read()).decode("ascii"),
            "Model": "3.0",  # 3.1 不支持 LowPoly
            "GenerateType": "LowPoly",
            "PolygonType": polygon_type,
            "FaceCount": face_count,
            "ResultFormat": "FBX",
        }
    try:
        resp = _call(mod, mod._PROVIDER_MAP["3d"]["submit_action"], body, token)
    except SystemExit:
        print(json.dumps({"error": "SUBMIT_FAILED", "tag": tag}, ensure_ascii=False))
        sys.exit(1)
    job_id = resp.get("JobId") if resp else None
    if not job_id:
        print(json.dumps({"error": "NO_JOB_ID", "submit": resp, "tag": tag}, ensure_ascii=False))
        sys.exit(1)
    with open(os.path.join(PLAN_DIR, "gen3d_quad_{}_job.json".format(tag)), "w", encoding="utf-8") as fh:
        json.dump({"job_id": job_id, "face_count": face_count, "polygon_type": polygon_type}, fh)
    print(json.dumps({"tag": tag, "job_id": job_id, "status": "SUBMITTED", "body": {k: v for k, v in body.items() if k != "ImageBase64"}}, ensure_ascii=False))


def cmd_status(mod, token, tag):
    with open(os.path.join(PLAN_DIR, "gen3d_quad_{}_job.json".format(tag)), "r", encoding="utf-8") as fh:
        job = json.load(fh)
    result = _call(mod, mod._PROVIDER_MAP["3d"]["query_action"], {"JobId": job["job_id"]}, token)
    raw = result.get("JobStatusCode")
    status = result.get("Status") or {1: "QUEUED", 2: "PROCESSING", 4: "FAIL", 5: "DONE"}.get(
        int(raw) if raw is not None else 0, "UNKNOWN"
    )
    out = {"tag": tag, "job_id": job["job_id"], "status": status}
    if status == "DONE":
        out_dir = os.path.join(OUT_ROOT, tag)
        os.makedirs(out_dir, exist_ok=True)
        saved = []
        seen = {}
        preview_done = False
        for item in result.get("ResultFile3Ds") or []:
            url = item.get("Url")
            if not url:
                continue
            ext = urllib.parse.urlparse(url).path.rsplit(".", 1)[-1].lower() or "bin"
            seen[ext] = seen.get(ext, 0) + 1
            name = "Gun{}.{}".format("" if seen[ext] == 1 else seen[ext], ext)
            r = mod.requests.get(url, timeout=180)
            r.raise_for_status()
            path = os.path.join(out_dir, name)
            with open(path, "wb") as fh:
                fh.write(r.content)
            saved.append({"path": path, "type": item.get("Type"), "size": len(r.content)})
            preview = item.get("PreviewImageUrl")
            if preview and not preview_done:
                rp = mod.requests.get(preview, timeout=120)
                rp.raise_for_status()
                with open(os.path.join(out_dir, "Preview.png"), "wb") as fh:
                    fh.write(rp.content)
                preview_done = True
        out["saved"] = saved
        out["result"] = result
    elif status == "FAIL":
        out["error"] = result.get("ErrorMessage", result.get("JobErrorMsg", "generation failed"))
    print(json.dumps(out, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["submit", "status"])
    parser.add_argument("--tag", required=True)
    parser.add_argument("--face-count", type=int, default=10000)
    parser.add_argument("--polygon-type", default="quadrilateral")
    parser.add_argument("--token", required=True)
    args = parser.parse_args()
    mod = _load_skill()
    if args.command == "submit":
        cmd_submit(mod, args.token, args.tag, args.face_count, args.polygon_type)
    else:
        cmd_status(mod, args.token, args.tag)


if __name__ == "__main__":
    main()
