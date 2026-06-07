@echo off
title llama-server: Qwen3.6-27B-UD-Q5_K_XL
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwen3.6-27B-UD-Q5_K_XL\Qwen3.6-27B-UD-Q5_K_XL.gguf" ^
 --mmproj "D:\Models\GGUF\Others\Qwen3.6-27B-UD-Q5_K_XL\mmproj-F16.gguf" ^
 -c 32768 ^
 -b 2048 ^
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