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
#include <string>
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
//ReJIT only exists from the seventh revision of the profiling interface on
static const IID IidProfilerInfo7 = {
    0x9AEECC0D, 0x63E0, 0x4187, {0x8C, 0x00, 0xE3, 0x12, 0xF5, 0x03, 0xF6, 0x63}};

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
void ncn_on_module_load_finished(uint64_t module_id, int32_t hr_status);
void ncn_on_module_unload_started(uint64_t module_id);
int32_t ncn_on_get_rejit_parameters(uint64_t module_id, uint32_t method_def, void* function_control);
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

//A second reference, to the revision that carries ReJIT
//It is taken from the same object during Initialize and released the same way
static ICorProfilerInfo7* g_profiler_info7 = nullptr;

//The runtime mask has to carry what both sides need
//
//A process has exactly one profiler, so there is one mask, and setting it twice would have the later call replace the
//earlier one and silently switch that side off. The union is therefore built here and set once.
//
//MONITOR_JIT_COMPILATION is notification only and costs nothing by itself
//ENABLE_REJIT is what makes RequestReJIT possible, and the runtime refuses it unless ReadyToRun images are given up
//alongside it, which is why DISABLE_ALL_NGEN_IMAGES comes too
static const DWORD ProfilerEventMask = COR_PRF_MONITOR_JIT_COMPILATION | COR_PRF_MONITOR_MODULE_LOADS |
    COR_PRF_ENABLE_REJIT | COR_PRF_DISABLE_ALL_NGEN_IMAGES;

//SetProfilerMask enables the combined mask and reports whether the runtime took it
//A failure costs both the early stack check and every runtime rewrite, so it is surfaced rather than swallowed
static bool SetProfilerMask()
{
    if (g_profiler_info == nullptr)
        return false;

    const HRESULT hr = g_profiler_info->SetEventMask(ProfilerEventMask);
    return SUCCEEDED(hr);
}

//---- early stack check injection ----

//The check itself lives on the managed side: reading the thread's stack limit and the quiet period around a trip both
//belong next to the entry the injected call points at, and a call that never leaves managed code costs a fraction of
//the transition into this layer did. What is left here is deciding which bodies are worth the check at all

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
//The call points at the kernel entry rather than straight at the framework probe. The probe on its own fires on every
//call, so once the stack is short it keeps firing while the exception is unwound and handled, and the process ends up
//dying on its way to reporting the problem; the kernel entry wraps it with a quiet period instead
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

//---- runaway body scan ----

//Operand widths and branch flags, taken from CoreCLR's own opcode table
//
//Stepping over IL means advancing by each instruction's exact width: read an operand as an opcode and the rest of the
//walk is nonsense. The tokens below stand in for the ones that table uses so it expands into the tables underneath
//instead of being transcribed by hand, and a token it uses that is missing here fails the build rather than passing
//silently. The internal entries are left out, they carry no encoding
#define OPDEF_REAL_OPCODES_ONLY
#define InlineNone 0
#define InlineI 4
#define InlineI8 8
#define InlineR 8
#define ShortInlineR 4
#define InlineBrTarget 4
#define ShortInlineBrTarget 1
#define InlineVar 2
#define ShortInlineVar 1
#define ShortInlineI 1
#define InlineType 4
#define InlineMethod 4
#define InlineField 4
#define InlineString 4
#define InlineSig 4
#define InlineTok 4
//switch is the one operand whose width is not fixed; the walk reads it apart itself
#define InlineSwitch 0

//The two kinds worth noticing are folded into one value as bits: an instruction that branches back can drive a loop,
//and one that calls can push the frame that begins the next descent. The rest say how an instruction leaves, which this
//walk does not trace
#define NEXT 0
#define BREAK 0
#define RETURN 0
#define THROW 0
#define META 0
#define BRANCH 1
#define COND_BRANCH 1
#define CALL 2

//The bits of that value, named here because the tokens above only exist while the table is being expanded
static const unsigned char IlFlowBranch = 1;
static const unsigned char IlFlowCall = 2;

