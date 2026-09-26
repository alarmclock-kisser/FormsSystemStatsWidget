# Implementierungsstatus - FSSW ONNX GenAI Server

**Stand:** 2026-09-26  
**Fortschrittsquelle:** [master-requirement_progress.md](master-requirement_progress.md)

## Kurzfassung

Der reale API-Pfad funktioniert vom C#-Host ueber Python-IPC bis zur Qwen-Inferenz. Der C#-Host hat das Modell geladen und `POST /v1/chat/completions` hat erfolgreich OpenAI-kompatible SSE-Daten geliefert. Das ist ein wichtiger Integrationsmeilenstein, aber noch kein Abschluss der API-, Streaming- oder Gesamtanforderungen.

## Verifiziert

- C#-Server-Build fuer `net10.0` erfolgreich. Vorhandene Warnungen: CS8602 in `PythonIpcClient.cs` und ASP0000 in `Program.cs`.
- `Qwen3.8-27B-onnx-int4` wurde vom C#-gesteuerten Python-Child geladen; C# `/health` meldete `engineReady=true`.
- Reale Anfrage an `/v1/chat/completions`: HTTP 200, `text/event-stream`, echte Qwen-Token, genau ein `finish_reason=stop` und ein `[DONE]`.
- Doppelte SSE-Finalisierung in `PythonIpcClient` behoben. Der Fallback-Finalwert wird nur noch gesendet, wenn kein expliziter Finish-Chunk empfangen wurde.
- Der zuvor gemeldete ORT-Fehler `bad allocation` trat beim isolierten Lauf mit einem Child auf Port 8082 nicht erneut auf. Die frueheren Mehrfachprozesse auf Port 8081 machen die urspruengliche Ursache jedoch ungeklaert.
- Letzter vollstaendiger Python-Suite-Lauf aus der vorherigen Session: 51 bestanden, 1 uebersprungen. Echter Python-Dual-GPU-Prefill/Decode-Test auf GPU 0/1 war ebenfalls zuvor erfolgreich; beides wurde in dieser Session nicht erneut ausgefuehrt.

## Phasenstand

| Bereich | Status | Einordnung |
| --- | --- | --- |
| Phasen 1-3 | Abgeschlossen | Infrastruktur, Modellvalidierung und Python-Prozess/IPC-Grundlage |
| Phase 4 | In Arbeit | 4a-4c abgeschlossen; 4d Dual-Device Snapshot/Restore offen |
| Phasen 5-10 | Noch offen | Boundary, Prefill/Decode/Sampling, Tokenizer und Chat-Templates |
| Phase 11 | In Arbeit | Reale Chat-Completions-Anfrage funktioniert; Kompatibilitaet und Prompt-Verhalten offen |
| Phase 12 | In Arbeit | SSE real verifiziert; Finalisierungsduplikat behoben, Fehler-/Abbruchpfade offen |
| Phasen 13-15 | Noch offen | Benchmarking, Stress-Tests und Optimierung |

## Offene Punkte

1. Der kurze Chat-Testprompt wurde teilweise wiederholt, statt wie verlangt beantwortet zu werden. Chat-Template und Prompt-Aufbereitung fuer R25/R43 pruefen.
2. C#-Discovery protokolliert das Modell weiterhin als nicht partitioniert/Single-Stage, waehrend Python das Paket laden und generieren konnte. Stage-Erkennung und tatsaechliche GPU-Zuordnung im C#-gesteuerten Lauf explizit verifizieren.
3. Phase 4d: Snapshot/Restore fuer beide Stage-State-Dictionaries implementieren und testen.
4. Phase 5: Boundary-Handoff, Device-Residency und GPU-Speicheranforderungen vollstaendig nachweisen.
5. API/Streaming: Non-Streaming, Abbruch, Fehlerantworten und breitere OpenAI-Kompatibilitaet gezielt testen.
6. Den frueheren `bad allocation`-Fehler bei Bedarf mit genau einem Python-Child und geprueften Speicher-/GPU-Ressourcen weiter untersuchen.

## Laufzeit- und Prozessnotiz

- Modell: `D:\Models\ONNX\Qwen3.8-27B-onnx-int4`.
- Python: `C:\Python314\python.exe`; im erfolgreichen Test C#-Host auf Port 8280 und Python-Child auf Port 8082.
- Der von diesem Test gestartete Host und Child wurden beendet; Ports 8280 und 8082 waren beim letzten Cleanup frei.
- Ein aelterer, nicht von diesem Test gestarteter Python-Prozess auf Port 8081 (PID 7532) blieb unangetastet. Sein letzter `/health`-Status war `ready=false`, `status=unloaded`. Vor dem naechsten Modellstart Prozess- und GPU-Belegung erneut pruefen.

## Naechster Meilenstein

Phase 4d bleibt der naechste Phasen-Meilenstein. Den Chat-Template-Fehler und die Single-Stage-Diagnose bei Gelegenheit als gezielte Integrationschecks weiterverfolgen; Phase 5 erst nach Abschluss von Phase 4 beginnen.