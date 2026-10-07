# -*- coding: utf-8 -*-
r"""[修误伤] 把 ShootMapPreView.cs 还原成 HEAD 版本。

原因：重命名脚本按 token 无差别替换，把它那个**与联机无关**的 public 方法 `StartTask()`
也改成了 `ConfirmTask()` —— 而该方法是被场景 UnityEvent **按名字**调用的
（`Assets/Scene/Teach.unity:24319`、`Assets/Scene/BattleScene.unity:2392` ⇒ 改了就会静默失效）。
用只读的 `git show` 取 HEAD 内容后写回（不碰索引，也不动其它文件）。

用法： python -X utf8 .codebuddy/plans/restore_shootmap_preview.py
"""
import io
import os
import subprocess

ROOT = r'd:\Pro\Bluedivers'
REL = 'Assets/Scripts/10_Effect/ShootMapPreView.cs'


def main():
    blob = subprocess.run(['git', '--no-pager', 'show', 'HEAD:' + REL],
                          cwd=ROOT, stdout=subprocess.PIPE, check=True).stdout
    path = os.path.join(ROOT, REL.replace('/', os.sep))
    with open(path, 'wb') as f:
        f.write(blob)
    crlf = blob.count(b'\r\n')
    print('已还原 %s：%d 字节，CRLF=%d，NUL=%d' % (REL, len(blob), crlf, blob.count(b'\x00')))
    print('含 public void StartTask() =', b'public void StartTask()' in blob)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