struct IlOpcodeInfo
{
    unsigned char OperandBytes;
    unsigned char Flow;
};

//One row per entry of the vendored table, before it is spread over the two lookups
struct IlOpcodeRow
{
    unsigned char EncodingBytes;
    unsigned char SecondByte;
    unsigned char OperandBytes;
    unsigned char Flow;
};

static const IlOpcodeRow IlOpcodeRows[] =
{
#define OPDEF(canonical, display, pop, push, operand, kind, encoding, first, second, flow) \
    { encoding, second, operand, flow },
#define OPALIAS(canonical, display, real)
#include "opcode.def"
#undef OPALIAS
#undef OPDEF
};

#undef OPDEF_REAL_OPCODES_ONLY
#undef InlineNone
#undef InlineI
#undef InlineI8
#undef InlineR
#undef ShortInlineR
#undef InlineBrTarget
#undef ShortInlineBrTarget
#undef InlineVar
#undef ShortInlineVar
#undef ShortInlineI
#undef InlineType
#undef InlineMethod
#undef InlineField
#undef InlineString
#undef InlineSig
#undef InlineTok
#undef InlineSwitch
#undef NEXT
#undef BREAK
#undef CALL
#undef RETURN
#undef THROW
#undef META
#undef BRANCH
#undef COND_BRANCH

//The plain map is indexed by the first byte, the prefixed one by the byte after 0xFE
static IlOpcodeInfo g_ilPlain[256];
static IlOpcodeInfo g_ilPrefixed[256];
static std::once_flag g_ilTablesOnce;

//ReadUInt32 reads a little endian four byte operand
static unsigned int ReadUInt32(const unsigned char* at)
{
    return static_cast<unsigned int>(at[0])
        | (static_cast<unsigned int>(at[1]) << 8)
        | (static_cast<unsigned int>(at[2]) << 16)
        | (static_cast<unsigned int>(at[3]) << 24);
}

//BuildIlOpcodeTables spreads the rows over the two lookups
static void BuildIlOpcodeTables()
{
    for (const IlOpcodeRow& row : IlOpcodeRows)
    {
        if (row.EncodingBytes == 1)
            g_ilPlain[row.SecondByte] = { row.OperandBytes, row.Flow };
        else
            g_ilPrefixed[row.SecondByte] = { row.OperandBytes, row.Flow };
    }
}

