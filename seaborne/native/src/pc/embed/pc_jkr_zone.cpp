// libtww: a malloc zone that owns JKRHeap blocks, so free() and realloc() of one go to JKRHeap::free.
//
// In seaborne the executable's global operator new and delete (pc_jkr_heap.cpp) also serve
// libc++.dylib: dyld binds every image's weak operator new to the executable's. A dlopen'ed libtww cannot
// do that for images already loaded, so libc++'s out-of-line code (std::string growth, std::filesystem)
// frees with free() a block that libtww's inline code took from the current JKRHeap: "pointer being freed
// was not allocated" in PADInit's keyboard-binding path. free() asks each registered zone's size() whose
// pointer it is; this zone answers for the blocks of the JKRHeap tree. The other direction already
// works: operator delete hands a pointer no JKRHeap owns back to free() (pc_jkr_heap.cpp).
#include "pc_internal.h"

#include "JSystem/JKernel/JKRHeap.h"

#if defined(__APPLE__)
#include <malloc/malloc.h>
#include <mach/mach.h>

#include <cstring>

namespace {

malloc_zone_t sZone;
malloc_introspection_t sIntrospect;

JKRHeap* owner(const void* ptr) {
    return ptr != nullptr ? JKRHeap::findFromRoot(const_cast<void*>(ptr)) : nullptr;
}

size_t zoneSize(malloc_zone_t*, const void* ptr) {
    JKRHeap* heap = owner(ptr);
    if (heap == nullptr) {
        return 0;
    }
    const s32 size = JKRHeap::getSize(const_cast<void*>(ptr), heap);
    return size > 0 ? (size_t)size : 1;
}

void zoneFree(malloc_zone_t*, void* ptr) {
    if (JKRHeap* heap = owner(ptr)) {
        JKRHeap::free(ptr, heap);
    }
}

void zoneFreeDefiniteSize(malloc_zone_t* zone, void* ptr, size_t) {
    zoneFree(zone, ptr);
}

// The zone never hands out memory itself: anything asked of it comes from the default zone.
void* zoneMalloc(malloc_zone_t*, size_t size) {
    return malloc_zone_malloc(malloc_default_zone(), size);
}

void* zoneCalloc(malloc_zone_t*, size_t count, size_t size) {
    return malloc_zone_calloc(malloc_default_zone(), count, size);
}

void* zoneValloc(malloc_zone_t*, size_t size) {
    return malloc_zone_valloc(malloc_default_zone(), size);
}

void* zoneMemalign(malloc_zone_t*, size_t alignment, size_t size) {
    return malloc_zone_memalign(malloc_default_zone(), alignment, size);
}

// A JKRHeap block grown or shrunk: host memory from then on.
void* zoneRealloc(malloc_zone_t* zone, void* ptr, size_t size) {
    const size_t old = zoneSize(zone, ptr);
    void* fresh = malloc_zone_malloc(malloc_default_zone(), size);
    if (fresh != nullptr && ptr != nullptr) {
        memcpy(fresh, ptr, old < size ? old : size);
        zoneFree(zone, ptr);
    }
    return fresh;
}

boolean_t zoneClaimed(malloc_zone_t*, void* ptr) {
    return owner(ptr) != nullptr;
}

void zoneDestroy(malloc_zone_t*) {}

kern_return_t introspectEnumerate(task_t, void*, unsigned, vm_address_t, memory_reader_t, vm_range_recorder_t) {
    return KERN_SUCCESS;
}
size_t introspectGoodSize(malloc_zone_t*, size_t size) {
    return size;
}
boolean_t introspectCheck(malloc_zone_t*) {
    return true;
}
void introspectPrint(malloc_zone_t*, boolean_t) {}
void introspectLog(malloc_zone_t*, void*) {}
void introspectLock(malloc_zone_t*) {}
void introspectStatistics(malloc_zone_t*, malloc_statistics_t* stats) {
    memset(stats, 0, sizeof(*stats));
}
boolean_t introspectLocked(malloc_zone_t*) {
    return false;
}

} // namespace

namespace pc {

void registerJkrMallocZone() {
    static bool sRegistered = false;
    if (sRegistered) {
        return;
    }
    sRegistered = true;
    sIntrospect.enumerator = introspectEnumerate;
    sIntrospect.good_size = introspectGoodSize;
    sIntrospect.check = introspectCheck;
    sIntrospect.print = introspectPrint;
    sIntrospect.log = introspectLog;
    sIntrospect.force_lock = introspectLock;
    sIntrospect.force_unlock = introspectLock;
    sIntrospect.statistics = introspectStatistics;
    sIntrospect.zone_locked = introspectLocked;
    sZone.size = zoneSize;
    sZone.malloc = zoneMalloc;
    sZone.calloc = zoneCalloc;
    sZone.valloc = zoneValloc;
    sZone.free = zoneFree;
    sZone.realloc = zoneRealloc;
    sZone.destroy = zoneDestroy;
    sZone.zone_name = "libtww JKRHeap";
    sZone.introspect = &sIntrospect;
    sZone.version = 10;
    sZone.memalign = zoneMemalign;
    sZone.free_definite_size = zoneFreeDefiniteSize;
    sZone.claimed_address = zoneClaimed;
    malloc_zone_register(&sZone);
}

} // namespace pc

#else

namespace pc {

void registerJkrMallocZone() {}

} // namespace pc

#endif
