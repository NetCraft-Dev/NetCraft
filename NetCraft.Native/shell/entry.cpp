//Entry point of the NetCraft native layer
//The CLR loads this library as a profiler when the CORECLR_* environment variables are present, which happens before
//any managed code in the process has run; that makes the first callback the right place to install the handler that
//watches for a stack overflow, and nothing here needs the managed side to be up yet

#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <map>
#include <mutex>
#include <set>
#include <utility>
#include <unknwn.h>
#include <cor.h>
#include <corprof.h>

#include "com_base.h"

//The CLSID the CORECLR_PROFILER variable must carry
//ProfilerRelaunch writes the same value on the managed side and the two have to stay in step
static const GUID ClsidNetCraftNative = {
    0x2B5F8D34, 0x6C1A, 0x4E27, {0x9B, 0x3D, 0x52, 0xE8, 0x71, 0x4A, 0xC6, 0x18}};

static const IID IidCorProfilerInfo = {
    0x28B5557D, 0x3F3F, 0x48B4, {0x90, 0xB2, 0x5F, 0x9E, 0xEA, 0x2F, 0x6C, 0x48}};
static const IID IidMetaDataImport = {
    0x7DAC8207, 0xD3AE, 0x4C75, {0x9B, 0x67, 0x92, 0x80, 0x1A, 0x49, 0x7D, 0x44}};
static const IID IidMetaDataEmit = {
    0xBA3FEE4C, 0xECB9, 0x4E41, {0x83, 0xB7, 0x18, 0x3F, 0xA4, 0x1C, 0xD8, 0x59}};
//The reference table is not on the plain import; that interface only offers it through this one
static const IID IidMetaDataAssemblyImport = {
    0xEE62470B, 0xE94B, 0x424E, {0x9B, 0x7C, 0x2F, 0x00, 0xC9, 0x24, 0x9F, 0x93}};

