module CleverCompact.MSeq

open System
open System.Threading.Tasks

let longLength (seq: 'a seq) : int64 =
    let mutable result = 0L
    use enumerator = seq.GetEnumerator ()

    while enumerator.MoveNext() do
        result <- result + 1L

    result

let runParallel (maxJobs: int) (jobs: (unit -> unit) seq) =
    if maxJobs <= 0 then
        failwithf "Invalid argument value: maxJobs can't be <= 0 (%d)" maxJobs

    let currentJobs = System.Collections.Generic.List<Task> maxJobs

    let waitForSomeJobs () = Task.WhenAny(currentJobs).Wait()

    let checkExceptions () =
        let errors =
            currentJobs
            |> Seq.filter (fun task -> task.Status = TaskStatus.Faulted)
            |> Seq.map (fun task -> task.Exception :> Exception)
            |> List.ofSeq

        if not (List.isEmpty errors) then
            raise (AggregateException("Some of jobs have faulted.", errors))

    let removeCompletedJobs () =
        currentJobs.RemoveAll(fun task -> task.IsCompleted) |> ignore


    for job in jobs do
        if currentJobs.Count < maxJobs then
            currentJobs.Add(Task.Run(job))
        else
            waitForSomeJobs     ()
            checkExceptions     ()
            removeCompletedJobs ()
            currentJobs.Add(Task.Run(job))

    Task.WaitAll (Array.ofSeq currentJobs)
    checkExceptions ()
