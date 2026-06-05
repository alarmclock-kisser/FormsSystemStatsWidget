@echo off
title llama-server: Qwen2.5-Coder-32B-Q4_K_M
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwen2.5-Coder-32B-Q4_K_M\Qwen2.5-Coder-32B-Q4_K_M.gguf" ^
 -c 65536 ^
 -b 4096 ^
 -sm row ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 2 ^
 --no-warmup ^
 --no-kv-offload ^
 -fit off ^
 -tb 8192

pause