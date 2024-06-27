module CleverCompact.Program

open Argu
open ByteSizeLib
open Compact
open Helpers
open System
open System.Diagnostics
open System.IO
open System.Threading


type CliArgs =
    | [<AltCommandLine("-q")>] Quiet
    | [<AltCommandLine("-r")>] Recursive
    | [<AltCommandLine("--dry-run")>] Dryrun
    | UseDefCompact
    | Jobs of int

    | ExesOnly
    | NotExes

    | [<AltCommandLine("-i")>] Inputs of string list

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Quiet -> "Do not print progress & stats."
            | Recursive -> "Process input directories recursively."
            | Dryrun -> "Do not actually compact. Just print progress & stats."
            | UseDefCompact -> "Use default compact algorithm for all files."
            | Jobs _ -> "How many compact jobs run in parallel. Defaults equal to cpu count."
            | ExesOnly -> "compact only executable files."
            | NotExes -> "compact everything but executable files."
            | Inputs _ -> "Input paths to compact(file & dirs)."


let console = CleverCompact.Console ()

let getProgressLine (current: double) (total: double) barCount =
    let bars, percent =
        if total = 0.0 then
            0, 0.0
        else
            (int (current / total * (double barCount))) , (current / total * 100.0)

    let line = Seq.init barCount (fun i -> if i <= bars then "#" else " ") |> String.concat ""
    sprintf "[%s]: %3.2f%%" line percent

let pathExists path = File.Exists path || Directory.Exists path

let isExecutable (file: FileSystemInfo) =
    match file with
    | :? FileInfo as file ->
        let ext = file.Extension.ToLowerInvariant()
        ext = ".exe" || ext = ".dll"
    | _ -> false

let rec enumeratePath path recursive : FileSystemInfo seq = seq {
    if File.Exists path then
        yield FileInfo path

    elif Directory.Exists path then
        yield DirectoryInfo path

        if recursive then
            let attrs = FileAttributes.System ||| FileAttributes.Device ||| FileAttributes.Offline ||| FileAttributes.Encrypted |||
                        FileAttributes.Temporary
            let opts = EnumerationOptions(AttributesToSkip = attrs, IgnoreInaccessible = true, RecurseSubdirectories = false)
            yield!
                Directory.EnumerateFileSystemEntries (path, "*", opts)
                |> Seq.collect (fun p -> enumeratePath p recursive)
}

let getFilesToProcess (cli: ParseResults<CliArgs>) (files: FileSystemInfo seq) =
    if cli.Contains ExesOnly then
        files
        |> Seq.filter isExecutable
    elif cli.Contains NotExes then
        files
        |> Seq.filter (isExecutable >> not)
    else
        files



[<EntryPoint>]
let main argv =
    try
        let stopwatch = Stopwatch.StartNew ()

        let cliArgs = ArgumentParser.Create<CliArgs>().ParseCommandLine argv

        let quiet = cliArgs.Contains Quiet
        let recursive = cliArgs.Contains Recursive
        let dryrun = cliArgs.Contains Dryrun
        let useDefCompact = cliArgs.Contains UseDefCompact
        let parallelJobs = cliArgs.GetResult (Jobs, defaultValue = Environment.ProcessorCount)
        let parallelJobs = if parallelJobs <= 0 then Environment.ProcessorCount else parallelJobs
        let inputs = cliArgs.GetResult (Inputs, defaultValue = [])

        let filesToProcess =
            inputs
            |> Seq.ofList
            |> Seq.collect (fun path -> enumeratePath path recursive)
            |> getFilesToProcess cliArgs

        let totalFiles =
            if quiet then
                0L
            else
                MSeq.longLength filesToProcess

        let mutable currentFiles = 0L
        let mutable uncompressedBytes = 0L
        let mutable compressedBytes = 0L
        let mutable errors = 0L

        let logProgress : (unit -> unit) =
            if quiet then
                (fun () -> ())
            else
                (fun () ->
                    let line = getProgressLine (double currentFiles) (double totalFiles) 30
                    console.UpdateLines [
                        "Compressing..."
                        sprintf "File %7d of %7d total" currentFiles totalFiles
                        line
                    ])

        let processFile (file: FileSystemInfo) =
            let result =
                if not dryrun then
                    if useDefCompact then
                        compact file.FullName
                    elif isExecutable file then
                        compactExe file.FullName
                    else
                        compact file.FullName
                else
                    Ok ()

            Interlocked.Increment &currentFiles |> ignore
            match result with
            | Ok () -> ()
            | Error err ->
                console.Eprintfn "Error on compacting \"%s\"" file.FullName
                console.Eprintfn "%s" err
                Interlocked.Increment &errors |> ignore

            if file :? FileInfo then
                match getFileSizes file.FullName with
                | Ok sizes ->
                    Interlocked.Add (&uncompressedBytes, sizes.UncompressedSizeBytes) |> ignore
                    Interlocked.Add (&compressedBytes, sizes.CompressedSizeBytes) |> ignore
                | Error err ->
                    console.Eprintfn "Error on getting file size for \"%s\"" file.FullName
                    console.Eprintfn "%s" err
                    Interlocked.Increment &errors |> ignore


        use timer = timer (TimeSpan.FromSeconds 2.0) logProgress

        filesToProcess
        |> Seq.map (fun file -> (fun () -> processFile file))
        |> MSeq.runParallel parallelJobs

        timer.Dispose ()
        logProgress ()

        let ratio =
            if compressedBytes = 0 then
                0.0
            else
                (double uncompressedBytes) / (double compressedBytes)

        console.Printfn "Took...............: %O" stopwatch.Elapsed
        console.Printfn "Total files........: %d" currentFiles
        console.Printfn "Errors.............: %d" errors
        console.Printfn "Uncompressed size..: %O" (ByteSize.FromBytes (double uncompressedBytes))
        console.Printfn "Compressed size....: %O" (ByteSize.FromBytes (double compressedBytes))
        console.Printfn "Ratio..............: %3.3f" ratio

        0
    with
    | :? ArguParseException as exc ->
        eprintfn "%s" exc.Message
        1
    | exc ->
        eprintfn "%O" exc
        2

