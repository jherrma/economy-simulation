# Language-selection benchmark

Sources behind the measurements in [`../docs/LANGUAGE-CHOICE.md`](../docs/LANGUAGE-CHOICE.md).
All of them implement the same algorithm and **must** print `bought=8072155`; a differing count
means the implementations have diverged and the comparison is void.

| File | Variant |
|---|---|
| `bench.py` | Python, `list.sort(key=...)` |
| `bench.go` | Go, `sort.Slice` — the *slow* idiom, kept to show the difference |
| `bench_go2.go` | Go, `slices.SortFunc` — the idiom used in the results table |
| `bench.c` | C, `qsort` — the *slow* idiom |
| `bench_c2.c` | C, hand-rolled inlined insertion sort — the idiom used in the table |
| `bench.zig` | Zig, `std.mem.sort` — the *slow* idiom |
| `bench_zig2.zig` | Zig, hand-rolled inlined insertion sort — the idiom used in the results table |
| `cs/` | C#, `Array.Sort` with a `Comparison<T>` |

```sh
python3 bench.py
go build -o bench_go2 bench_go2.go   && ./bench_go2
gcc -O2 -o bench_c2 bench_c2.c -lm   && ./bench_c2
zig build-exe -OReleaseFast bench_zig2.zig && ./bench_zig2
cd cs && dotnet build -c Release && ./bin/Release/net10.0/bench
```

Measure by taking the **minimum of 5 interleaved rounds**, not a single run: on the reference
machine single-run noise reached 60%, enough to reorder adjacent languages.
