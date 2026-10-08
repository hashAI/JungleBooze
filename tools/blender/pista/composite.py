"""Numpy compositing for step 6: rope clone, roughness fixes, delight check, albedo floor, packing, resizing.

All inputs are Blender float image buffers (rows bottom-up, RGBA, linear values for emission bakes).
Needs numpy, scipy and Pillow in the Python that runs Blender (see art_source/pista/README.md).
"""
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage

ALBEDO_FLOOR = 30 / 255      # ART_DIRECTION 10.2: darkest natural albedo 30-50 sRGB
ALBEDO_TOE = 48 / 255        # values below this are lifted smoothly toward the floor
ALBEDO_CEIL = 240 / 255


def srgb_encode(x):
    x = np.clip(x, 0, 1)
    return np.where(x <= 0.0031308, 12.92 * x, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def srgb_decode(x):
    return np.where(x <= 0.04045, x / 12.92, np.power((x + 0.055) / 1.055, 2.4))


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rc = np.where(m, (mx - r) / np.where(m, d, 1), 0)
    gc = np.where(m, (mx - g) / np.where(m, d, 1), 0)
    bc = np.where(m, (mx - b) / np.where(m, d, 1), 0)
    h = np.where(r == mx, bc - gc, np.where(g == mx, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(m, (h / 6.0) % 1.0, 0)
    s = np.where(mx > 1e-6, d / np.where(mx > 1e-6, mx, 1), 0)
    return h, s, mx


def soft(mask, sigma):
    return np.clip(ndimage.gaussian_filter(mask.astype(np.float32), sigma), 0, 1)


def resize(a, size):
    """Lanczos resize of an HxWxC float array (top-down rows)."""
    ch = [np.asarray(Image.fromarray(a[..., c].astype(np.float32), 'F').resize((size, size), Image.LANCZOS))
          for c in range(a.shape[-1])]
    return np.stack(ch, -1)


def save8(a, path, mode):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), mode).save(path, optimize=True)


def run(base, base_m, rmap, rmap_m, nrm, nrm_m, zones1, zones2, ao, out_dir, log, rot=None, zones3=None):
    os.makedirs(out_dir, exist_ok=True)
    R = base.shape[0]
    valid = base[..., 3] > 0.5
    valid_m = base_m[..., 3] > 0.5
    report = {}

    # --- rope clone mask --------------------------------------------------------------------------------------
    srgb = srgb_encode(base[..., :3])
    h, s, v = rgb_to_hsv(srgb)
    # rope texture = tan/saturated strands, or any smear clearly lighter than the charcoal trouser fabric
    rope_col = ((s > 0.22) & (v > 0.25) & (h > 0.04) & (h < 0.17)) | (v > 0.27)
    hip = zones1[..., 1] > 0.5
    m_col = ndimage.binary_dilation(rope_col & hip, iterations=max(2, R // 700)) & hip
    m = np.maximum(zones1[..., 0], m_col.astype(np.float32))
    m = np.maximum(m, ndimage.binary_fill_holes(m > 0.5).astype(np.float32))   # no unmasked islands inside
    m = soft(m, R / 1024) * valid_m
    report['rope_clone_texels'] = int((m > 0.5).sum())
    log(f'rope clone mask: {report["rope_clone_texels"]} texels > 0.5')

    def lerp(a, b, t):
        return a * (1 - t[..., None]) + b * t[..., None]

    alb = lerp(base[..., :3], base_m[..., :3], m)
    rm = lerp(rmap[..., :3], rmap_m[..., :3], m)
    nn = lerp(nrm[..., :3] * 2 - 1, nrm_m[..., :3] * 2 - 1, m)

    # --- side-seam smear on both hips: clone from the rotated copy of the same thigh ------------------------
    if rot:
        for ch, side in ((0, 'R'), (1, 'L')):
            b_, r_, n_ = rot[side]
            t = soft(zones3[..., ch], R / 2048) * (b_[..., 3] > 0.5)
            alb = lerp(alb, b_[..., :3], t)
            rm = lerp(rm, r_[..., :3], t)
            nn = lerp(nn, n_[..., :3] * 2 - 1, t)
            report[f'seam_clone_{side}_texels'] = int((t > 0.5).sum())
        log(f'seam clone texels R/L: {report["seam_clone_R_texels"]}/{report["seam_clone_L_texels"]}')

    # --- AO (baked at lower res) ----------------------------------------------------------------------------
    aov = ao[..., 0]
    if aov.shape[0] != R:
        aov = ndimage.zoom(aov, R / aov.shape[0], order=1)
    aov = np.clip(aov, 0, 1)

    # --- delight check: does albedo get darker where the geometry is occluded? -------------------------------
    lum = (0.2126 * alb[..., 0] + 0.7152 * alb[..., 1] + 0.0722 * alb[..., 2])
    w = valid.astype(np.float32)
    sig = R / 256
    lum_b = ndimage.gaussian_filter(lum * w, sig) / np.maximum(ndimage.gaussian_filter(w, sig), 1e-4)
    rel = lum / np.maximum(lum_b, 1e-4)
    sel = valid & (lum_b > 0.01) & (zones2[..., 2] < 0.5)    # skip the ponytail (strand gaps are real)
    x = aov[sel]; y = np.clip(rel[sel], 0, 3)
    slope = float(np.polyfit(x, y, 1)[0]) if x.size > 100 else 0.0
    corr = float(np.corrcoef(x, y)[0, 1]) if x.size > 100 else 0.0
    occl = sel & (aov < 0.6)
    open_ = sel & (aov > 0.9)
    ratio = float(np.median(rel[occl]) / np.median(rel[open_])) if occl.any() and open_.any() else 1.0
    report.update(delight_slope=slope, delight_corr=corr, occluded_vs_open_albedo=ratio)
    log(f'delight check: AO-vs-relative-albedo slope {slope:.3f}, corr {corr:.3f}, '
        f'occluded/open median albedo {ratio:.3f}')
    k = float(np.clip(slope, 0, 0.5)) if (slope > 0.15 and corr > 0.15) else 0.0
    if k > 0:
        alb = alb / np.maximum(1 - k * (1 - aov[..., None]), 0.5)
        log(f'delight correction applied: albedo /= 1 - {k:.2f} * (1 - AO)')
    report['delight_correction_k'] = k

    # --- roughness by material zone (ART_DIRECTION 10.2) -----------------------------------------------------
    srgb = srgb_encode(alb)
    h, s, v = rgb_to_hsv(srgb)
    head, arms, belt, pony = zones1[..., 2] > 0.5, zones2[..., 0] > 0.5, zones2[..., 1] > 0.5, zones2[..., 2] > 0.5
    skin = (head | arms) & ((h < 0.11) | (h > 0.95)) & (s > 0.15) & (s < 0.65) & (v > 0.40)
    hair = pony | (head & (v < 0.33) & (h < 0.15))
    leather = belt & (h > 0.03) & (h < 0.12) & (s > 0.40) & (v > 0.20) & (v < 0.65)
    r0 = rm[..., 0]
    med = float(np.median(r0[valid]))
    var = np.clip(r0 - med, -0.15, 0.15) * 0.5
    rough = r0.copy()
    for mask, target in ((skin, 0.52), (hair, 0.48), (leather, 0.62)):
        f = soft(mask, R / 2048)
        rough = rough * (1 - f) + (target + var) * f
    rough = np.clip(rough, 0.30, 0.97)
    metal = np.clip(rm[..., 1], 0, 1)
    for name, mask in (('skin', skin), ('hair', hair), ('leather', leather)):
        report[f'roughness_{name}_median'] = float(np.median(rough[mask & valid])) if (mask & valid).any() else None
    report['roughness_other_median'] = float(np.median(rough[valid & ~skin & ~hair & ~leather]))
    report['metallic_texels_over_0.5'] = int((metal[valid] > 0.5).sum())

    # --- albedo floor/ceiling in sRGB ---------------------------------------------------------------------
    srgb = srgb_encode(alb)
    L = 0.2126 * srgb[..., 0] + 0.7152 * srgb[..., 1] + 0.0722 * srgb[..., 2]
    Lt = np.where(L < ALBEDO_TOE, ALBEDO_FLOOR + (ALBEDO_TOE - ALBEDO_FLOOR) * (L / ALBEDO_TOE), L)
    srgb = np.clip(srgb * (Lt / np.maximum(L, 1e-4))[..., None], 0, 1)
    over = srgb.max(-1) > ALBEDO_CEIL
    srgb[over] *= (ALBEDO_CEIL / srgb.max(-1)[over])[..., None]
    L2 = 0.2126 * srgb[..., 0] + 0.7152 * srgb[..., 1] + 0.0722 * srgb[..., 2]
    report['albedo_srgb_p0.1_p50_p99.9'] = [float(np.percentile(L2[valid], q)) * 255 for q in (0.1, 50, 99.9)]
    report['albedo_texels_lifted'] = int((L[valid] < ALBEDO_TOE).sum())
    log(f'albedo sRGB luminance p0.1/p50/p99.9: {report["albedo_srgb_p0.1_p50_p99.9"]}')

    # --- normals ------------------------------------------------------------------------------------------
    nn = nn / np.maximum(np.linalg.norm(nn, axis=-1, keepdims=True), 1e-6)
    nn[~valid] = (0, 0, 1)

    # --- write (Blender rows are bottom-up; images are top-down) ------------------------------------------
    maps = {
        'BaseColor': (np.flipud(srgb), 'RGB'),
        'Normal': (np.flipud(nn * 0.5 + 0.5), 'RGB'),
        'ARM': (np.flipud(np.dstack([aov, rough, metal])), 'RGB'),
    }
    for size, tag in ((2048, '2k'), (1024, '1k')):
        for name, (a, mode) in maps.items():
            r = resize(a, size)
            if name == 'Normal':
                n = r * 2 - 1
                n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
                r = n * 0.5 + 0.5
            path = os.path.join(out_dir, f'T_Pista_{name}_{tag}.png')
            save8(r, path, mode)
            log('wrote', path)
    # debug masks for review (not shipped)
    dbg = np.dstack([m, skin * 1.0 + leather * 0.5, hair * 1.0])
    save8(np.flipud(resize(dbg, 1024)), os.path.join(os.path.dirname(out_dir), '..', 'work', 'debug_masks.png'), 'RGB')
    with open(os.path.join(os.path.dirname(out_dir), '..', 'work', 'texture_report.json'), 'w') as f:
        json.dump(report, f, indent=1)
    return report


def build_material(mat, tex_dir, tag):
    """Principled material using the shipped maps. ARM's G/B feed roughness/metallic, R feeds glTF occlusion."""
    import bpy
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in ('BSDF_PRINCIPLED', 'OUTPUT_MATERIAL'):
            nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])

    def img(name, color):
        path = os.path.join(tex_dir, f'T_Pista_{name}_{tag}.png')
        im = bpy.data.images.get(os.path.basename(path)) or bpy.data.images.load(path)
        im.reload()
        im.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = im
        return n

    b = img('BaseColor', True)
    nt.nodes.active = b   # viewport/workbench texture preview
    nt.links.new(b.outputs['Color'], bsdf.inputs['Base Color'])
    arm = img('ARM', False)
    sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(arm.outputs['Color'], sep.inputs[0])
    nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
    nt.links.new(sep.outputs['Blue'], bsdf.inputs['Metallic'])
    nm = img('Normal', False)
    nmap = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nm.outputs['Color'], nmap.inputs['Color'])
    nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    # glTF exporter convention: a node group named 'glTF Material Output' with an 'Occlusion' input
    grp = bpy.data.node_groups.get('glTF Material Output')
    if grp is None:
        grp = bpy.data.node_groups.new('glTF Material Output', 'ShaderNodeTree')
        grp.interface.new_socket('Occlusion', in_out='INPUT', socket_type='NodeSocketFloat')
    g = nt.nodes.new('ShaderNodeGroup'); g.node_tree = grp
    nt.links.new(sep.outputs['Red'], g.inputs['Occlusion'])
