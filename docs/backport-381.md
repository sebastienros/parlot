# Downlevel delimiter search

On net472 and netstandard2.0, `AnyCharBefore` scans text once to find the next expected delimiter character instead of rescanning the remainder once per expected character. Results are unchanged. `TextBeforeBenchmarks` exercises false starts with multiple delimiter characters; its normal net10.0 run uses the unchanged SearchValues implementation. Load the netstandard2.0 library in a consumer to measure the downlevel change.
