# clevercompact

This is a wrapper around Windows builtin [compact](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/compact)
utility.

`compact` has `/EXE` flag which uses compression algorithm optimized for executable files, but you can either tell `compact`
to compress all files with `/EXE` flag or compress all files using default algorithm. There is no way to tell `compact` to
compress executable files (`*.exe`, `*.dll`) using `/EXE` flag and other files using default algorithm.

`clevercompact`, on the other hand, checks file extension and if it is executable, it invokes `compact /EXE`, otherwise,
it invokes `compact`. That's the only difference.

`clevercompact` works only on Windows 10, Windows 11.

## Building

```
dotnet build clevercompact.fsproj -c Release --runtime win-x64 --self-contained --no-incremental
dotnet publish clevercompact.fsproj -c Release --runtime win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Exe file will be placed at `./publish` directory.

## Usage

```
USAGE: clevercompact [--help] [--quiet] [--recursive] [--dryrun] [--usedefcompact] [--jobs <int>] [--exesonly]
                     [--notexes] [--inputs [<string>...]]

OPTIONS:

    --quiet, -q           Do not print progress & stats.
    --recursive, -r       Process input directories recursively.
    --dryrun, --dry-run   Do not actually compact. Just print progress & stats.
    --usedefcompact       Use default compact algorithm for all files.
    --jobs <int>          How many compact jobs run in parallel. Defaults equal to cpu count.
    --exesonly            compact only executable files.
    --notexes             compact everything but executable files.
    --inputs, -i [<string>...]
                          Input paths to compact(file & dirs).
    --help                display this list of options.
```

