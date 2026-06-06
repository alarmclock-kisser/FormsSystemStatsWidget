@echo off
title llama-server: gemma-4-12B-it-Q8_0 + MTP
llama-server ^
 -m "D:\Models\GGUF\Others\gemma-4-12B-it-Q8_0\gemma-4-12B-it-Q8_0.gguf" ^
 -md "D:\Models\GGUF\Others\gemma-4-12B-it-Q8_0\gemma-4-12B-it-assistant-Q8_0.gguf" ^
 --spec-type draft-mtp ^
 --spec-draft-n-max 3 ^
 -c 131072 ^
 -b 4096 ^
 -sm layer ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 1 ^
 --no-warmup ^
 --kv-offload ^
 -fit off ^
 -tb 4096 ^
 -ctk q8_0 ^
 -ctv q8_0

pause