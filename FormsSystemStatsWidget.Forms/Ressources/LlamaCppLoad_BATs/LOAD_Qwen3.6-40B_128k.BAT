@echo off
title llama-server: Qwen3.6-40B-Deck-Opus2T-OT-IQ4_NL
llama-server ^
 -m "D:\\Models\GGUF\Others\Qwen3.6-40B-Deck-Opus2T-OT-IQ4_NL\Qwen3.6-40B-Deck-Opus2T-OT-IQ4_NL.gguf" ^
 --mmproj "D:\Models\GGUF\Others\Qwen3.6-40B-Deck-Opus2T-OT-IQ4_NL\mmproj-BF16.gguf" ^
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