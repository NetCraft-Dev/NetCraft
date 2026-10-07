//Entry point of the NetCraft native layer
//The CLR loads this library as a profiler when the CORECLR_* environment variables are present, which happens before
//any managed code in the process has run; that makes the first callback the right place to install the handler that
//watches for a stack overflow, and nothing here needs the managed side to be up yet

#include <atomic>
#include <cstdint>
#include <cstring>
#include <unknwn.h>
#include <cor.h>
#include <corprof.h>

#include "com_base.h"

//The CLSID the CORECLR_PROFILER variable must carry
//ProfilerRelaunch writes the same value on the managed side and the two have to stay in step
static const GUID ClsidNetCraftNative = {
    0x2B5F8D34, 0x6C1A, 0x4E27, {0x9B, 0x3D, 0x52, 0xE8, 0x71, 0x4A, 0xC6, 0x18}};

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
int32_t ncn_com_get_class_object(const GUID* rclsid, const GUID* riid, void** ppv_object);
int32_t ncn_com_can_unload_now();
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

//Profiler overrides only the two lifecycle callbacks
//Everything else stays on the generated defaults because this profiler does not subscribe to any event
class Profiler final : public ComBase {
public:
    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override
    {
        //The interface is forwarded untouched; the Rust side keeps it in case a later module needs it
        return ncn_on_initialize(pICorProfilerInfoUnk) == 0 ? S_OK : E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE Shutdown() override
    {
        ncn_on_shutdown();
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
