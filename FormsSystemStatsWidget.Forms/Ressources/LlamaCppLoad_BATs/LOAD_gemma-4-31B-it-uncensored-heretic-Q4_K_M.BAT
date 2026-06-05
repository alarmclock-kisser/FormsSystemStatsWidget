@echo off
title llama-server: gemma-4-31B-it-uncensored-heretic-Q4_K_M
llama-server ^
 -m "D:\\Models\GGUF\Others\gemma-4-31B-it-uncensored-heretic-Q4_K_M\gemma-4-31B-it-uncensored-heretic-Q4_K_M.gguf" ^
 -c 131072 ^
 -b 4096 ^
 -sm row ^
 -ts 16,12 ^
 -fa on ^
 -ngl 999 ^
 -np 1 ^
 --no-warmup ^
 --no-kv-offload ^
 -fit off ^
 -tb 8192

pause