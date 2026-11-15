import re

s = open('Assets/Art/Anim/Student/Noa/Noa.controller', encoding='utf-8').read()

# AnimatorController doc
i = s.find('AnimatorController:')
j = s.find('\n--- !u!', i)
ctrl = s[i:j]
print('==== AnimatorController (layers) ====')
print(ctrl[:4000])