//IIDs are written out as file-local constants instead of __uuidof, which is an MSVC extension that cannot see the uuid
//of interfaces the platform headers only declare; spelling them out avoids depending on any external symbol
static const IID IidUnknown = {
    0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
static const IID IidClassFactory = {
    0x00000001, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
static const IID IidProfilerCallback = {
    0x176FBED1, 0xA55C, 0x4796, {0x98, 0xCA, 0xA9, 0xDA, 0x0E, 0xF8, 0x83, 0xE7}};
static const IID IidProfilerCallback2 = {
    0x8A8CC829, 0xCCF2, 0x49FE, {0xBB, 0xAE, 0x0F, 0x02, 0x22, 0x28, 0x07, 0x1A}};
static const IID IidProfilerCallback3 = {
    0x4FD2ED52, 0x7731, 0x4B8D, {0x94, 0x69, 0x03, 0xD2, 0xCC, 0x30, 0x86, 0xC5}};
static const IID IidProfilerCallback4 = {
    0x7B63B2E3, 0x107D, 0x4D48, {0xB2, 0xF6, 0xF6, 0x1E, 0x22, 0x94, 0x70, 0xD2}};
static const IID IidProfilerCallback5 = {
    0x8DFBA405, 0x8C9F, 0x45F8, {0xBF, 0xFA, 0x83, 0xB1, 0x4C, 0xEF, 0x78, 0xB5}};
static const IID IidProfilerCallback6 = {
    0xFC13DF4B, 0x4448, 0x4F4F, {0x95, 0x0C, 0xBA, 0x8D, 0x19, 0xD0, 0x0C, 0x36}};
static const IID IidProfilerCallback7 = {
    0xF76A2DBA, 0x1D52, 0x4539, {0x86, 0x6C, 0x2A, 0xA5, 0x18, 0xF9, 0xEF, 0xC3}};
static const IID IidProfilerCallback8 = {
    0x5BED9B15, 0xC079, 0x4D47, {0xBF, 0xE2, 0x21, 0x5A, 0x14, 0x0C, 0x07, 0xE0}};
static const IID IidProfilerCallback9 = {
    0x27583EC3, 0xC8F5, 0x482F, {0x80, 0x52, 0x19, 0x4B, 0x8C, 0xE4, 0x70, 0x5A}};
static const IID IidProfilerCallback10 = {
    0xCEC5B60E, 0xC69C, 0x495F, {0x87, 0xF6, 0x84, 0xD2, 0x8E, 0xE1, 0x6F, 0xFB}};

//SameIid compares two interface ids byte by byte
static bool SameIid(REFIID left, const IID& right)
{
    return memcmp(&left, &right, sizeof(IID)) == 0;
}

//The Rust side owns every decision; the shell only marshals
extern "C" {
int32_t ncn_on_initialize(void* profiler_info_unknown);
void ncn_on_shutdown();
int32_t ncn_on_jit_compiled(size_t function_id);
void ncn_on_jit_notifications(bool taken);
int32_t ncn_should_rewrite_module(const char* simple_name, int32_t ecosystem_reference);
int32_t ncn_stack_guard_ready();
void ncn_on_injected();
void ncn_on_module_seen(const char* simple_name, int32_t accepted);
int32_t ncn_com_get_class_object(const GUID* rclsid, const GUID* riid, void** ppv_object);
int32_t ncn_com_can_unload_now();
}

//The profiling interface, kept for the lifetime of the process
//SetEventMask has to be called from Initialize and the callbacks arrive on runtime threads afterwards
static ICorProfilerInfo* g_profiler_info = nullptr;

//SetNeedJitNotifications turns the JIT callback on and reports whether the runtime accepted the mask
//Without it no method is reported and there is nothing to rewrite, so a failure here is worth surfacing
static bool SetJitNotifications()
{
    if (g_profiler_info == nullptr)
        return false;

    //This mask is notification only; unlike its neighbours it does not disable inlining or optimizations by itself
    const HRESULT hr = g_profiler_info->SetEventMask(COR_PRF_MONITOR_JIT_COMPILATION);
    return SUCCEEDED(hr);
}

//---- early stack check injection ----

//How much stack has to be left over for the throw, the unwind and whatever handles it to run
static const uintptr_t StackGuardReserveBytes = 128 * 1024;

//Once a thread has been told its stack is short it stays quiet until the stack has grown back
//Without this the check fires on every frame the unwind and the handler pass through, and the report never gets made
static thread_local bool g_stack_guard_quiet = false;

//NcnStackGuard answers whether the caller should raise the stack exception
//The remaining space is measured from the address of this frame down to the bottom of the thread's stack
extern "C" int32_t ncn_stack_guard_impl()
{
    ULONG_PTR low = 0;
    ULONG_PTR high = 0;
    GetCurrentThreadStackLimits(&low, &high);
    if (low == 0)
        return 0;

    const uintptr_t here = reinterpret_cast<uintptr_t>(_AddressOfReturnAddress());
    const uintptr_t remaining = here > low ? here - low : 0;

    //Well clear of the reserve, so the guard is armed again for the next descent
    if (remaining > StackGuardReserveBytes * 4)
        g_stack_guard_quiet = false;

    if (remaining > StackGuardReserveBytes || g_stack_guard_quiet)
        return 0;

    g_stack_guard_quiet = true;
    return 1;
}

//Method headers belong to the file format rather than the metadata, so their layout is spelled out here
//ECMA-335 II.25.4.2: the low bits of the first byte pick between the two layouts and the tiny one carries the size
static const unsigned char IlFatFormat = 0x3;

//A fat header is a bitfield: the low bits hold the format and flags, the top four the header length in dwords
//MoreSects marks a section table after the code, and that is where exception handlers are described
//The value is taken from corhdr.h, where MoreSects is 0x8 and InitLocals is 0x10
static const unsigned short IlMoreSects = 0x8;
static const unsigned short IlFatHeaderDwords = 3;

//The format and flags share the low bits, with the header length in the top four
//Everything in between is an ordinary flag and has to be carried over when the header is rebuilt
static const unsigned short IlFormatMask = 0x7;
static const unsigned short IlHeaderSizeMask = 0xF000;

struct IlFatHeader
{
    unsigned short Flags;
    unsigned short MaxStack;
    unsigned int CodeSize;
    unsigned int LocalVarSigTok;
};

//The call opcode followed by a little endian four byte metadata token
static const unsigned char IlCallOpcode = 0x28;
static const unsigned int IlCallSize = 5;

//The check takes no arguments and returns nothing, so it leaves the stack balanced and MaxStack stays as it was
//A method signature is the calling convention, then the parameter count, then the return type
//
//The call points at the kernel rather than at the framework helper. The framework check fires on every call, so once
//the stack is short it keeps firing while the exception is unwound and handled, and the process ends up dying while
//it is trying to report the problem. The kernel entry goes quiet instead until the stack has grown back
static const unsigned char StackCheckSignature[] = {0x00, 0x00, 0x01};
static const wchar_t StackCheckTypeName[] = L"NetCraft.Util.NativeStackGuard";
static const wchar_t StackCheckMethodName[] = L"EnsureStack";
static const wchar_t StackCheckAssemblyName[] = L"NetCraft.Util";

static const IID IidMetaDataAssemblyEmit = {
    0x211EF15B, 0x5317, 0x4438, {0xB1, 0x96, 0xDE, 0xC8, 0x7B, 0x88, 0x76, 0x93}};

//A metadata token only means something inside the module that defined it, so one reference is kept per module
static std::map<ModuleID, mdMemberRef> g_stack_check_refs;
static std::mutex g_stack_check_refs_mutex;

//Bodies already given the check
//
//SetILFunctionBody is only defined for the first call on a given method: a method body can be reported for
//compilation more than once, and replacing it again leaves the runtime with IL it rejects as invalid. Recording what
//has been handled keeps every later report off a body that was already rewritten
static std::set<std::pair<ModuleID, mdMethodDef>> g_rewritten_bodies;
static std::mutex g_rewritten_bodies_mutex;

//MarkRewritten records a body and reports whether this is the first time it was seen
static bool MarkRewritten(ModuleID moduleId, mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> guard(g_rewritten_bodies_mutex);
    return g_rewritten_bodies.insert(std::make_pair(moduleId, methodDef)).second;
}

//FindOrCreateStackCheckRef returns the member reference for the helper, creating it on first use in a module
//Zero means it could not be built, and the caller then leaves the method alone rather than writing IL that cannot bind
static mdMemberRef FindOrCreateStackCheckRef(ModuleID moduleId, IMetaDataEmit* emit)
{
    {
        std::lock_guard<std::mutex> guard(g_stack_check_refs_mutex);
        const auto found = g_stack_check_refs.find(moduleId);
        if (found != g_stack_check_refs.end())
            return found->second;
    }

    IMetaDataAssemblyEmit* assemblyEmit = nullptr;
    if (FAILED(emit->QueryInterface(IidMetaDataAssemblyEmit, reinterpret_cast<void**>(&assemblyEmit))))
        return 0;

    //The assembly is referenced by name alone: binding in this runtime goes by simple name, and a module touching any
    //framework type already resolves against the same core library
    ASSEMBLYMETADATA assemblyMeta;
    memset(&assemblyMeta, 0, sizeof(assemblyMeta));

    mdAssemblyRef coreLibRef = 0;
    HRESULT hr = assemblyEmit->DefineAssemblyRef(
        nullptr, 0, StackCheckAssemblyName, &assemblyMeta, nullptr, 0, 0, &coreLibRef);
    assemblyEmit->Release();

    if (FAILED(hr) || coreLibRef == 0)
        return 0;

    mdTypeRef typeRef = 0;
    hr = emit->DefineTypeRefByName(coreLibRef, StackCheckTypeName, &typeRef);
    if (FAILED(hr) || typeRef == 0)
        return 0;

    mdMemberRef memberRef = 0;
    hr = emit->DefineMemberRef(
        typeRef, StackCheckMethodName, StackCheckSignature,
        static_cast<ULONG>(sizeof(StackCheckSignature)), &memberRef);
    if (FAILED(hr) || memberRef == 0)
        return 0;

    {
        std::lock_guard<std::mutex> guard(g_stack_check_refs_mutex);
        g_stack_check_refs[moduleId] = memberRef;
    }

    return memberRef;
}

//The prefix every assembly of this ecosystem carries
static const wchar_t EcosystemNamePrefix[] = L"NetCraft";
static const size_t EcosystemNamePrefixLength = 8;

//How much room is given to one referenced assembly name while the reference table is walked
static const ULONG AssemblyRefNameCapacity = 256;

//Whether a module's assembly references this ecosystem, worked out once per module
//Reading the metadata is not free and the answer never changes for a module
static std::map<ModuleID, bool> g_ecosystem_modules;
static std::mutex g_ecosystem_modules_mutex;

//HasEcosystemReference reports whether a module's assembly references anything from this ecosystem
//
//The injected call names a type in one of the kernel's assemblies, so a module referencing none of them cannot bind
//that call: the rewritten body is rejected the moment it is compiled. The host that starts the kernel is exactly this
//case, it carries the same name prefix while knowing only its own assemblies
static bool HasEcosystemReference(ModuleID moduleId)
{
    {
        std::lock_guard<std::mutex> guard(g_ecosystem_modules_mutex);
        const auto found = g_ecosystem_modules.find(moduleId);
        if (found != g_ecosystem_modules.end())
            return found->second;
    }

    bool referenced = false;
    IMetaDataImport* import = nullptr;
    if (SUCCEEDED(g_profiler_info->GetModuleMetaData(
            moduleId, ofRead, IidMetaDataImport, reinterpret_cast<IUnknown**>(&import))))
    {
        //The reference table hangs off the assembly import, which the plain import hands over on request
        IMetaDataAssemblyImport* assemblyImport = nullptr;
        if (SUCCEEDED(import->QueryInterface(
                IidMetaDataAssemblyImport, reinterpret_cast<void**>(&assemblyImport))))
        {
            HCORENUM enumerator = nullptr;
            mdAssemblyRef refs[16];
            ULONG count = 0;

            //The name is only reported as long as there is room, and a shorter buffer would silently truncate, so the
            //result is compared as a prefix rather than as a whole name
            while (!referenced &&
                   SUCCEEDED(assemblyImport->EnumAssemblyRefs(&enumerator, refs, 16, &count)) &&
                   count > 0)
            {
                for (ULONG i = 0; i < count; i++)
                {
                    wchar_t refName[AssemblyRefNameCapacity];
                    ULONG refNameLength = 0;
                    if (FAILED(assemblyImport->GetAssemblyRefProps(
                            refs[i], nullptr, nullptr, refName, AssemblyRefNameCapacity, &refNameLength,
                            nullptr, nullptr, nullptr, nullptr)))
                        continue;

                    if (wcsncmp(refName, EcosystemNamePrefix, EcosystemNamePrefixLength) == 0)
                    {
                        referenced = true;
                        break;
                    }
                }

                count = 0;
            }

            assemblyImport->CloseEnum(enumerator);
            assemblyImport->Release();
        }

        import->Release();
    }

    {
        std::lock_guard<std::mutex> guard(g_ecosystem_modules_mutex);
        g_ecosystem_modules[moduleId] = referenced;
    }

    return referenced;
}

//IsRewritableModule asks the Rust side whether this module is on the configured list
//The name is taken from the assembly rather than the file path: a mod can be handed to the runtime as a byte array,
//which leaves the module with no path at all, while its assembly name is present however it was loaded
static bool IsRewritableModule(AssemblyID assemblyId)
{
    wchar_t assemblyName[512];
    ULONG nameLength = 0;
    AppDomainID appDomainId = 0;
    ModuleID moduleId = 0;
    if (FAILED(g_profiler_info->GetAssemblyInfo(
            assemblyId, 512, &nameLength, assemblyName, &appDomainId, &moduleId)))
        return false;

    //Assembly names are ASCII; anything else is written as a placeholder rather than mistranslated
    char simpleName[256];
    int length = 0;
    for (const wchar_t* cursor = assemblyName; *cursor != L'\0' && length < 255; cursor++)
    {
        simpleName[length++] = (*cursor < 128) ? static_cast<char>(*cursor) : '?';
    }
    simpleName[length] = '\0';

    if (simpleName[0] == '\0')
        return false;

    //Whether the module belongs to the ecosystem is only weighed when no list is configured, but the shell is the only
    //side that can read it off the metadata, so it is gathered here either way
    const bool accepted =
        ncn_should_rewrite_module(simpleName, HasEcosystemReference(moduleId) ? 1 : 0) != 0;

    //Reported either way so a run shows which assemblies were asked about and which ones the list took
    ncn_on_module_seen(simpleName, accepted ? 1 : 0);
    return accepted;
}

//InjectIntoBody rewrites one method body, keeping the original code and placing the call in front of it
//
//Bodies carrying a section table are passed over. That table is where exception handlers are described, and every
//offset in it is relative to the start of the code, so inserting anything would require shifting all of them by the
//same amount. Skipping them keeps this step safe to reason about; handlers can be handled afterwards
static void InjectIntoBody(
    ModuleID moduleId, mdMethodDef methodDef, IMetaDataEmit* emit, LPCBYTE header)
{
    const unsigned char* bytes = reinterpret_cast<const unsigned char*>(header);
    const bool isFat = (bytes[0] & 0x3) == IlFatFormat;


    const unsigned char* code = nullptr;
    unsigned int codeSize = 0;
    unsigned short maxStack = 8;
    unsigned int localVarSigTok = 0;
    unsigned short preservedFlags = 0;

    if (isFat)
    {
        const IlFatHeader* fat = reinterpret_cast<const IlFatHeader*>(bytes);

        if ((fat->Flags & IlMoreSects) != 0)
            return;

        //The header length is stored as a count of dwords, so the code begins that far into the body
        const unsigned int headerBytes = ((fat->Flags >> 12) & 0xF) * 4;
        code = bytes + headerBytes;
        codeSize = fat->CodeSize;
        maxStack = fat->MaxStack;
        localVarSigTok = fat->LocalVarSigTok;

        //Only the format and header length bits are rewritten when the header is rebuilt, so the rest are kept here
        //InitLocals is the one that matters: dropping it leaves the locals uninitialized and the body stops verifying
        preservedFlags = static_cast<unsigned short>(fat->Flags & ~IlFormatMask & ~IlHeaderSizeMask);
    }
    else
    {
        //A tiny header packs the size into the byte itself and carries nothing else
        codeSize = (bytes[0] >> 2) & 0x3F;
        code = bytes + 1;
    }

    const unsigned int newCodeSize = codeSize + IlCallSize;

    //A tiny header cannot describe more than 63 bytes, so a body that no longer fits is left as it is rather than
    //promoted to a fat header. Promotion is a separate change and not needed to prove the mechanism
    if (!isFat && newCodeSize > 63)
        return;

    const mdMemberRef callee = FindOrCreateStackCheckRef(moduleId, emit);
    if (callee == 0)
        return;

    IMethodMalloc* allocator = nullptr;
    if (FAILED(g_profiler_info->GetILFunctionBodyAllocator(moduleId, &allocator)))
        return;

    const unsigned int headerBytes = isFat ? sizeof(IlFatHeader) : 1;
    unsigned char* replacement = static_cast<unsigned char*>(allocator->Alloc(headerBytes + newCodeSize));
    if (replacement != nullptr)
    {
        unsigned char* target = replacement;

        if (isFat)
        {
            //The format and header length are set for the rebuilt header, every other flag is carried over untouched
            IlFatHeader* fat = reinterpret_cast<IlFatHeader*>(replacement);
            fat->Flags = static_cast<unsigned short>(
                preservedFlags | (IlFatHeaderDwords << 12) | IlFatFormat);
            fat->MaxStack = maxStack;
            fat->CodeSize = newCodeSize;
            fat->LocalVarSigTok = localVarSigTok;
            target = replacement + sizeof(IlFatHeader);
        }
        else
        {
            replacement[0] = static_cast<unsigned char>((newCodeSize << 2) | 0x2);
            target = replacement + 1;
        }

        //The call is the opcode followed by the token, least significant byte first
        target[0] = IlCallOpcode;
        memcpy(target + 1, &callee, sizeof(callee));

        memcpy(target + IlCallSize, code, codeSize);

        if (SUCCEEDED(g_profiler_info->SetILFunctionBody(moduleId, methodDef, replacement)))
            ncn_on_injected();
    }

    allocator->Release();
}

//TryInjectStackCheck adds the check to one method body when that method is one this layer should touch
static void TryInjectStackCheck(FunctionID functionId, BOOL isSafeToBlock)
{
    //Nothing is written until the assembly holding the injected entry has been loaded
    //
    //The kernel's main assembly deliberately reaches for nothing outside the framework while it initializes: the
    //resolver that can find this layer's assembly is installed from inside that very initialization, so resolving the
    //entry any earlier would fail and take the whole main library load down with it. A body compiled before then has
    //to be left alone, and the managed side says when that point has passed
    if (ncn_stack_guard_ready() == 0)
        return;

    //Replacing a body is not safe while the runtime is at a point it cannot block, so those are passed over
    if (isSafeToBlock == FALSE)
        return;

    ClassID classId = 0;
    ModuleID moduleId = 0;
    mdMethodDef methodDef = 0;
    if (FAILED(g_profiler_info->GetFunctionInfo(functionId, &classId, &moduleId, &methodDef)))
        return;

    //Replacing a body twice is not something the runtime supports, so a method already handled is left alone
    if (!MarkRewritten(moduleId, methodDef))
        return;

    //The assembly is what the configured list names, so it is resolved from the module rather than kept alongside
    AssemblyID assemblyId = 0;
    if (FAILED(g_profiler_info->GetModuleInfo(moduleId, nullptr, 0, nullptr, nullptr, &assemblyId)))
        return;

    if (!IsRewritableModule(assemblyId))
        return;

    IMetaDataImport* import = nullptr;
    IMetaDataEmit* emit = nullptr;
    if (FAILED(g_profiler_info->GetModuleMetaData(
            moduleId, ofRead | ofWrite, IidMetaDataImport, reinterpret_cast<IUnknown**>(&import))))
        return;
    if (FAILED(import->QueryInterface(IidMetaDataEmit, reinterpret_cast<void**>(&emit))))
    {
        import->Release();
        return;
    }

    LPCBYTE header = nullptr;
    ULONG headerSize = 0;
    if (SUCCEEDED(g_profiler_info->GetILFunctionBody(moduleId, methodDef, &header, &headerSize)))
        InjectIntoBody(moduleId, methodDef, emit, header);

    emit->Release();
    import->Release();
}

namespace netcraft_native {

//IUnknown: the count never reaches zero for destruction
//The profiler lives until the process ends, so tearing it down during shutdown cannot cause trouble
ULONG STDMETHODCALLTYPE ComBase::AddRef()
{
    return ++ref_count_;
}

ULONG STDMETHODCALLTYPE ComBase::Release()
{
    return --ref_count_;
}

HRESULT STDMETHODCALLTYPE ComBase::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
        return E_POINTER;

    const bool known =
        SameIid(riid, IidUnknown) ||
        SameIid(riid, IidProfilerCallback) ||
        SameIid(riid, IidProfilerCallback2) ||
        SameIid(riid, IidProfilerCallback3) ||
        SameIid(riid, IidProfilerCallback4) ||
        SameIid(riid, IidProfilerCallback5) ||
        SameIid(riid, IidProfilerCallback6) ||
        SameIid(riid, IidProfilerCallback7) ||
        SameIid(riid, IidProfilerCallback8) ||
        SameIid(riid, IidProfilerCallback9) ||
        SameIid(riid, IidProfilerCallback10);

    if (!known)
    {
        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }

    *ppvObject = static_cast<ICorProfilerCallback10*>(this);
    AddRef();
    return S_OK;
}

//Profiler overrides the lifecycle callbacks and the one compilation notification
//Everything else stays on the generated defaults because this profiler subscribes to nothing else
class Profiler final : public ComBase {
public:
    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override
    {
        //The profiling interface is taken here and held; the runtime only offers it during this call
        HRESULT hr = E_FAIL;
        if (pICorProfilerInfoUnk != nullptr)
        {
            hr = pICorProfilerInfoUnk->QueryInterface(IidCorProfilerInfo, reinterpret_cast<void**>(&g_profiler_info));
        }

        //The interface is forwarded untouched as well; the Rust side keeps it in case a later module needs it
        const int32_t result = ncn_on_initialize(pICorProfilerInfoUnk);
        if (result != 0)
            return E_FAIL;

        if (FAILED(hr) || !SetJitNotifications())
        {
            //Stack checks need the notification to have any effect, so the Rust side is told it did not take
            ncn_on_jit_notifications(false);
        }
        else
        {
            ncn_on_jit_notifications(true);
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Shutdown() override
    {
        ncn_on_shutdown();

        if (g_profiler_info != nullptr)
        {
            g_profiler_info->Release();
            g_profiler_info = nullptr;
        }

        return S_OK;
    }

    //JITCompilationStarted fires before a method body is handed to the JIT, which is the only moment its IL can be
    //replaced. The Rust side decides whether this method is one of the ones carrying a stack check
    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) override
    {
        ncn_on_jit_compiled(static_cast<size_t>(functionId));
        TryInjectStackCheck(functionId, fIsSafeToBlock);
        return S_OK;
    }
};

//ClassFactory builds the Profiler when the CLR asks for one
class ClassFactory final : public IClassFactory {
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
    {
        if (ppvObject == nullptr)
            return E_POINTER;
        if (SameIid(riid, IidUnknown) || SameIid(riid, IidClassFactory))
        {
            *ppvObject = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return ++ref_count_;
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG count = --ref_count_;
        if (count == 0)
            delete this;
        return count;
    }

    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override
    {
        if (ppvObject == nullptr)
            return E_POINTER;
        *ppvObject = nullptr;
        if (pUnkOuter != nullptr)
            return CLASS_E_NOAGGREGATION;

        auto* profiler = new (std::nothrow) Profiler();
        if (profiler == nullptr)
            return E_OUTOFMEMORY;

        const HRESULT hr = profiler->QueryInterface(riid, ppvObject);
        profiler->Release();
        return hr;
    }

    HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override
    {
        (void)fLock;
        return S_OK;
    }

private:
    std::atomic<ULONG> ref_count_{1};
};

}

//The C++ side only implements; the exported names are produced by the Rust side, see lib.rs
extern "C" int32_t ncn_com_get_class_object(const GUID* rclsid, const GUID* riid, void** ppv_object)
{
    if (ppv_object == nullptr)
        return E_POINTER;

    if (rclsid == nullptr || !SameIid(*rclsid, ClsidNetCraftNative))
        return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new (std::nothrow) netcraft_native::ClassFactory();
    if (factory == nullptr)
        return E_OUTOFMEMORY;

    const HRESULT hr = factory->QueryInterface(*riid, ppv_object);
    factory->Release();
    return hr;
}

//The layer stays in the process for its whole life, so it never reports itself as unloadable
int32_t ncn_com_can_unload_now()
{
    return S_FALSE;
}
