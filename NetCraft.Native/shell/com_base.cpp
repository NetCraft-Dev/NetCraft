//Generated from corprof.h, do not edit by hand

#include "com_base.h"

namespace netcraft_native {

ComBase::ComBase() : ref_count_(0) {}

ComBase::~ComBase() = default;

HRESULT STDMETHODCALLTYPE ComBase::Initialize(IUnknown*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::Shutdown(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AppDomainCreationStarted(AppDomainID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AppDomainCreationFinished(AppDomainID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AppDomainShutdownStarted(AppDomainID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AppDomainShutdownFinished(AppDomainID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AssemblyLoadStarted(AssemblyID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AssemblyLoadFinished(AssemblyID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AssemblyUnloadStarted(AssemblyID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::AssemblyUnloadFinished(AssemblyID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleLoadStarted(ModuleID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleLoadFinished(ModuleID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleUnloadStarted(ModuleID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleUnloadFinished(ModuleID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleAttachedToAssembly(ModuleID, AssemblyID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ClassLoadStarted(ClassID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ClassLoadFinished(ClassID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ClassUnloadStarted(ClassID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ClassUnloadFinished(ClassID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::FunctionUnloadStarted(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITCompilationStarted(FunctionID, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITCompilationFinished(FunctionID, HRESULT, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITCachedFunctionSearchStarted(FunctionID, BOOL*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITCachedFunctionSearchFinished(FunctionID, COR_PRF_JIT_CACHE) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITFunctionPitched(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::JITInlining(FunctionID, FunctionID, BOOL*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ThreadCreated(ThreadID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ThreadDestroyed(ThreadID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ThreadAssignedToOSThread(ThreadID, DWORD) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingClientInvocationStarted(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingClientSendingMessage(GUID*, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingClientReceivingReply(GUID*, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingClientInvocationFinished(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingServerReceivingMessage(GUID*, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingServerInvocationStarted(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingServerInvocationReturned(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RemotingServerSendingReply(GUID*, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::UnmanagedToManagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ManagedToUnmanagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeSuspendFinished(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeSuspendAborted(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeResumeStarted(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeResumeFinished(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeThreadSuspended(ThreadID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RuntimeThreadResumed(ThreadID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::MovedReferences(ULONG, ObjectID[], ObjectID[], ULONG[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ObjectAllocated(ObjectID, ClassID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ObjectsAllocatedByClass(ULONG, ClassID[], ULONG[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ObjectReferences(ObjectID, ClassID, ULONG, ObjectID[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RootReferences(ULONG, ObjectID[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionThrown(ObjectID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionSearchFunctionEnter(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionSearchFunctionLeave(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionSearchFilterEnter(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionSearchFilterLeave(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionSearchCatcherFound(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionOSHandlerEnter(UINT_PTR) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionOSHandlerLeave(UINT_PTR) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionUnwindFunctionEnter(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionUnwindFunctionLeave(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionUnwindFinallyEnter(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionUnwindFinallyLeave(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionCatcherEnter(FunctionID, ObjectID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionCatcherLeave(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::COMClassicVTableCreated(ClassID, REFGUID, void*, ULONG) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::COMClassicVTableDestroyed(ClassID, REFGUID, void*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionCLRCatcherFound(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ExceptionCLRCatcherExecute(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ThreadNameChanged(ThreadID, ULONG, _In_reads_opt_(cchName) WCHAR[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::GarbageCollectionStarted(int, BOOL[], COR_PRF_GC_REASON) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::SurvivingReferences(ULONG, ObjectID[], ULONG[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::GarbageCollectionFinished(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::FinalizeableObjectQueued(DWORD, ObjectID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::RootReferences2(ULONG, ObjectID[], COR_PRF_GC_ROOT_KIND[], COR_PRF_GC_ROOT_FLAGS[], UINT_PTR[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::HandleCreated(GCHandleID, ObjectID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::HandleDestroyed(GCHandleID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::InitializeForAttach(IUnknown*, void*, UINT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ProfilerAttachComplete(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ProfilerDetachSucceeded(void) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ReJITCompilationStarted(FunctionID, ReJITID, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::GetReJITParameters(ModuleID, mdMethodDef, ICorProfilerFunctionControl*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ReJITCompilationFinished(FunctionID, ReJITID, HRESULT, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ReJITError(ModuleID, mdMethodDef, FunctionID, HRESULT) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::MovedReferences2(ULONG, ObjectID[], ObjectID[], SIZE_T[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::SurvivingReferences2(ULONG, ObjectID[], SIZE_T[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ConditionalWeakTableElementReferences(ULONG, ObjectID[], ObjectID[], GCHandleID[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::GetAssemblyReferences(const WCHAR*, ICorProfilerAssemblyReferenceProvider*) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::ModuleInMemorySymbolsUpdated(ModuleID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::DynamicMethodJITCompilationStarted(FunctionID, BOOL, LPCBYTE, ULONG) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::DynamicMethodJITCompilationFinished(FunctionID, HRESULT, BOOL) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::DynamicMethodUnloaded(FunctionID) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::EventPipeEventDelivered(EVENTPIPE_PROVIDER, DWORD, DWORD, ULONG, LPCBYTE, ULONG, LPCBYTE, LPCGUID, LPCGUID, ThreadID, ULONG, UINT_PTR[]) { return S_OK; }
HRESULT STDMETHODCALLTYPE ComBase::EventPipeProviderCreated(EVENTPIPE_PROVIDER) { return S_OK; }

}
