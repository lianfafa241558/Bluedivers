# -*- coding: utf-8 -*-
"""CmdId 号重排辅助脚本：按 old->new 映射一次性改写源码注释/日志里的旧命令号。

用法：
  python .codebuddy/plans/cmdid_remap.py           # 干跑，只打印将要改的行
  python .codebuddy/plans/cmdid_remap.py --write   # 落盘（保留原 BOM）

要点：
  - 单次扫描替换（re.sub + 函数），避免 4035->4066 与 4054->4035 这类链式覆盖。
  - (?<!\d)/(?!\d) 边界，防止吃掉日期 2026-10-12 或更长的号。
  - CmdId.cs 本身跳过（已手工重写）。
"""
import os
import re
import sys

ROOT = r'e:\Bluedivers\Assets\Scripts'
TARGET = r'e:\Bluedivers\Assets\Scripts\07NetGame\CmdId.cs'

MAP = {
    '4001': '4001', '4002': '4002', '4003': '4003', '4004': '4004',
    '4005': '4005', '4006': '4006', '4012': '4007',
    '4013': '4020', '4014': '4021', '4015': '4022', '4016': '4023',
    '4017': '4024', '4018': '4025', '4019': '4026',
    '4020': '4027', '4021': '4056', '4022': '4057', '4023': '4050',
    '4024': '4052', '4025': '4053', '4026': '4090',
    '4027': '4033', '4028': '4034', '4029': '4008', '4030': '4029',
    '4031': '4030', '4032': '4055', '4033': '4064',
    '4034': '4065', '4035': '4066', '4036': '4059', '4037': '4031',
    '4038': '4032', '4039': '4051', '4040': '4054',
    '4041': '4009', '4042': '4010', '4043': '4058', '4044': '6004',
    '4045': '6005', '4046': '4061', '4047': '4091',
    '4048': '4060', '4049': '4062', '4050': '4028', '4051': '4092',
    '4052': '4063', '4053': '4093', '4054': '4035', '4055': '4094',
    '6006': '6004', '6007': '6005',
}

PAT = re.compile(r'(?<!\d)(\d{4})(?!\d)')


def remap(text):
    def sub(m):
        return MAP.get(m.group(1), m.group(1))
    return PAT.sub(sub, text)


def main():
    write = '--write' in sys.argv
    changed_files = 0
    changed_lines = 0
    for dirpath, _, files in os.walk(ROOT):
        for name in files:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(dirpath, name)
            if os.path.normcase(path) == os.path.normcase(TARGET):
                continue
            raw = open(path, 'rb').read()
            bom = raw.startswith(b'\xef\xbb\xbf')
            text = raw.decode('utf-8-sig')
            new = remap(text)
            if new == text:
                continue
            changed_files += 1
            old_lines = text.splitlines()
            new_lines = new.splitlines()
            for i, (a, b) in enumerate(zip(old_lines, new_lines), 1):
                if a != b:
                    changed_lines += 1
                    print('%s:%d: %s' % (os.path.relpath(path, ROOT), i, b.strip()[:130]))
            if write:
                data = new.encode('utf-8')
                if bom:
                    data = b'\xef\xbb\xbf' + data
                with open(path, 'wb') as f:
                    f.write(data)
                    f.flush()
                    os.fsync(f.fileno())
    print('---- files=%d lines=%d write=%s' % (changed_files, changed_lines, write))


if __name__ == '__main__':
    main()
