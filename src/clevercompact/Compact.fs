module CleverCompact.Compact

open System.Diagnostics
open System.Runtime.InteropServices
open System
open System.IO
open Microsoft.FSharp.NativeInterop

module private WinApi =

    type BOOL = int32
    type DWORD = uint32

    let GENERIC_READ : DWORD = 0x80000000u
    let FILE_SHARE_READ : DWORD = 0x00000001u
    let OPEN_EXISTING : DWORD = 3u
    let FILE_ATTRIBUTE_NORMAL : DWORD = 0x00000080u

    let NULL : voidptr = NativePtr.nullPtr<uint32> |> NativePtr.toVoidPtr

    [<DllImport("kernel32", SetLastError = true)>]
    extern voidptr CreateFileW([<MarshalAs(UnmanagedType.LPWStr)>]string lpFileName,
                               DWORD dwDesiredAccess,
                               DWORD drShareMode,
                               voidptr lpSecurityAttributes,
                               DWORD dwCreationDisposition,
                               DWORD dwFlagsAndAttributes,
                               voidptr hTemplateFile)

    [<DllImport("kernel32", SetLastError = true)>]
    extern BOOL CloseHandle(voidptr handle)

    [<DllImport("kernel32")>]
    extern DWORD GetLastError()

    [<DllImport("kernel32")>]
    extern void SetLastError(DWORD dwErrCode)

    [<DllImport("kernel32", SetLastError = true)>]
    extern BOOL GetFileSizeEx(voidptr hFile, int64& lpFileSize)

    [<DllImport("kernel32", SetLastError = true)>]
    extern DWORD GetCompressedFileSizeW([<MarshalAs(UnmanagedType.LPWStr)>]string lpFileName,
                                        DWORD& lpFileSizeHigh)

type FileSize =
    { CompressedSizeBytes: int64
      UncompressedSizeBytes: int64 }


let private invokeCompact (path: string) exe =
    let args =
        [ "/C"
          "/F"
          "/I"
          "/A"
          "/Q"
        ] @ if exe then [ "/EXE:LZX" ] else []
        @ [ path ]

    let procStartInfo = ProcessStartInfo("compact", args)
    procStartInfo.RedirectStandardOutput <- true
    use job = Process.Start procStartInfo
    job.WaitForExit ()

    if job.ExitCode <> 0 then
        Error (sprintf "\"compact\"'s exit code is %d" job.ExitCode)
    else
        Ok ()

let compact (path: string) = invokeCompact path false

let compactExe (path: string) = invokeCompact path true

let getFileSizes (path: string) =
    try
        if String.IsNullOrWhiteSpace path then
            failwithf "Invalid argument value: path can't be null,empty,whitespace."

        if not (File.Exists path) then
            failwithf "File not found: %s" path

        WinApi.SetLastError 0u
        let handle: voidptr = WinApi.CreateFileW (path, WinApi.GENERIC_READ, WinApi.FILE_SHARE_READ,
                                                  WinApi.NULL, WinApi.OPEN_EXISTING, WinApi.FILE_ATTRIBUTE_NORMAL,
                                                  WinApi.NULL)
        let lastError = WinApi.GetLastError ()
        if lastError <> 0u then
            failwithf "Can't open file \"%s\". Last error: 0x%x" path lastError

        let mutable uncompressedSize: int64 = 0L
        let failure = WinApi.GetFileSizeEx (handle, &uncompressedSize) = 0
        let lastError = WinApi.GetLastError ()
        WinApi.CloseHandle handle |> ignore

        if failure then
            failwithf "Can't get uncompressed file size. Path \"%s\". Last error: 0x%x" path lastError

        WinApi.SetLastError 0u
        let mutable fileSizeHigh : WinApi.DWORD = 0u
        let mutable fileSizeLow : WinApi.DWORD = WinApi.GetCompressedFileSizeW (path, &fileSizeHigh)
        let lastError = WinApi.GetLastError ()
        if lastError <> 0u then
            failwithf "Can't get compressed file size. Path \"%s\". Last error: 0x%x" path lastError

        Ok { UncompressedSizeBytes = uncompressedSize
             CompressedSizeBytes =  ((int64 fileSizeHigh) <<< 32) ||| (int64 fileSizeLow) }

    with
    | exc -> Error exc.Message
