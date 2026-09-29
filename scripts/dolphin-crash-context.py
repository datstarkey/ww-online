# Dolphin crash context reader (GZLE01, Windows).
#
# When the game dies with a JUTException screen, this reads the faulting thread's OSContext from
# JUTException's exCallbackObject (0x803ED988) straight out of the running Dolphin process, prints
# PC / LR / CTR / GPRs, symbolises DOL addresses and walks the stack. REL addresses are mapped to
# (module id, section, offset); module 0x58 is the puppet REL.
#
# usage: python scripts/dolphin-crash-context.py <dolphin pid> <tww-decomp>/config/GZLE01/symbols.txt
# It reads only the emulated RAM and the public symbol names from your own decomp checkout.
#
# Reading the stack: each line is a frame and the return address saved in it. When PC is garbage
# (a call through a freed vtable), the first frame belongs to the caller of the bad call and its
# "ret" slot is stale; the real call site is LR, and the chain is reliable from the second frame.
# Pair it with scripts/dolphin-link-anm-check.py for use-after-free crashes in Link's model data.
import ctypes, ctypes.wintypes as W, struct, sys, re, bisect
pid=int(sys.argv[1]); SYMS=sys.argv[2]
k=ctypes.WinDLL('kernel32',use_last_error=True)
h=k.OpenProcess(0x0410,False,pid)
if not h: sys.exit("OpenProcess failed")
class MBI(ctypes.Structure):
    _fields_=[("BaseAddress",ctypes.c_void_p),("AllocationBase",ctypes.c_void_p),("AllocationProtect",W.DWORD),("PartitionId",W.WORD),("RegionSize",ctypes.c_size_t),("State",W.DWORD),("Protect",W.DWORD),("Type",W.DWORD)]
def rd(addr,n):
    buf=ctypes.create_string_buffer(n); got=ctypes.c_size_t()
    return buf.raw[:got.value] if k.ReadProcessMemory(h,ctypes.c_void_p(addr),buf,n,ctypes.byref(got)) else None
mbi=MBI(); a=0; base=None
while k.VirtualQueryEx(h,ctypes.c_void_p(a),ctypes.byref(mbi),ctypes.sizeof(mbi)):
    if mbi.State==0x1000 and mbi.RegionSize in (0x2000000,0x3000000,0x4000000) and rd(mbi.BaseAddress,6)==b'GZLE01':
        base=mbi.BaseAddress; size=mbi.RegionSize; break
    a=(mbi.BaseAddress or 0)+mbi.RegionSize
    if a>=0x7FFFFFFFFFFF: break
if base is None: sys.exit("MEM1 not found")
def ok(ga): return 0x80000000<=ga<0x80000000+size
def r32(ga): return struct.unpack('>I',rd(base+(ga-0x80000000),4))[0] if ok(ga) else 0
syms=[]
for line in open(SYMS,encoding='utf-8',errors='ignore'):
    m=re.match(r'(\S+)\s*=\s*\.(\w+):0x([0-9A-Fa-f]+);',line)
    if m and m.group(2) in ('text','init'): syms.append((int(m.group(3),16),m.group(1)))
syms.sort(); sa=[x for x,_ in syms]
# loaded REL modules: OSModuleInfo list head at 0x800030C8
mods=[]; m=r32(0x800030C8); n=0
while m and ok(m) and n<400:
    mid=r32(m); nsec=r32(m+0xC); sec=r32(m+0x10)
    for i in range(nsec):
        off=r32(sec+i*8)&~1; sz=r32(sec+i*8+4)
        if off and sz: mods.append((off,off+sz,mid,i))
    m=r32(m+4); n+=1
def name(x):
    for lo,hi,mid,s in mods:
        if lo<=x<hi: return "REL module 0x%x sec %d +0x%x%s"%(mid,s,x-lo," (PUPPET REL)" if mid==0x58 else "")
    if 0x80003000<=x<0x80400000 and sa:
        i=bisect.bisect_right(sa,x)-1
        if i>=0: return "%s+0x%x"%(syms[i][1],x-syms[i][0])
    return "?"
cb=0x803ED988
err=struct.unpack('>H',rd(base+(cb+4-0x80000000),2))[0]; ctx=r32(cb+8); dsisr=r32(cb+0xC); dar=r32(cb+0x10)
print("error=%d ctx=%08x dsisr=%08x dar=%08x"%(err,ctx,dsisr,dar))
if not ok(ctx): sys.exit("no crash context yet")
raw=rd(base+(ctx-0x80000000),0x1A0)
g=struct.unpack('>32I',raw[:128]); cr,lr,ctr,xer=struct.unpack('>4I',raw[0x80:0x90]); srr0,srr1=struct.unpack('>2I',raw[0x198:0x1A0])
print("PC  %08x  %s"%(srr0,name(srr0)))
print("LR  %08x  %s"%(lr,name(lr)))
print("CTR %08x  %s"%(ctr,name(ctr)))
print("regs: "+" ".join("r%d=%08x"%(i,g[i]) for i in range(32)))
sp=g[1]
for i in range(20):
    if not ok(sp): break
    ra=r32(sp+4); print("  frame %08x ret %08x  %s"%(sp,ra,name(ra)))
    sp=r32(sp)
    if sp in (0,0xFFFFFFFF): break
print("loaded modules:",len(set(x[2] for x in mods)))