//MayExhaustStack reports whether a body can take part in a descent that never ends
//
//Every frame such a descent pushes comes from a call, so a body with no call in it always returns and no chain of such
//bodies can run the stack out. It also has to come back to a body it has already been in, which is either a branch back
//or a call sitting on a cycle; asking only whether a call is there at all covers straight and mutual recursion alike,
//and it needs no idea where the call goes. That last part is what makes it usable here: the interesting cycles go
//through an interface, where the token names the interface method rather than whichever implementation is reached
//
//The branch rule is kept alongside it. A loop that only takes space from its own frame, and so never calls anything,
//can still walk the stack pointer down to the guard page
//
//A body the walk cannot make sense of is taken to be able to. That leaves it exactly as it was before this scan existed,
//so an unreadable body keeps the check rather than quietly losing it
static bool MayExhaustStack(const unsigned char* code, unsigned int codeSize)
{
    std::call_once(g_ilTablesOnce, BuildIlOpcodeTables);

    unsigned int at = 0;
    while (at < codeSize)
    {
        const unsigned char opcode = code[at];
        const IlOpcodeInfo* info = &g_ilPlain[opcode];
        unsigned int operandAt = at + 1;

        if (opcode == 0xFE)
        {
            if (operandAt >= codeSize)
                return true;
            info = &g_ilPrefixed[code[operandAt]];
            operandAt++;
        }

        //switch carries a count and that many offsets, so its width is only known once the count has been read
        if (opcode == 0x45)
        {
            if (operandAt + 4 > codeSize)
                return true;

            const unsigned int targets = ReadUInt32(code + operandAt);
            const unsigned int tableAt = operandAt + 4;
            if (targets > (codeSize - tableAt) / 4)
                return true;

            const unsigned int after = tableAt + targets * 4;
            for (unsigned int i = 0; i < targets; i++)
            {
                const long long target = static_cast<long long>(after)
                    + static_cast<int>(ReadUInt32(code + tableAt + i * 4));
                if (target <= static_cast<long long>(at))
                    return true;
            }

            at = after;
            continue;
        }

        if ((info->Flow & IlFlowBranch) != 0)
        {
            if (operandAt + info->OperandBytes > codeSize)
                return true;

            //Targets are relative to the instruction after the branch; a back edge always clears the instruction by a
            //wide margin, so the exact convention does not matter to a loop test
            const long long after = static_cast<long long>(operandAt) + info->OperandBytes;
            const long long target = after + (info->OperandBytes == 1
                ? static_cast<signed char>(code[operandAt])
                : static_cast<int>(ReadUInt32(code + operandAt)));
            if (target <= static_cast<long long>(at))
                return true;
        }
        else if ((info->Flow & IlFlowCall) != 0)
        {
            //The target is not looked at: whatever it names, this is the instruction that pushes the next frame
            return true;
        }

        at = operandAt + info->OperandBytes;
    }

    return false;
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

    //Only a body that can take part in an endless descent is worth the check, which keeps it off the many leaf methods
    //that can never be part of one
    if (!MayExhaustStack(code, codeSize))
        return;

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

            //The rewrite engine needs the seventh revision, reached from the same object; a runtime that does not
            //offer it leaves that pointer null, which costs runtime rewrites and nothing else
            pICorProfilerInfoUnk->QueryInterface(IidProfilerInfo7, reinterpret_cast<void**>(&g_profiler_info7));
        }

        //The interface is forwarded untouched as well; the Rust side keeps it in case a later module needs it
        const int32_t result = ncn_on_initialize(pICorProfilerInfoUnk);
        if (result != 0)
            return E_FAIL;

        if (FAILED(hr) || !SetProfilerMask())
        {
            //Both the stack check and the rewrite engine depend on the mask, so the Rust side is told it did not take
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

        if (g_profiler_info7 != nullptr)
        {
            g_profiler_info7->Release();
            g_profiler_info7 = nullptr;
        }

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

    //ModuleLoadFinished is where the rewrite engine learns that a module is in place
    //Requests wait on this: a module that is not loaded cannot be looked up and its name is not even known
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) override
    {
        ncn_on_module_load_finished(static_cast<uint64_t>(moduleId), static_cast<int32_t>(hrStatus));
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID moduleId) override
    {
        ncn_on_module_unload_started(static_cast<uint64_t>(moduleId));
        return S_OK;
    }

    //GetReJITParameters is the moment a runtime rewrite actually happens: the runtime is waiting for the new IL
    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                                 ICorProfilerFunctionControl* pFunctionControl) override
    {
        return ncn_on_get_rejit_parameters(static_cast<uint64_t>(moduleId), static_cast<uint32_t>(methodId),
                                           pFunctionControl) == 0
                   ? S_OK
                   : E_FAIL;
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

//---- façade for the rewrite engine ----
//The Rust side reaches the profiling API only through these; no COM is written over there
//
//Every one of them goes through the seventh revision, because that is the one the rewrite engine is built on and it
//inherits everything the earlier ones offered. A runtime that did not hand it over leaves the pointer null, and each
//call then fails on its own rather than taking the layer down

//WCHAR is wchar_t on Windows and char16_t elsewhere, so wide text is stored under this alias throughout
using WideString = std::basic_string<WCHAR>;

//ToWide widens an ASCII name byte by byte
//No platform API is used, which keeps the same code working on every target
static WideString ToWide(const char* text)
{
    WideString wide;
    if (text == nullptr)
        return wide;
    for (const char* cursor = text; *cursor != '\0'; ++cursor)
        wide.push_back(static_cast<WCHAR>(static_cast<unsigned char>(*cursor)));
    return wide;
}

//ncn_get_module_name spells out a module's file name, which is what identifies the target of a request
extern "C" int32_t ncn_get_module_name(uint64_t module_id, char* buffer, uint32_t capacity)
{
    if (g_profiler_info7 == nullptr || buffer == nullptr || capacity == 0)
        return -1;

    WCHAR name[512] = {};
    ULONG length = 0;
    const HRESULT hr = g_profiler_info7->GetModuleInfo(static_cast<ModuleID>(module_id), nullptr, 512,
                                                       &length, name, nullptr);
    if (hr != S_OK)
        return -1;

    uint32_t index = 0;
    for (; name[index] != 0 && index + 1 < capacity; ++index)
        buffer[index] = static_cast<char>(name[index]);
    buffer[index] = '\0';
    return 0;
}

//ncn_find_method looks a method up by type and method name inside one module, answering its metadata token
extern "C" uint32_t ncn_find_method(uint64_t module_id, const char* type_name, const char* method_name)
{
    if (g_profiler_info7 == nullptr || type_name == nullptr || method_name == nullptr)
        return 0;

    IMetaDataImport* import = nullptr;
    const HRESULT import_hr = g_profiler_info7->GetModuleMetaData(
        static_cast<ModuleID>(module_id), ofRead, IidMetaDataImport, reinterpret_cast<IUnknown**>(&import));
    if (import_hr != S_OK || import == nullptr)
        return 0;

    const WideString wide_type = ToWide(type_name);
    const WideString wide_method = ToWide(method_name);

    uint32_t found = 0;
    mdTypeDef type_def = mdTypeDefNil;
    if (import->FindTypeDefByName(wide_type.c_str(), mdTokenNil, &type_def) == S_OK)
    {
        HCORENUM enumerator = nullptr;
        mdMethodDef methods[32] = {};
        ULONG fetched = 0;
        while (found == 0 && import->EnumMethods(&enumerator, type_def, methods, 32, &fetched) == S_OK && fetched > 0)
        {
            for (ULONG i = 0; i < fetched && found == 0; ++i)
            {
                WCHAR name[256] = {};
                ULONG name_length = 0;
                if (import->GetMethodProps(methods[i], nullptr, name, 256, &name_length, nullptr, nullptr, nullptr,
                                           nullptr, nullptr) == S_OK &&
                    wide_method == name)
                {
                    found = methods[i];
                }
            }
        }
        import->CloseEnum(enumerator);
    }

    import->Release();
    return found;
}

//ncn_request_rejit asks the runtime to recompile a method the next time it is called
//That is when it asks back for the replacement body, through GetReJITParameters
extern "C" int32_t ncn_request_rejit(uint64_t module_id, uint32_t method_def)
{
    if (g_profiler_info7 == nullptr || method_def == 0)
        return -1;

    ModuleID modules[1] = {static_cast<ModuleID>(module_id)};
    mdMethodDef tokens[1] = {static_cast<mdMethodDef>(method_def)};
    return g_profiler_info7->RequestReJIT(1, modules, tokens) == S_OK ? 0 : -1;
}

//ncn_set_il_function_body hands the replacement body over at the moment the runtime asks for it
extern "C" int32_t ncn_set_il_function_body(void* function_control, const uint8_t* body, uint32_t size)
{
    if (function_control == nullptr || body == nullptr || size == 0)
        return -1;

    auto* control = static_cast<ICorProfilerFunctionControl*>(function_control);
    return control->SetILFunctionBody(size, body) == S_OK ? 0 : -1;
}

//ncn_get_il_function_body copies a method's current IL out so the Rust side can parse it
extern "C" int32_t ncn_get_il_function_body(uint64_t module_id, uint32_t method_def, uint8_t* buffer, uint32_t capacity)
{
    if (g_profiler_info7 == nullptr || buffer == nullptr || capacity == 0)
        return -1;

    LPCBYTE body = nullptr;
    ULONG size = 0;
    const HRESULT hr = g_profiler_info7->GetILFunctionBody(static_cast<ModuleID>(module_id),
                                                           static_cast<mdMethodDef>(method_def), &body, &size);
    if (hr != S_OK || body == nullptr || size == 0 || size > capacity)
        return -1;

    memcpy(buffer, body, size);
    return static_cast<int32_t>(size);
}

//ncn_initialize_current_thread lets the runtime recognize a thread belonging to this layer
//A call into the profiling API from any other thread has to be preceded by it or it fails
extern "C" int32_t ncn_initialize_current_thread()
{
    if (g_profiler_info7 == nullptr)
        return -1;
    return g_profiler_info7->InitializeCurrentThread() == S_OK ? 0 : -1;
}

//FindOrCreateAssemblyRef reuses a reference the module already carries
//Adding a duplicate would leave two entries with the same name, and every token after it in the table would shift
static mdAssemblyRef FindOrCreateAssemblyRef(IMetaDataAssemblyImport* assembly_import,
                                             IMetaDataAssemblyEmit* assembly_emit, const WideString& name)
{
    if (assembly_import != nullptr)
    {
        HCORENUM enumerator = nullptr;
        mdAssemblyRef refs[32] = {};
        ULONG fetched = 0;
        while (assembly_import->EnumAssemblyRefs(&enumerator, refs, 32, &fetched) == S_OK && fetched > 0)
        {
            for (ULONG i = 0; i < fetched; ++i)
            {
                WCHAR ref_name[256] = {};
                ULONG name_length = 0;
                if (assembly_import->GetAssemblyRefProps(refs[i], nullptr, nullptr, ref_name, 256, &name_length,
                                                         nullptr, nullptr, nullptr, nullptr) == S_OK &&
                    name == ref_name)
                {
                    assembly_import->CloseEnum(enumerator);
                    return refs[i];
                }
            }
        }
        assembly_import->CloseEnum(enumerator);
    }

    if (assembly_emit == nullptr)
        return mdAssemblyRefNil;

    ASSEMBLYMETADATA metadata = {};
    mdAssemblyRef created = mdAssemblyRefNil;
    if (assembly_emit->DefineAssemblyRef(nullptr, 0, name.c_str(), &metadata, nullptr, 0, 0, &created) != S_OK)
        return mdAssemblyRefNil;
    return created;
}

//ncn_ensure_type_ref makes sure a module carries a reference to a type and answers its TypeRef token
extern "C" uint32_t ncn_ensure_type_ref(uint64_t module_id, const char* assembly_name, const char* type_name)
{
    if (g_profiler_info7 == nullptr || assembly_name == nullptr || type_name == nullptr)
        return 0;

    IMetaDataAssemblyImport* assembly_import = nullptr;
    IMetaDataAssemblyEmit* assembly_emit = nullptr;
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead, IidMetaDataAssemblyImport,
                                        reinterpret_cast<IUnknown**>(&assembly_import));
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead | ofWrite, IidMetaDataAssemblyEmit,
                                        reinterpret_cast<IUnknown**>(&assembly_emit));

    const mdAssemblyRef scope = FindOrCreateAssemblyRef(assembly_import, assembly_emit, ToWide(assembly_name));

    if (assembly_emit != nullptr)
        assembly_emit->Release();
    if (assembly_import != nullptr)
        assembly_import->Release();

    if (scope == mdAssemblyRefNil)
        return 0;

    //The scope has to be settled before the lookup: FindTypeRef only matches references without a scope when it is
    //handed mdTokenNil, and everything built here hangs off an AssemblyRef. A nil scope would therefore never find
    //the reference already made, would try to build it a second time, fail, and leave the caller without a token
    IMetaDataImport* import = nullptr;
    IMetaDataEmit* emit = nullptr;
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead, IidMetaDataImport,
                                        reinterpret_cast<IUnknown**>(&import));
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead | ofWrite, IidMetaDataEmit,
                                        reinterpret_cast<IUnknown**>(&emit));

    const WideString wide = ToWide(type_name);

    if (import != nullptr)
    {
        mdTypeRef existing = mdTypeRefNil;
        if (import->FindTypeRef(scope, wide.c_str(), &existing) == S_OK)
        {
            import->Release();
            if (emit != nullptr)
                emit->Release();
            return static_cast<uint32_t>(existing);
        }
    }

    uint32_t created = 0;
    if (emit != nullptr)
    {
        mdTypeRef type_ref = mdTypeRefNil;
        if (emit->DefineTypeRefByName(scope, wide.c_str(), &type_ref) == S_OK)
            created = static_cast<uint32_t>(type_ref);
    }

    if (import != nullptr)
        import->Release();
    if (emit != nullptr)
        emit->Release();
    return created;
}

