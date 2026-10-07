//Generated from corprof.h, do not edit by hand
//Source dotnet/runtime 10.0.9 src/coreclr/pal/prebuilt/inc/corprof.h, MIT
//Flattens the ICorProfilerCallback10 inheritance chain; callbacks we do not care about return S_OK
//The slots must be complete or the class stays abstract, and the compiler enforces their order
//Parameters keep their types but drop their names, so declarations match definitions without unused-parameter warnings

#ifndef NETCRAFT_NATIVE_COM_BASE_H
#define NETCRAFT_NATIVE_COM_BASE_H

#include <atomic>
#include <unknwn.h>
#include <cor.h>
#include <corprof.h>

namespace netcraft_native {

//ComBase is an instantiable ICorProfilerCallback10
//A subclass overrides the callbacks it cares about and the rest fall through to the defaults here
class ComBase : public ICorProfilerCallback10 {
public:
    ComBase();
    virtual ~ComBase();

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    HRESULT STDMETHODCALLTYPE Initialize(IUnknown*) override;
    HRESULT STDMETHODCALLTYPE Shutdown(void) override;
    HRESULT STDMETHODCALLTYPE AppDomainCreationStarted(AppDomainID) override;
    HRESULT STDMETHODCALLTYPE AppDomainCreationFinished(AppDomainID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE AppDomainShutdownStarted(AppDomainID) override;
    HRESULT STDMETHODCALLTYPE AppDomainShutdownFinished(AppDomainID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE AssemblyLoadStarted(AssemblyID) override;
    HRESULT STDMETHODCALLTYPE AssemblyLoadFinished(AssemblyID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE AssemblyUnloadStarted(AssemblyID) override;
    HRESULT STDMETHODCALLTYPE AssemblyUnloadFinished(AssemblyID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE ModuleLoadStarted(ModuleID) override;
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID) override;
    HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE ModuleAttachedToAssembly(ModuleID, AssemblyID) override;
    HRESULT STDMETHODCALLTYPE ClassLoadStarted(ClassID) override;
    HRESULT STDMETHODCALLTYPE ClassLoadFinished(ClassID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE ClassUnloadStarted(ClassID) override;
    HRESULT STDMETHODCALLTYPE ClassUnloadFinished(ClassID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE FunctionUnloadStarted(FunctionID) override;
    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID, BOOL) override;
    HRESULT STDMETHODCALLTYPE JITCompilationFinished(FunctionID, HRESULT, BOOL) override;
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID, BOOL*) override;
    HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchFinished(FunctionID, COR_PRF_JIT_CACHE) override;
    HRESULT STDMETHODCALLTYPE JITFunctionPitched(FunctionID) override;
    HRESULT STDMETHODCALLTYPE JITInlining(FunctionID, FunctionID, BOOL*) override;
    HRESULT STDMETHODCALLTYPE ThreadCreated(ThreadID) override;
    HRESULT STDMETHODCALLTYPE ThreadDestroyed(ThreadID) override;
    HRESULT STDMETHODCALLTYPE ThreadAssignedToOSThread(ThreadID, DWORD) override;
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationStarted(void) override;
    HRESULT STDMETHODCALLTYPE RemotingClientSendingMessage(GUID*, BOOL) override;
    HRESULT STDMETHODCALLTYPE RemotingClientReceivingReply(GUID*, BOOL) override;
    HRESULT STDMETHODCALLTYPE RemotingClientInvocationFinished(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerReceivingMessage(GUID*, BOOL) override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationStarted(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerInvocationReturned(void) override;
    HRESULT STDMETHODCALLTYPE RemotingServerSendingReply(GUID*, BOOL) override;
    HRESULT STDMETHODCALLTYPE UnmanagedToManagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) override;
    HRESULT STDMETHODCALLTYPE ManagedToUnmanagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendFinished(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeSuspendAborted(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeStarted(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeResumeFinished(void) override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadSuspended(ThreadID) override;
    HRESULT STDMETHODCALLTYPE RuntimeThreadResumed(ThreadID) override;
    HRESULT STDMETHODCALLTYPE MovedReferences(ULONG, ObjectID[], ObjectID[], ULONG[]) override;
    HRESULT STDMETHODCALLTYPE ObjectAllocated(ObjectID, ClassID) override;
    HRESULT STDMETHODCALLTYPE ObjectsAllocatedByClass(ULONG, ClassID[], ULONG[]) override;
    HRESULT STDMETHODCALLTYPE ObjectReferences(ObjectID, ClassID, ULONG, ObjectID[]) override;
    HRESULT STDMETHODCALLTYPE RootReferences(ULONG, ObjectID[]) override;
    HRESULT STDMETHODCALLTYPE ExceptionThrown(ObjectID) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter(FunctionID) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter(FunctionID) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound(FunctionID) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerEnter(UINT_PTR) override;
    HRESULT STDMETHODCALLTYPE ExceptionOSHandlerLeave(UINT_PTR) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter(FunctionID) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter(FunctionID) override;
    HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter(FunctionID, ObjectID) override;
    HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave(void) override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableCreated(ClassID, REFGUID, void*, ULONG) override;
    HRESULT STDMETHODCALLTYPE COMClassicVTableDestroyed(ClassID, REFGUID, void*) override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherFound(void) override;
    HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherExecute(void) override;
    HRESULT STDMETHODCALLTYPE ThreadNameChanged(ThreadID, ULONG, _In_reads_opt_(cchName) WCHAR[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionStarted(int, BOOL[], COR_PRF_GC_REASON) override;
    HRESULT STDMETHODCALLTYPE SurvivingReferences(ULONG, ObjectID[], ULONG[]) override;
    HRESULT STDMETHODCALLTYPE GarbageCollectionFinished(void) override;
    HRESULT STDMETHODCALLTYPE FinalizeableObjectQueued(DWORD, ObjectID) override;
    HRESULT STDMETHODCALLTYPE RootReferences2(ULONG, ObjectID[], COR_PRF_GC_ROOT_KIND[], COR_PRF_GC_ROOT_FLAGS[], UINT_PTR[]) override;
    HRESULT STDMETHODCALLTYPE HandleCreated(GCHandleID, ObjectID) override;
    HRESULT STDMETHODCALLTYPE HandleDestroyed(GCHandleID) override;
    HRESULT STDMETHODCALLTYPE InitializeForAttach(IUnknown*, void*, UINT) override;
    HRESULT STDMETHODCALLTYPE ProfilerAttachComplete(void) override;
    HRESULT STDMETHODCALLTYPE ProfilerDetachSucceeded(void) override;
    HRESULT STDMETHODCALLTYPE ReJITCompilationStarted(FunctionID, ReJITID, BOOL) override;
    HRESULT STDMETHODCALLTYPE GetReJITParameters(ModuleID, mdMethodDef, ICorProfilerFunctionControl*) override;
    HRESULT STDMETHODCALLTYPE ReJITCompilationFinished(FunctionID, ReJITID, HRESULT, BOOL) override;
    HRESULT STDMETHODCALLTYPE ReJITError(ModuleID, mdMethodDef, FunctionID, HRESULT) override;
    HRESULT STDMETHODCALLTYPE MovedReferences2(ULONG, ObjectID[], ObjectID[], SIZE_T[]) override;
    HRESULT STDMETHODCALLTYPE SurvivingReferences2(ULONG, ObjectID[], SIZE_T[]) override;
    HRESULT STDMETHODCALLTYPE ConditionalWeakTableElementReferences(ULONG, ObjectID[], ObjectID[], GCHandleID[]) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyReferences(const WCHAR*, ICorProfilerAssemblyReferenceProvider*) override;
    HRESULT STDMETHODCALLTYPE ModuleInMemorySymbolsUpdated(ModuleID) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationStarted(FunctionID, BOOL, LPCBYTE, ULONG) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodJITCompilationFinished(FunctionID, HRESULT, BOOL) override;
    HRESULT STDMETHODCALLTYPE DynamicMethodUnloaded(FunctionID) override;
    HRESULT STDMETHODCALLTYPE EventPipeEventDelivered(EVENTPIPE_PROVIDER, DWORD, DWORD, ULONG, LPCBYTE, ULONG, LPCBYTE, LPCGUID, LPCGUID, ThreadID, ULONG, UINT_PTR[]) override;
    HRESULT STDMETHODCALLTYPE EventPipeProviderCreated(EVENTPIPE_PROVIDER) override;

private:
    std::atomic<int> ref_count_;
};

}

#endif