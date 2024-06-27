module CleverCompact.Helpers

open System

let timer (interval: TimeSpan) (f: unit -> unit) =

    let locker = obj ()
    let mutable isDisposed = false
    let timer = new System.Timers.Timer (interval)
    timer.AutoReset <- false

    timer.Elapsed.Add(fun _ -> lock locker (fun () ->
        if not isDisposed then
            try
                f ()
            with
            | _ -> ()

            timer.Start()))

    timer.Start ()

    { new IDisposable with

        member _.Dispose () =
            lock locker (fun () ->
                if not isDisposed then
                    isDisposed <- true
                    timer.Stop ()
                    timer.Dispose ()) }
