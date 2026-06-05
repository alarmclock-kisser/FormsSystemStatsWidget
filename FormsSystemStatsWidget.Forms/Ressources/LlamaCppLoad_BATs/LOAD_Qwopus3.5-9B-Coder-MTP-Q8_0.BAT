@echo off
title llama-server: Qwopus3.5-9B-Coder-MTP-Q8_0
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwopus3.5-9B-Coder-MTP-Q8_0\Qwopus3.5-9B-Coder-MTP-Q8_0.gguf" ^
 -c 131072 ^
 -b 4096 ^
 -sm row ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 4 ^
 --no-warmup ^
 --kv-offload ^
 -fit off ^
 -tb 4096

pause