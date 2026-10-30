# Implementierungsstatus - FSSW ONNX GenAI Server

**Stand:** 2026-09-26  
**Fortschrittsquelle:** [master-requirement_progress.md](master-requirement_progress.md)

## Kurzfassung

Phase 4 ist einschliesslich Dual-Device-Snapshot/Restore abgeschlossen. Der partitionierte Qwen-Pfad funktioniert real ueber C# OpenAI-API → Python-IPC → beide ONNX-Stages: die strukturierte Anfrage lieferte exakt `4`, zwei Completion-Tokens und `finish_reason=stop`. Prozessbereinigung ist graceful; Sampling-Parameter sind end-to-end verdrahtet.

## Verifiziert

- Python-Suite vor den letzten Python-Patches: 60 passed, 1 skipped; danach 2 fokussierte Sampling-Tests und 1 EOS-Regressionstest bestanden. Pylance-Syntaxpruefung fuer Generator/Test sauber.
- C#-Server-Build erfolgreich; die bekannten Warnungen CS8602 (`PythonIpcClient.cs`) und ASP0000 (`Program.cs`) bleiben.
- Phase 4d: Dual-Device-Snapshot/Restore, 21 fokussierte Tests und realer Snapshot/Restore-Lauf wurden zuvor erfolgreich abgeschlossen.
- Realer Qwen-Prozess-Lifecycle: `/load` und `/unload` lieferten 200; derselbe PID blieb nach Unload als `Unloaded` erreichbar. Idle-Shutdown wurde protokolliert; anschliessend waren Worker-PID und Listener weg.
- C#-Lifecycle-Smoke: Dummy-Worker mit PID/Instance-ID registriert, graceful gestoppt und aus der Registry entfernt; stale Registry-PID wurde beim Start entfernt.
- Sampling: `min_p`, Presence-/Frequency-Penalty und Seed werden von Chat-/Completion-DTO ueber C#-IPC bis Python `SamplingConfig` weitergereicht. `min_p`-Grenzen werden validiert.
- Reale strukturierte C#-Chat-Anfrage nach EOS-Fix: HTTP 200, Inhalt exakt `4`, `completion_tokens=2`, `finish_reason=stop`.
- `GenerationEngine` ergänzt Tokenizer-EOS zur Stop-Konfiguration und unterdrückt EOS-/Stop-Control-Tokens im sichtbaren Text.
- C#-Partition-Discovery sucht jetzt auch im `partitioned/`-Unterordner; Build erfolgreich.

## Phasenstand

| Bereich | Status | Einordnung |
| --- | --- | --- |
| Phasen 1-3 | Abgeschlossen | Infrastruktur, Modellvalidierung und Python-Prozess/IPC-Grundlage |
| Phase 4 | Abgeschlossen | 4a-4d einschliesslich Dual-Device Snapshot/Restore |
| Phase 5 | In Arbeit | Device-resident Boundary bestaetigt; Stage-/State-/Temporary-Memory-Tracking teilweise offen |
| Phasen 6-7 | Abgeschlossen | Reales Multi-token-Prefill und persistenter Decode-Pfad vorhanden |
| Phase 8 | In Arbeit | Sampling erweitert; verbleibende Sampler-/Stop-Paritaet offen |
| Phasen 9-10 | Weitgehend verifiziert | Modell-Tokenizer und Jinja-Chat-Template funktionieren im realen Qwen-API-Pfad |
| Phase 11 | In Arbeit | Reale Chat-Completions-Anfrage funktioniert; Kompatibilitaet und Prompt-Verhalten offen |
| Phase 12 | In Arbeit | SSE real verifiziert; Finalisierungsduplikat behoben, Fehler-/Abbruchpfade offen |
| Phasen 13-15 | Noch offen | Benchmarking, Stress-Tests und Optimierung |

## Offene Punkte

1. R34-Memory-Diagnostik best-effort fuer Stage/State/Temporary-Memory ergaenzen, nur soweit Runtime-APIs dies ohne CPU-Kopie erlauben.
2. Sampling-/Stop-Paritaet und OpenAI API: Abbruch, Fehlerantworten und kompatible Request-Optionen schliessen.
3. Timing-/Live-Statistik-Luecken sowie Benchmark-/Stress-Anforderungen nach Aufwand-Nutzen priorisieren.

## Laufzeit- und Prozessnotiz

- Modell: `D:\Models\ONNX\Qwen3.8-27B-onnx-int4`.
- Python: `C:\Python314\python.exe`; realer Lifecycle-Test auf Port 8092, Worker nach Idle-Shutdown nicht mehr vorhanden.
- C#-Dummy-Host/Worker wurden graceful beendet und aus der isolierten Test-Registry entfernt. Der reale Chat-Host/Worker (PID 25884) wurde ebenfalls graceful beendet; Registry ist leer.
- Zuletzt gepruefte Ports 8092, 8093 und 8285: kein Listener; `tasklist` zeigte keinen Python-Prozess. Verbleibende TCP-Eintraege waren nur Verbindungsabbau. Kein Prozess wurde pauschal beendet.

## Naechster Meilenstein

Naechster Schritt mit hohem Nutzen pro Aufwand: R34 best-effort Memory-Diagnostik und API-/Streaming-Randfaelle. Der reale partitionierte Qwen-Chat und Prozess-Lifecycle sind bestaetigt; keine weitere Arbeit an Phase 4d.