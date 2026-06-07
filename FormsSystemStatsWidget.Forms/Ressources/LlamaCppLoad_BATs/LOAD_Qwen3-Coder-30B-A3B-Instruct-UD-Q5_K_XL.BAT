@echo off
title llama-server: Qwen3-Coder-30B-A3B-Instruct-UD-Q5_K_XL
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwen3-Coder-30B-A3B-Instruct-UD-Q5_K_XL\Qwen3-Coder-30B-A3B-Instruct-UD-Q5_K_XL.gguf" ^
 -c 131072 ^
 -b 4096 ^
 -sm layer ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 1 ^
 --no-warmup ^
 --no-kv-offload ^
 -fit off ^
 -tb 4096

pause