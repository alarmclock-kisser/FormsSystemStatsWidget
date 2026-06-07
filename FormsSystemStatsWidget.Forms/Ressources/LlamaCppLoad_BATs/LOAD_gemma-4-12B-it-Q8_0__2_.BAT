@echo off
title llama-server: gemma-4-12B-it-Q8_0
llama-server ^
 -m "D:\\Models\GGUF\Others\gemma-4-12B-it-Q8_0\gemma-4-12B-it-Q8_0.gguf" ^
 --mmproj "D:\Models\GGUF\Others\gemma-4-12B-it-Q8_0\mmproj-gemma-4-12B-it-bf16.gguf" ^
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
 -tb 2048

pause