//ncn_ensure_member_ref makes sure a module carries a reference to a member and answers its MemberRef token
//The MemberRef table holds method and field references alike; what the signature blob starts with decides which one
//this is, so a field travels through here too with a FIELD-tagged signature
extern "C" uint32_t ncn_ensure_member_ref(uint64_t module_id, const char* assembly_name, const char* type_name,
                               const char* member_name, const uint8_t* signature, uint32_t signature_size)
{
    if (g_profiler_info7 == nullptr || assembly_name == nullptr || type_name == nullptr || member_name == nullptr ||
        signature == nullptr || signature_size == 0)
        return 0;

    const uint32_t type_ref = ncn_ensure_type_ref(module_id, assembly_name, type_name);
    if (type_ref == 0)
        return 0;

    IMetaDataImport* import = nullptr;
    IMetaDataEmit* emit = nullptr;
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead, IidMetaDataImport,
                                        reinterpret_cast<IUnknown**>(&import));
    g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead | ofWrite, IidMetaDataEmit,
                                        reinterpret_cast<IUnknown**>(&emit));

    const WideString wide = ToWide(member_name);

    if (import != nullptr)
    {
        mdMemberRef existing = mdMemberRefNil;
        if (import->FindMemberRef(static_cast<mdTypeRef>(type_ref), wide.c_str(), signature, signature_size,
                                  &existing) == S_OK)
        {
            import->Release();
            if (emit != nullptr)
                emit->Release();
            return static_cast<uint32_t>(existing);
        }
    }

    uint32_t created = 0;
    if (emit != nullptr)
    {
        mdMemberRef member_ref = mdMemberRefNil;
        if (emit->DefineMemberRef(static_cast<mdTypeRef>(type_ref), wide.c_str(), signature, signature_size,
                                  &member_ref) == S_OK)
            created = static_cast<uint32_t>(member_ref);
    }

    if (import != nullptr)
        import->Release();
    if (emit != nullptr)
        emit->Release();
    return created;
}

//ncn_define_user_string interns a string literal and answers its token
//Strings live in a heap of their own, which is why they need an entry point of their own
//The caller hands over UTF-16 code units, the same shape the heap stores
extern "C" uint32_t ncn_define_user_string(uint64_t module_id, const uint16_t* text, uint32_t length)
{
    if (g_profiler_info7 == nullptr || text == nullptr)
        return 0;

    IMetaDataEmit* emit = nullptr;
    if (FAILED(g_profiler_info7->GetModuleMetaData(static_cast<ModuleID>(module_id), ofRead | ofWrite,
                                                   IidMetaDataEmit, reinterpret_cast<IUnknown**>(&emit))) ||
        emit == nullptr)
        return 0;

    mdString token = 0;
    const HRESULT hr = emit->DefineUserString(reinterpret_cast<LPCWSTR>(text), length, &token);
    emit->Release();
    return SUCCEEDED(hr) ? static_cast<uint32_t>(token) : 0;
}

//The layer stays in the process for its whole life, so it never reports itself as unloadable
extern "C" int32_t ncn_com_can_unload_now()
{
    return S_FALSE;
}
