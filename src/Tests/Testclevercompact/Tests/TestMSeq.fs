namespace Testclevercompact.Tests

open System.Threading
open CleverCompact
open NUnit.Framework
open FsUnit


[<TestFixture>]
module TestMSeq =

    [<Test>]
    let ``longLength returns valid length`` () =
        let seq = Seq.init 123 (fun _ -> ())

        MSeq.longLength seq
        |> should equal 123L

    [<Test>]
    let ``runParallel runs all jobs`` ([<Random(100, 1000, 10, Distinct = true)>] jobCount,
                                       [<Random(1, 100, 10, Distinct = true)>] jobParallelCount) =
        let mutable actualRun = 0L
        let jobs =
            Seq.init jobCount (fun _ -> (fun () -> Interlocked.Increment &actualRun |> ignore))

        MSeq.runParallel jobParallelCount jobs
        actualRun |> should equal (int64 jobCount)

        MSeq.runParallel jobParallelCount jobs
        actualRun |> should equal (int64 (jobCount * 2))



