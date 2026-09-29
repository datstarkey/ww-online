# Read-only check of a running Dolphin (GZLE01): does every animator pointer in the "Link"
# archive's shared model data belong to the LOCAL Link? Walks the same words as
# GameMod/src/puppet_link/puppet_sharedanm.c (each material's mMaterialAnm, the J3DMaterialAnm's
# 26 slots, joint 0's mMtxCalc) and sorts each non-zero pointer by owner: the local Link's actor
# heap / item heaps / item anime heap, the Link archive's own data heap, or FOREIGN (a puppet's
# heap, another actor's (a bomb or arrow registers its own animator on the same model data), or
# freed memory: the use-after-free that crashed J3DJoint::entryIn).
# With the puppet_sharedanm fix, no FOREIGN pointer may point into a live or deleted puppet's heap
# (compare with the "[PUPPET] shared anm" lines in the Dolphin log); cl.bdl's (bdl 0x18) must
# always be the local Link's.
#
# usage: python scripts/dolphin-link-anm-check.py <dolphin pid>
# Offsets: tww-decomp d_com_inf_game.h / d_resorce.h / J3D headers, verified against the GZLE01 DOL
# (see puppet_sharedanm.c). Reads only the emulated RAM; writes nothing.
import ctypes, ctypes.wintypes as W, struct, sys

GAMEINFO = 0x803C4C08
PLAYER0 = GAMEINFO + 0x5B44          # dComIfGp_getPlayer(0) (DOL 0x800F1034)
OBJECT_INFO = GAMEINFO + 0x1BFC0     # dRes_info_c[64] (GAMEINFO_RES_OBJECT_INFO)
BDL_RANGES = [(0x13, 0x29), (0x2C, 0x2F), (0x32, 0x34), (0x37, 0x4E)]  # Link.h BDL/BDLC/BDLI/BDLM
VT_MODELDATA = 0x8039EA34

pid = int(sys.argv[1])
k = ctypes.WinDLL('kernel32', use_last_error=True)
h = k.OpenProcess(0x0410, False, pid)
if not h: sys.exit("OpenProcess failed")
class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p), ("AllocationProtect", W.DWORD),
                ("PartitionId", W.WORD), ("RegionSize", ctypes.c_size_t), ("State", W.DWORD), ("Protect", W.DWORD), ("Type", W.DWORD)]
def rd(addr, n):
    buf = ctypes.create_string_buffer(n); got = ctypes.c_size_t()
    return buf.raw[:got.value] if k.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) else None
mbi = MBI(); a = 0; base = None
while k.VirtualQueryEx(h, ctypes.c_void_p(a), ctypes.byref(mbi), ctypes.sizeof(mbi)):
    if mbi.State == 0x1000 and mbi.RegionSize in (0x2000000, 0x3000000, 0x4000000) and rd(mbi.BaseAddress, 6) == b'GZLE01':
        base = mbi.BaseAddress; size = mbi.RegionSize; break
    a = (mbi.BaseAddress or 0) + mbi.RegionSize
    if a >= 0x7FFFFFFFFFFF: break
if base is None: sys.exit("MEM1 not found")
def ok(p): return 0x80000000 <= p < 0x80000000 + size and (p & 3) == 0
def r32(ga): return struct.unpack('>I', rd(base + (ga - 0x80000000), 4))[0] if ok(ga) else 0
def r16(ga): return struct.unpack('>H', rd(base + (ga - 0x80000000), 2))[0] if ok(ga & ~3) else 0
def heap_range(heap): return (r32(heap + 0x30), r32(heap + 0x34)) if ok(heap) else None  # JKRHeap mStart/mEnd

link = r32(PLAYER0)
if not ok(link): sys.exit("no local Link")
owners = []
for name, off in (("local actor heap", 0xF0), ("local item heap 0", 0x2E90), ("local item heap 1", 0x2E94), ("local item anime heap", 0x2ECC)):
    rg = heap_range(r32(link + off))
    if rg: owners.append((name, rg))
info = None
for i in range(64):
    e = OBJECT_INFO + i * 0x24
    if r16(e + 0x0E) and rd(base + (e - 0x80000000), 5) == b'Link\0':
        info = e; break
if info is None: sys.exit("Link archive not loaded")
rg = heap_range(r32(info + 0x1C))
if rg: owners.append(("Link archive", rg))
res = r32(info + 0x20)

def owner(p):
    for name, (lo, hi) in owners:
        if lo <= p < hi: return name
    return "FOREIGN"

counts = {}; foreign = []; words = [0]
def note(where, p):
    words[0] += 1
    if p == 0: return
    o = owner(p); counts[o] = counts.get(o, 0) + 1
    if o == "FOREIGN": foreign.append((where, p))
for lo, hi in BDL_RANGES:
    for idx in range(lo, hi + 1):
        md = r32(res + 4 * idx)
        if not ok(md) or r32(md) != VT_MODELDATA: continue
        mats = r32(md + 0x60)
        for m in range(r16(md + 0x5C)):
            mat = r32(mats + 4 * m)
            if not ok(mat): continue
            anm = r32(mat + 0x3C)
            note("bdl 0x%02X mat %d mMaterialAnm" % (idx, m), anm)
            if ok(anm):
                for s in range(26):
                    note("bdl 0x%02X mat %d anm slot %d" % (idx, m, s), r32(anm + 4 + 4 * s))
        joints = r32(md + 0x2C)
        if r16(md + 0x28) and ok(joints):
            note("bdl 0x%02X joint 0 mMtxCalc" % idx, r32(r32(joints) + 0x58))
print("tracked words %d (puppet_sharedanm.c logs the same count as words=)" % words[0])
print("local Link %08x; owners: %s" % (link, ", ".join("%s [%08x,%08x)" % (n, lo, hi) for n, (lo, hi) in owners)))
for o, c in sorted(counts.items()): print("  %-22s %d" % (o, c))
for where, p in foreign: print("  FOREIGN %-40s %08x" % (where, p))
print("OK: nothing points outside the local Link / archive" if not foreign else "%d FOREIGN pointer(s)" % len(foreign))
