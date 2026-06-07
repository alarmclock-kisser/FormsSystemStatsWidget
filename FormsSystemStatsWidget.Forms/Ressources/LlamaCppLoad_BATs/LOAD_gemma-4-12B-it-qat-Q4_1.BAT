@echo off
title llama-server: gemma-4-12B-it-qat-q4_0-unquantized.i1-Q4_1
llama-server ^
 -m "D:\\Models\GGUF\Others\gemma-4-12B-it-qat-q4_0-unquantized.i1-Q4_1\gemma-4-12B-it-qat-q4_0-unquantized.i1-Q4_1.gguf" ^
 --mmproj "D:\Models\GGUF\Others\gemma-4-12B-it-qat-q4_0-unquantized.i1-Q4_1\mmproj-F16.gguf" ^
 -c 262144 ^
 -b 4096 ^
 -sm layer ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 1 ^
 --no-warmup ^
 --kv-offload ^
 -fit off ^
 -tb 4096

pause