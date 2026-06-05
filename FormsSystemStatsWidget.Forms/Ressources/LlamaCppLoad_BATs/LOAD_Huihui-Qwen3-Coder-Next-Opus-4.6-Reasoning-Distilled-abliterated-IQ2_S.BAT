@echo off
title llama-server: Huihui-Qwen3-Coder-Next-Opus-4.6-Reasoning-Distilled-abliterated-IQ2_S
llama-server ^
 -m "D:\\Models\GGUF\Others\Huihui-Qwen3-Coder-Next-Opus-4.6-Reasoning-Distilled-abliterated-IQ2_S\Huihui-Qwen3-Coder-Next-Opus-4.6-Reasoning-Distilled-abliterated-IQ2_S.gguf" ^
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