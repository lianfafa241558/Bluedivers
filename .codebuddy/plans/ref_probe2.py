"""探查参考图通道/alpha，并给出几种候选分割的覆盖率，挑可行方案。"""
from PIL import Image
import numpy as np

REF = "E:/Bluedivers/.codebuddy/ref/ref_render.png"
im = Image.open(REF)
print("mode", im.mode, "size", im.size, "info", {k: v for k, v in im.info.items() if k != "icc_profile"})
a = np.array(im)
print("shape", a.shape)
if a.ndim == 3 and a.shape[2] == 4:
    al = a[:, :, 3]
    print("alpha stats min", al.min(), "max", al.max(), "unique<10:", sorted(set(np.unique(al).tolist()))[:10])
    print("opaque ratio %.2f%%" % (100.0 * (al > 200).mean()))
    np.save("E:/Bluedivers/.codebuddy/out/ref_alpha.npy", al > 200)
else:
    print("no alpha channel")
