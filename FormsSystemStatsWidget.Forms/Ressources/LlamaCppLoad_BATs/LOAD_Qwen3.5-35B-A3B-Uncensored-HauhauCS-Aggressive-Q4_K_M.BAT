@echo off
title llama-server: Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-Q4_K_M
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-Q4_K_M\Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf" ^
 --mmproj "D:\Models\GGUF\Others\Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-Q4_K_M\mmproj-Qwen3.5-35B-A3B-Uncensored-HauhauCS-Aggressive-f16.gguf" ^
 -c 65536 ^
 -b 2048 ^
 -sm layer ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 1 ^
 --no-warmup ^
 --no-kv-offload ^
 -fit on ^
 -tb 16384

pause