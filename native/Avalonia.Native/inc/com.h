#pragma clang diagnostic push
#pragma ide diagnostic ignored "OCUnusedGlobalDeclarationInspection"
#ifndef COM_H_INCLUDED
#define COM_H_INCLUDED


typedef struct _GUID {
    unsigned int  Data1;
    unsigned short Data2;
    unsigned short Data3;
    unsigned char  Data4[ 8 ];
} GUID;
typedef GUID IID;
typedef const IID* REFIID;
typedef unsigned int HRESULT;
typedef unsigned int DWORD;
typedef DWORD ULONG;

#define STDMETHODCALLTYPE

#define S_OK                             0x0L

#define E_NOTIMPL                        0x80004001L
#define E_NOINTERFACE                    0x80004002L
#define E_POINTER                        0x80004003L
#define E_ABORT                          0x80004004L
#define E_FAIL                           0x80004005L
#define E_UNEXPECTED                     0x8000FFFFL
#define E_HANDLE                         0x80070006L
#define E_INVALIDARG                     0x80070057L
#define COR_E_INVALIDOPERATION           0x80131509L
#define COR_E_OBJECTDISPOSED             0x80131622L

struct IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE QueryInterface(
            REFIID riid,
            void **ppvObject) = 0;

    virtual ULONG STDMETHODCALLTYPE AddRef( void) = 0;

    virtual ULONG STDMETHODCALLTYPE Release( void) = 0;

};

#ifdef COM_GUIDS_MATERIALIZE
#define __IID_DEF(name,d1,d2,d3, d41, d42, d43, d44, d45, d46, d47, d48) extern "C" const GUID IID_ ## name = {0x ## d1, 0x ## d2, 0x ## d3, \
{0x ## d41, 0x ## d42, 0x ## d42, 0x ## d42, 0x ## d42, 0x ## d42, 0x ## d42, 0x ## d42 } };
#else
#define __IID_DEF(name,d1,d2,d3, d41, d42, d43, d44, d45, d46, d47, d48) extern "C" const GUID IID_ ## name;
#endif
#define COMINTERFACE(name,d1,d2,d3, d41, d42, d43, d44, d45, d46, d47, d48) __IID_DEF(name,d1,d2,d3, d41, d42, d43, d44, d45, d46, d47, d48) \
struct __attribute__((annotate("uuid(" #d1 "-" #d2 "-" #d3 "-" #d41 #d42 "-" #d43 #d44 #d45 #d46 #d47 #d48 ")" ))) name

// Keep __IID_DEF unchanged: existing Avalonia.Native interfaces depend on its
// historical byte layout. New interfaces use this versioned materializer.
#ifdef COM_GUIDS_MATERIALIZE
#define AVN_IID_V2(symbol,d1,d2,d3, d41,d42,d43,d44,d45,d46,d47,d48) \
extern "C" const GUID symbol = {0x ## d1, 0x ## d2, 0x ## d3, \
{0x ## d41, 0x ## d42, 0x ## d43, 0x ## d44, 0x ## d45, 0x ## d46, 0x ## d47, 0x ## d48 } };
#else
#define AVN_IID_V2(symbol,d1,d2,d3, d41,d42,d43,d44,d45,d46,d47,d48) \
extern "C" const GUID symbol;
#endif

#endif // COM_H_INCLUDED
#pragma clang diagnostic pop
