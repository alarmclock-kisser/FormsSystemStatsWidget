@echo off
title llama-server: gemma-4-A4B-98e-v6-coder-it-Q6_K_L
llama-server ^
 -m "D:\\Models\GGUF\Others\gemma-4-A4B-98e-v6-coder-it-Q6_K_L\gemma-4-A4B-98e-v6-coder-it-Q6_K_L.gguf" ^
 -c 131072 ^
 -b 4096 ^
 -sm layer ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 2 ^
 --no-warmup ^
 --kv-offload ^
 -fit on ^
 -tb 4096

pause