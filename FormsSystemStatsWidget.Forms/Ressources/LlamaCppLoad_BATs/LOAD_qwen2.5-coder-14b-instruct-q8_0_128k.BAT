@echo off
title llama-server: qwen2.5-coder-14b-instruct-q8_0
llama-server ^
 -m "D:\\Models\GGUF\Others\qwen2.5-coder-14b-instruct-q8_0\qwen2.5-coder-14b-instruct-q8_0.gguf" ^
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
 -tb 2048

pause