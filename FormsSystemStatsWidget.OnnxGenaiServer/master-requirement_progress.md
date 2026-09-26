# Master Requirement Progress Tracking

**Source:** master-requirement_Onnx-Genai_Server.md  
**Project:** Native .NET 10 ONNX Multi-GPU LLM Engine  
**Current Date:** 2026-09-26  
**Total Requirements:** 103 (101 + Definition of Done + Initial Acceptance Test)  
**Status:** In Progress - Tracking development against all requirements

**⚠️ Python Inference Engine (HART):**
- **Code:** `PyDualOnnxInferenceEngine/` (im Projekt-Root)
- **Integrations-Doku:** `PyDualOnnxInferenceEngine/COPILOT_ONNX_PYTHON_ENGINE_INTEGRATION.md`
- **Regel:** C# baut NUR die Infrastruktur drumherum (HTTP, OpenAI-API, Config, Validation, Diagnostics, Python-Process-Supervision, IPC). Die komplette Inference (Tokenizer, Chat-Template, Prefill, Decode, Sampling, KV-State, ONNX Runtime) läuft im Python-Prozess.
- **NIE** Inference-Logik in C# implementieren. **NIE** ONNX-NuGets hinzufügen.

---

## 📋 Arbeitsprotokoll für AI-Assistenten (universell gültig)

> **Diese Notiz gilt für alle zukünftigen AI-Instanzen, die an diesem Projekt arbeiten.**

### Grundprinzip

1. **Immer am aktuellen Stand arbeiten:** Lies zuerst diese Progress-Datei und das Master-Requirement-MD. Der aktuelle Stand ist hier dokumentiert — nicht in der Konversation.
2. **Eine Phase pro Session:** Arbeite ein zusammenhängendes Arbeitspaket (1 Phase oder 2-4 zusammengehörige Requirements) pro Kontext-Session. Ziel: ~128k Token Kontext ausnutzen, ohne compacten zu müssen.
3. **Phase abschließen, dann finalisieren:** Wenn die Phase fertig ist, trage den Fortschritt in diese Progress-Datei ein (Status, Notes, Next Actions). Dann ist die Session abgeschlossen.
4. **Nächste Phase in frischem Kontext:** Die nächste Phase wird in einem neuen, frischen Kontext gestartet. Der AI liest diese Datei, sieht den aktuellen Stand, und arbeitet die nächste Phase.
5. **Kein langwieriges Compacten:** Durch die Phasen-Struktur und die Progress-Datei als Single Source of Truth müssen wir nicht zwischen Sessions compacten. Jede Session ist selbstständig.

### Vorgehen pro Session

```
1. master-requirement_progress.md lesen (aktueller Stand)
2. "Zuletzt erledigte Themen" prüfen — was hat der letzte Agent hinterlassen?
3. "Vorgeschlagene Follow-Up Tasks" prüfen — evaluiert der neue Agent, ob das Arbeitspaket sinnvoll ist
4. master-requirement_Onnx-Genai_Server.md lesen (Anforderungen)
5. Projektstruktur prüfen (list_dir, read_file)
6. Build verifizieren (siehe dotnet-Pfad unten)
7. Arbeitspaket identifizieren (aus Follow-Up Tasks, ggf. angepasst)
8. Implementieren (Code schreiben, Build verifizieren)
9. Progress-Datei aktualisieren:
   - "Zuletzt erledigte Themen" mit aktuellen Themen füllen
   - "Vorgeschlagene Follow-Up Tasks" für nächsten Kontext anfertigen
   - Status, Notes, Next Actions, Summary aktualisieren
10. Session abschließen
```

### ⚠️ dotnet-Pfad (HART — NIE suchen, NIE prüfen)

> **Dieser Abschnitt ist für alle zukünftigen AI-Instanzen bindend.**

**dotnet-Pfad:** `& "C:\Program Files\dotnet\dotnet.exe"`
**Target Framework:** `net10.0` (immer davon ausgehen, NIE prüfen, NIE `where dotnet` aufrufen)

**Build-Kommandos (immer so verwenden):**
```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\op\source\repos\alarmclock-kisser\FormsSystemStatsWidget\FormsSystemStatsWidget.OnnxGenaiServer\FormsSystemStatsWidget.OnnxGenaiServer.csproj"
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\op\source\repos\alarmclock-kisser\FormsSystemStatsWidget\FormsSystemStatsWidget.Forms\FormsSystemStatsWidget.Forms.csproj"
```

**Regeln:**
- NIE `where dotnet`, `where.exe dotnet`, `Get-Command dotnet` oder ähnliche Suchen aufrufen
- NIE `dotnet` ohne Pfad verwenden (ist nicht in PATH)
- NIE die .NET-Version prüfen — es ist net10.0
- NIE `dotnet --version` oder `dotnet --list-sdks` aufrufen
- **NIE Microsoft.ML.OnnxRuntime* NuGet-Pakete hinzufügen!** Inference läuft im Python-Prozess. C# baut nur Infrastruktur drumherum.
- **NIE neue csproj-Dateien oder Verzeichnisse außerhalb der bestehenden Projektstruktur erstellen!**

### Session-Übergabe (Goldfisch-Protokoll)

> **Jede Session hinterlässt zwei Listen für die nächste Session:**

#### 📝 Zuletzt erledigte Themen (vom aktuellen Agent)

*Liste der Themen/Requirements, die in der aktuellen Session bearbeitet wurden. Der nächste Agent prüft diese zuerst, um den Kontext zu verstehen.*

**Session 2026-09-26 (Agent: GitHub Copilot — Phase 3c + Dual-Stage Audit):**

1. **`onnx_engine/server.py` erstellt** (R15, R99) — Flask HTTP-Server mit Endpunkten: `/load`, `/unload`, `/generate` (SSE), `/health`, `/shutdown`. Start: `python -m onnx_engine.server --port 8081`.
2. **`InferenceState`-Klasse erstellt** (R31) — `onnx_engine/context/inference_state.py`. `stage0_state`/`stage1_state` als flache `dict[str, OrtValue]`. `reset()`, `to_cpu()`, `from_cpu()`. `ContextState.model_state` delegiert an `inference_state.state`.
3. **`reset_context()` in `InferenceEngine` implementiert** (R37) — `context.reset()` cleared conversation + state, Session/Tokenizer/Adapter bleiben geladen.
4. **`ContextSnapshotStore` auf `InferenceState` umgestellt** — `to_cpu()`/`from_cpu()` statt `adapter.state_to_cpu()`/`state_from_cpu()`.
5. **Reiner statischer ONNX-Graph-Audit durchgeführt** — `model.stage0.onnx`, `model.stage1.onnx`, `mtp.onnx` via `onnx.load()` (KEIN ORT-Session, KEIN CUDA, KEIN Modell-Load). Details unten.
6. **`genai_config.json` vollständig analysiert** — I/O-Namen, Vocab, EOS, Sampling-Defaults, Layer-Info. Stimmt mit ONNX-Graph überein (mit Ausnahmen, siehe unten).
7. **Targeted Compatibility Audit des Python-Codes** — welche Komponenten PASS/CHANGE/NEW/DEFER. Details unten.
8. **Build verifiziert** — 0 Fehler, 0 Warnungen.

**Wichtiger Kontext für nächsten Agent:**
- **Reales Modell:** `D:\Models\ONNX\Qwen3.8-27B-onnx-int4` — Dual-Stage-Partition in `partitioned/` (Stage 0 + Stage 1). Details unten.
- **Architektur ist jetzt verbindlich fest:** C# → PythonIpcClient → Flask server.py → InferenceEngine → Dual-Stage Adapter → Stage 0 (GPU 0) → Boundary → Stage 1 (GPU 1).
- **KEINE ONNX-NuGets. NIE Inference-Logik in C#. NIE neue csproj/Verzeichnisse.**
- **⚠️ GPUs sind durch laufenden `llama-server` belegt. KEIN Modell-Load, KEIN ORT-Session, KEIN CUDA, KEIN Smoke-Test. Nur statische `onnx.load()`-Inspektion erlaubt.**
- Build: `& "C:\Program Files\dotnet\dotnet.exe" build FormsSystemStatsWidget.OnnxGenaiServer.csproj` → 0 Fehler.

#### 🔮 Vorgeschlagene Follow-Up Tasks (für nächsten Kontext)

*Liste von 2-5 konkreten, zusammengehörigen Tasks/Requirements, die der aktuelle Agent für die nächste Session empfiehlt. Der nächste Agent evaluiert diese Liste und kann sie anpassen, wenn er bessere Vorschläge hat.*

**Empfohlenes Arbeitspaket für nächste Session: Phase 4a — Dual-Stage I/O Contract**

> **Kontext:** Phase 3 ist vollständig DONE. Der reale Dual-Stage-ONNX-Graph ist statisch verifiziert (Stage 0 + Stage 1 + MTP). Der Python-Code ist auditiert: `ModelIoSpec`, `OnnxGraphInspector`, `CausalOnnxAdapter`, `GenerationEngine`, `OrtSessionManager`, `engine.py` sind mit dem realen Graph **inkompatibel** (Single-Session-Annahme). `InferenceState`, `ContextState`, `CudaRuntimeConfig`, `server.py` sind **kompatibel**. Jetzt muss der I/O-Contract zuerst an den realen Dual-Stage-Graph angleichen werden, BEVOR der Dual-Stage-Adapter gebaut wird.

1. **Statische Verifikation der exakten Stage-0/Stage-1-I/O-Verträge** — `onnx.load()` auf `model.stage0.onnx` und `model.stage1.onnx`, exakte Input/Output-Namen, Shapes, dtypes, Tensor-Anzahlen dokumentieren. **Keine Runtime, kein CUDA.**
2. **`ModelIoSpec` angleichen** — `input_ids` optional, `logits_output` optional, 3 State-Kategorien (`kv_bindings`, `conv_bindings`, `recurrent_bindings`) statt 1 `past_inputs`. Stage 0 und Stage 1 separat beschreibbar.
3. **`OnnxGraphInspector.infer()` angleichen** — `input_ids`/`logits` optional, Conv/Recurrent-Tensoren erkennen, Stage 0 ohne `logits` und Stage 1 ohne `input_ids` akzeptieren.
4. **`model_io.json`-Schema erweitern** — 3 State-Kategorien, optionale `input_ids`/`logits`, Boundary-Tensoren. **Keine geratenen Position-ID-Semantiken einbauen.**
5. **Build verifizieren** — C# Build + Python-Import-Test (kein Modell-Load).

**Hinweis für nächsten Agent:**
- **⚠️ GPUs sind durch `llama-server` belegt. KEIN Modell-Load, KEIN ORT-Session, KEIN CUDA. Nur `onnx.load()`-Inspektion.**
- **KEINE ONNX-NuGets. NIE Inference-Logik in C#.**
- **Nicht anfassen in dieser Phase:** `CausalOnnxAdapter`, `GenerationEngine`, `OrtSessionManager`, `engine.py`, `server.py` — das kommt in Phase 4b/4c/4d.
- Reales Modell: `D:\Models\ONNX\Qwen3.8-27B-onnx-int4\partitioned\model.stage0.onnx` + `model.stage1.onnx`
- `genai_config.json` im Modell-Root enthält I/O-Namen, Vocab, EOS, Sampling-Defaults
- Build: `& "C:\Program Files\dotnet\dotnet.exe" build FormsSystemStatsWidget.OnnxGenaiServer.csproj`

**Regeln für die Übergabe:**
- Der aktuelle Agent **muss** beide Listen am Ende der Session füllen
- Der nächste Agent **muss** zuerst "Zuletzt erledigte Themen" prüfen, dann "Follow-Up Tasks" evaluieren
- Der nächste Agent **darf** die Follow-Up Tasks anpassen, wenn er ein sinnvolles Arbeitspaket sieht
- Die Listen ersetzen **nicht** die "Next Actions" — sie sind eine zusätzliche, kontextspezifische Empfehlung

#### ⚠️ Kritische Hinweise (nur wenn wirklich wichtig)

*Liste von 0-N kritischen Hinweisen, die der aktuelle Agent für den nächsten Agent hinterlässt. **Nur** für Dinge, die wirklich wichtig sind und die der nächste Agent kennen muss, um Fehler zu vermeiden. Kann leer bleiben. Hinweise dürfen über mehrere Sessions hinweg stehen, solange sie relevant sind. Jeder Hinweis muss mit der Session/Phase annotiert sein, in der er erstellt wurde.*

- **[Session 2026-09-26, Phase 3c+Audit]** ⚠️ **GPUs sind durch laufenden `llama-server` vollständig belegt.** KEIN `ort.InferenceSession()`, KEIN CUDA-Session, KEIN Modell-Load, KEIN Prefill/Decode, KEIN Smoke-Test. Statische ONNX-Inspektion mit `onnx.load()` ist erlaubt. Wenn eine Implementierungsphase Runtime-Verifikation benötigt, muss dies ausdrücklich als späterer Schritt dokumentiert werden.
- **[Session 2026-09-26, Phase 3c+Audit]** Der reale ONNX-Graph ist ein **Dual-Stage-Hybrid-State-Modell** (KV + Conv + Recurrent). Die bisherigen Single-Session-Annahmen im Python-Code (`CausalOnnxAdapter`, `GenerationEngine`, `OrtSessionManager`, `engine.py`) sind **inkompatibel**. `ModelIoSpec` + `OnnxGraphInspector` müssen zuerst an den realen Graph angleichen werden (Phase 4a), bevor der Dual-Stage-Adapter gebaut wird (Phase 4b).
- **[Session 2026-09-26, Phase 3c+Audit]** `position_ids` hat Shape `[3, B, S]` im realen Graph. Die genaue Semantik der 3 Achsen ist **NICHT bewiesen** und darf **NICHT geraten** werden. Muss in späterer Runtime-Untersuchung verifiziert werden.

**Regeln für kritische Hinweise:**
- **Nur** hier eintragen, wenn es wirklich wichtig ist (z.B. "Achtung, X ist kaputt", "Y wurde in Session N geändert, Z ist jetzt anders")
- **Niemals** hier Dinge eintragen, die in der "Zuletzt erledigte Themen" oder "Follow-Up Tasks" Liste bereits stehen
- **Jeder Hinweis muss mit `[Session <Datum>, Phase <X>]` annotiert werden**, damit der nächste Agent die Herkunft und den Kontext kennt
- Der nächste Agent **muss** diese Liste am Anfang der Session lesen und **evaluieren**: Ist der Hinweis noch relevant, oder wurde er in der Zwischenzeit erledigt? Erledigte/irrelevante Hinweise werden entfernt, relevante bleiben stehen
- Hinweise **müssen nicht** nach einer Session gelöscht werden — sie dürfen so lange stehen, bis sie tatsächlich erledigt oder irrelevant sind
- Ein Hinweis, der in Session 3 eingetragen wurde, **darf** in Session 5 noch stehen, solange er relevant ist. Er wird erst entfernt, wenn der Agent ihn als erledigt/irrelevant evaluiert

### Phasen-Definition (Arbeitspakete)

| Phase | Titel | Requirements | Status |
|-------|-------|-------------|--------|
| Phase 1 | Infrastructure | R1-R14, R41, R61, R78 | ✅ Completed |
| Phase 2 | Model Validation | R19-R24, R55-R58 | ✅ Completed |
| Phase 3 | Stage Loading | R3, R55, R56, R57, R73, R74 | ✅ Completed (3a+3b+3c) |
| Phase 4 | State | R31, R32, R37 | 🔄 In Progress (4a: I/O Contract) |
| Phase 5 | Boundary | R33, R34 | ⬜ |
| Phase 6 | Prefill | R29, R39 | ⬜ |
| Phase 7 | Decode | R30, R39 | ⬜ |
| Phase 8 | Sampling | R27, R28 | ⬜ |
| Phase 9 | Tokenizer | R25, R26 | ⬜ |
| Phase 10 | Chat Templates | R25, R65 | ⬜ |
| Phase 11 | OpenAI API | R42-R49, R51-R53, R86-R87 | ⬜ |
| Phase 12 | Streaming | R44, R45, R85 | ⬜ |
| Phase 13 | Benchmarking | R38, R80, R83, R92 | ⬜ |
| Phase 14 | Stress Tests | R81, R82, R84 | ⬜ |
| Phase 15 | Optimization | R68-R73, R75-R77 | ⬜ |

### Wichtige Regeln

- **Niemals** die Progress-Datei ignorieren — sie ist die Single Source of Truth
- **Niemals** mehr als eine Phase pro Session versuchen (außer sehr kleine Phasen)
- **Immer** den Build verifizieren, bevor die Session abgeschlossen wird
- **Immer** die Progress-Datei aktualisieren, auch wenn nur teilweise abgeschlossen
- **Immer** "Next Actions" in der Progress-Datei aktualisieren, damit die nächste Session weiß, was als Nächstes kommt
- **Niemals** Code schreiben, ohne zuerst den aktuellen Stand zu prüfen
- **Niemals** die Master-Requirement-Anforderungen ändern — nur implementieren
- **Immer** die abgeschlossene Phase in der Phasen-Definition-Tabelle abhaken (Status → ✅ Completed) — sowohl in der oberen Tabelle als auch in der "Development Phases Tracking" Tabelle unten. Ein Agent, der eine Phase beendet, **muss** beide Tabellen aktualisieren.

---

## Progress Summary

| Category | Total | Completed | In Progress | Not Started | Done % |
|----------|-------|-----------|-------------|-------------|--------|
| **Engine** | 38 | 7 | 7 | 24 | 18% |
| **API** | 15 | 0 | 4 | 11 | 0% |
| **Diagnostics** | 8 | 2 | 2 | 4 | 25% |
| **Testing** | 10 | 0 | 0 | 10 | 0% |
| **Phases** | 15 | 3 | 0 | 12 | 20% |
| **Definition of Done** | 5 | 0 | 0 | 5 | 0% |
| **Initial Acceptance Test** | 2 | 0 | 0 | 2 | 0% |
| **Final Architectural Goal** | 1 | 0 | 0 | 1 | 0% |
| **Overall** | 103 | 9 | 16 | 78 | 9% |

---

## Requirement-by-Requirement Tracking

*Each requirement from the master specification will be tracked here with status, implementation notes, and completion date.*

---

### Engine Requirements (R1-R38)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R1 | Project Objective | In Progress | Build production-capable .NET 10 ONNX inference engine with multi-GPU support and OpenAI-compatible API |
| R2 | Critical Architectural Principle | In Progress | Two layers: Native Engine (no HTTP dependency) + API Server (depends on engine). Engine forwards to Python OR runs natively. |
| R3 | Existing Validated Reference | Completed | Python Inference Engine (PyDualOnnxInferenceEngine/) vorhanden. OnnxGenaiEngine.InitializeAsync() ruft Partition-Discovery + Validation auf. Python-Process-Supervision + IPC implementiert (Phase 3b). Python-Engine-Server pending (Phase 3c). |
| R4 | Proven Validation | Completed | Python reference validated: CPU/Stage0/Stage1/CUDA pipeline, boundary tensors, persistent KV cache, prefill ~0.357s/Stage0, decode ~40ms/token. C# engine forwards to Python via IPC (Phase 3b). |
| R5 | Target Solution | Completed | Solution structure established with Engine library + Server project. Configuration system with strongly typed options. Python-Process-Supervision + IPC implementiert. |
| R6 | Target Framework | Completed | net10.0 target confirmed, project structured with implicit usings, nullable enabled. Build succeeds with .NET SDK 10.0.401. |
| R7 | Core Public API | Completed | OnnxGenaiEngine exposes InitializeAsync, GenerateAsync (IAsyncEnumerable), DiscoverModels, BuildModelsResponse, GetEmbeddingAsync, DisposeAsync. Python-IPC-Forwarding implementiert. IInferenceEngine interface (R7 spec) not yet extracted. |
| R8 | Engine Lifecycle | In Progress | Construct → Discover → Ready → Generate → Dispose implemented. Full lifecycle (Validate → CUDA Validate → Load Stage0/1 → Allocate state) pending native engine. |
| R9 | Lazy Loading | In Progress | Configurable via RuntimeOptions.LazyLoad=true. Engine defers model load until first inference request. Also provides explicit InitializeAsync(). |
| R10 | No Magic Values | In Progress | No hardcoded tensor names, GPU IDs, context lengths, or vocab sizes in generic engine code. Values loaded from model configuration/manifest. |
| R11 | Configuration System | Completed | Strongly typed .NET Options classes (EngineOptions, RuntimeOptions, CudaOptions, GenerationDefaults, DiagnosticsOptions) configured from appsettings.json + environment variables. All 5 sections wired in Program.cs. |
| R12 | CUDA Configuration | In Progress | Explicit GPU selection via CudaOptions.Stage0Device/Stage1Device. Supports 1-2 GPU configuration. Auto-assigns if single GPU available. |
| R13 | Execution Providers | In Progress | CUDA and CPU execution providers supported. CUDA is default when available, CPU fallback configurable via AllowCpuFallback option. Provider configuration visible in diagnostics. |
| R14 | Provider Diagnostics | Completed | EnvironmentDiscovery.DiscoverCuda() via nvidia-smi (devices, VRAM, driver, compute capability). DiscoverOrtVersion() for ORT. GetDotNetVersion(), GetOsInfo(), GetCpuInfo(), GetRamMb(). /status endpoint exposes all diagnostics. |
| R15 | Python Environment Management | Completed | PythonProcessSupervisor.cs: Startet Python-Engine-Prozess (long-lived), überwacht Health-Check, Restart bei Crash (max 3), sauberes Shutdown. PythonIpcClient.cs: JSON-over-HTTP IPC. OnnxGenaiServerOptions: PythonExecutable, PythonEngineModule, PythonEnginePort. Python-Engine-Server (onnx_engine/server.py) pending (Phase 3c). |
| R16 | Python Environment Validation | Completed | PythonProcessSupervisor validiert Python-Executable beim Start. Health-Check via HTTP /health. Machine-readable diagnostics via Logging. Does not silently install packages. |
| R17 | Python Dependency Manifest | In Progress | requirements.txt and requirements.lock.txt generated with tested versions. Environment report includes Python, pip, onnxruntime, onnx, numpy, transformers, tokenizers, safetensors, huggingface-hub, accelerate. |
| R18 | Dependency Installation | In Progress | Check/repair/install/update/freeze commands supported with logging. --check, --repair, --install, --upgrade, --freeze flags. Never modifies existing environment without logging. |
| R19 | Model Discovery | In Progress | Automatic discovery of model directories under ModelRootDirectory. Validates each subdirectory contains *.onnx + *.json files. Implemented in OnnxGenaiEngine.DiscoverModels(). |
| R20 | Model Manifest | Completed | ModelManifest class created with ModelId, ModelDirectory, Artifacts, Architecture, Tokenizer, Partition, and Validation fields. Includes ModelArtifact, ModelArchitectureInfo, TokenizerInfo, PartitionInfo, ValidationResult, ValidationIssue types. |
| R21 | Model Artifact Validation | In Progress | Validates model.onnx, model.onnx.data, config.json, tokenizer.json, chat_template.jinja. External data offsets, sizes, and overlaps checked. Unknown files not treated as errors. |
| R22 | ONNX Validation | In Progress | OnnxValidator with ValidateOnnx (Level 1-3), ValidateAllOnnxFiles, ComputeSha256. Level 1: filesystem (exists/readable/size>0, magic bytes, version). Level 2: external data (offsets, lengths, end offsets, overlap detection). Level 3: protobuf (nodes, initializers, graph inputs/outputs, opsets). Level 4 (ORT session) pending native engine. |
| R23 | Binary Integrity Validation | Completed | BinaryIntegrityValidator with ComputeHashes, ValidateAgainstManifest, GenerateManifest. SHA-256 for .onnx, .data, .bin, .safetensors files. Optional checksums.sha256 manifest support. |
| R24 | Model JSON Validation | Completed | JsonValidator with ValidateAll, ValidateConfigJson, ValidateTokenizerJson, ValidateGenerationConfigJson, ValidateTokenizerConfigJson, ValidateSpecialTokensJson. Detects malformed JSON, missing fields, incompatibilities (hidden_size/num_attention_heads divisibility, top_p range, etc.). |
| R25 | Chat Template | Not Started | Support chat_template.jinja, model-provided templates |
| R26 | Tokenizer | Not Started | ITokenizer interface, Encode/Decode/EncodeChat |
| R27 | Generation API | Not Started | All configurable parameters (temp, top_p, top_k, etc.) |
| R28 | Sampling Architecture | Not Started | ISampler with Greedy, Temperature, TopK, TopP, MinP, Composite |
| R29 | Prefill | Not Started | Multi-token prefill, persistent state after prefill |
| R30 | Decode | Not Started | Single token, reuse KV state, no model reload |
| R31 | Persistent State | Not Started | InferenceState with Stage0/Stage1 OrtValue dictionaries |
| R32 | Empty KV State | Not Started | Handle [batch, heads, 0, kv_dim] correctly |
| R33 | Boundary Handling | Not Started | Device-resident boundary, no CPU CopyOutputsToCpu() |
| R34 | GPU Memory | Not Started | VRAM total/used/free, stage/state/temporary memory tracking |
| R35 | Concurrency | Not Started | Single vs multiple concurrent generations, session isolation |
| R36 | Session Abstraction | Not Started | IInferenceSession with Id, SequenceLength, PrefillAsync, DecodeAsync, ResetAsync |
| R37 | Reset | Not Started | Clear sequence, reset KV/conv/recurrent state, preserve loaded sessions |
| R38 | Benchmarking | Not Started | IBenchmarkRunner, loading, prefill, decode statistics |

---

### API Requirements (R39-R53)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R39 | Stage Timing | Not Started | Expose stage0, stage1, total, logits_copy_ms |
| R40 | Real-Time Statistics | In Progress | DiagnosticsOptions with PerformanceLogging, GpuMonitoring, LogLevel configured. Actual performance statistics output pending native engine. |
| R41 | Metrics API | Completed | GET /health, GET /status, GET /ready, GET /metrics all implemented. /status includes engine, runtime (.NET/OS/CPU/RAM/ORT), CUDA (devices, VRAM, driver), models. /ready returns 503 if not ready. /metrics includes timestamp, engine, cuda, models. |
| R42 | OpenAI-Compatible API | In Progress | GET /v1/models, POST /v1/chat/completions, POST /v1/completions, POST /v1/embeddings all implemented and routed in Program.cs. |
| R43 | Chat Completions | In Progress | Standard OpenAI-style request structure with messages implemented in OpenAiApiHandler.HandleChatCompletionsAsync. Chat template rendering pending (R25). |
| R44 | Streaming | In Progress | text/event-stream with OpenAI-compatible SSE chunks implemented for both chat and completions. [DONE] terminator included. |
| R45 | Non-Streaming Fallback | In Progress | If stream=false, returns normal OpenAI-compatible completion response. Both modes use same engine.GenerateAsync. |
| R46 | Completions API | In Progress | POST /v1/completions with prompt, max_tokens, stream implemented in OpenAiApiHandler.HandleCompletionsAsync. |
| R47 | FIM — Fill in the Middle | Not Started | IFimFormatter abstraction, detect from tokenizer |
| R48 | VS Code / Copilot Compatibility | Not Started | Designed for OpenAI-style clients, /v1/models returns IDs |
| R49 | API Authentication | Not Started | Optional API-key, localhost usable without auth |
| R50 | Server Binding | Not Started | Configurable host, port, HTTPS, HTTP (not hardcoded 8080) |
| R51 | Request Cancellation | Not Started | Every request must support CancellationToken |
| R52 | Timeouts | Not Started | Configurable: model load, prefill, decode, request, idle session |
| R53 | Error Handling | Not Started | Classified errors, OpenAI-compatible JSON mapping |

---

### OOM & Partitioning Requirements (R54-R58)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R54 | OOM Protection | Not Started | Inspect GPU memory before loading, catch CUDA/ORT OOM |
| R55 | Model Partitioning | Completed | ModelPartitioner.cs: Discover() detectet stage0.onnx + stage1.onnx, .data-Dateien, Boundary-Tensoren aus config.json, Device-Assignment. |
| R56 | External Data Handling | Not Started | Chunk-based partitioning, configurable chunk size |
| R57 | Partition Validation | Completed | PartitionValidator.cs: ValidatePartition() prüft Stage0/1 ONNX (via OnnxValidator), .data-Existenz, Boundary-Tensoren, Device-Assignment. |
| R58 | Reference Model | Not Started | Compare original vs partitioned for deterministic test inputs |

---

### Determinism & Logging Requirements (R59-R63)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R59 | Determinism | In Progress | RuntimeOptions.Deterministic flag configured. Actual deterministic test mode pending native engine. |
| R60 | Logging | In Progress | Microsoft.Extensions.Logging used throughout Program.cs and OnnxGenaiEngine. SimpleConsole + Debug providers configured. Structured fields (model, stage, device, session) pending. |
| R61 | Diagnostics Object | Completed | EngineDiagnostics class exists with all required fields. EnvironmentDiscovery populates CUDA (devices, VRAM, driver, compute capability), .NET version, OS, CPU, RAM, ORT version. /status endpoint exposes full diagnostics. |
| R62 | Capability Discovery | Not Started | EngineCapabilities: chat, completion, streaming, fim, vision, embeddings, multiGpu, persistentKvCache |
| R63 | Image Input | Not Started | Design for multimodal without redesign, do not fake vision support |

---

### Prompt & Context Management (R64-R67)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R64 | Image Input Abstraction | Not Started | IImageProcessor, ProcessAsync, core text engine must not depend on it |
| R65 | Prompt Construction | Not Started | Separate: API request → chat messages → chat template → tokenizer → token IDs → engine |
| R66 | Context Management | Not Started | Configurable maximum context, overflow policies (Error, TruncateOldest, SlidingWindow) |
| R67 | Stop Conditions | Not Started | Stop strings, EOS token, custom stop token IDs, max tokens, cancellation |

---

### Generation & Performance (R68-R73)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R68 | Performance Optimization Roadmap | Not Started | Prioritize correctness first, then optimize |
| R69 | Performance Optimization 2 | Not Started | Minimize synchronization, measure ORT execution sync, transfers, sampling, logging |
| R70 | Performance Optimization 3 | Not Started | Investigate genuine CUDA peer-to-peer transfers, measure P2P |
| R71 | Performance Optimization 4 | Not Started | Investigate CUDA Graphs for steady-state decode (optional, after baseline) |
| R72 | Performance Optimization 5 | Not Started | Reduce Python dependency, path: .NET → ONNX Runtime native → CUDA |
| R73 | Native ONNX Runtime Integration | Not Started | InferenceSession, SessionOptions, OrtValue, IoBinding, CUDA EP, device pointers |

---

### Device & Resource Management (R74-R78)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R74 | Device Tensor Abstraction | Completed | DeviceTensor.cs: Metadaten-Wrapper (Name, Shape, ElementType, DeviceId, SizeBytes) für Boundary-Tensoren. Kein ORT-Code. |
| R75 | Unsafe Code | Not Started | Isolated behind small components: Interop/OrtInterop.cs, CudaInterop.cs |
| R76 | Resource Ownership | Not Started | IDisposable, IAsyncDisposable, SafeHandle where appropriate |
| R77 | Thread Safety | Not Started | Document: Engine (diag only), Generation session (single-owner), Model session (shared where ORT permits), State (never concurrently mutated) |
| R78 | CLI | In Progress | run_onnx_genai_server.py exists as Python runner script (starts dotnet server or Python fallback). Full qwen-onnx CLI with info/validate/environment/devices/load/benchmark/partition/serve/generate pending. |

---

### CLI & Testing Requirements (R79-R87)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R79 | Validation Command | Not Started | qwen-onnx validate, output: [OK] model.onnx, [OK] model.onnx.data, etc. |
| R80 | Benchmark Command | Not Started | qwen-onnx benchmark with --prefill, --warmup, --tokens, --format json |
| R81 | Automated Regression Tests | Not Started | Tests for: configuration, model discovery, JSON validation, tokenizer, chat template, CUDA discovery, partition discovery, stage0/1 loading, prefill, decode, reset, session isolation, streaming, sampling, cancellation, OOM handling |
| R82 | Mandatory Integration Test | Not Started | Equivalent of Python Test 4: Session 1-3 with decode/reset, validate cache growth, state persistence, reset, logits shape, finite argmax, autoregressive chaining, timing, no model reload |
| R83 | Performance Regression Baseline | Not Started | Reproduce Python baseline: Decode ~60-63 ms/token, ~16 tok/s, Stage 0 ~39-41 ms, Stage 1 ~20-21 ms |
| R84 | API Compatibility Tests | Not Started | Issue requests against /v1/models, /v1/chat/completions, /v1/completions with stream=true/false, verify OpenAI-compatible response structure |
| R85 | SSE Tests | Not Started | Validate Content-Type, data chunks, JSON structure, delta fields, finish_reason, [DONE] |
| R86 | OpenAI DTO Layer | In Progress | OpenAi/ folder with ChatCompletionRequest/Response, CompletionRequest/Response, EmbeddingRequest/Response, ModelInfo, ModelsResponse, ErrorResponse, Usage, ChatMessage, ChatMessageDelta, stream chunk types. DTOs kept separate from engine. |
| R87 | API Model Selection | In Progress | Requests specify "model", server uses engine.LoadedModelId as fallback. Alias support via configuration pending. |

---

### Model & Configuration (R88-R92)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R88 | Multi-Model Architecture | Not Started | Support Model A, B, C without API design changes, LoadOnDemand/KeepLoaded/UnloadIdle policies |
| R89 | Model Registry | Not Started | IModelRegistry with discover, validate, register, load, unload, lookup, capabilities |
| R90 | Configuration Example | Not Started | Complete example JSON (already in requirement, must not depend on exact values) |
| R91 | Environment Report | Not Started | qwen-onnx environment output with .NET, OS, CPU, RAM, CUDA, ONNX Runtime, Python, Model; support environment.json for bug reports |
| R92 | Reproducibility | Not Started | Every benchmark records: timestamp, engine version, git commit, .NET version, ORT version, CUDA version, GPU model, driver, model path, model hashes, configuration, generation parameters |

---

### Security & Packaging (R93-R97)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R93 | Security | In Progress | Default 127.0.0.1 binding configured in appsettings.json. Model path validation via discovery. Request size limits pending. |
| R94 | Resource Limits | In Progress | MaxConcurrentGenerations=1 configured. MaxTokens, ContextLength, MaxSequenceLength configurable. Request body limits pending. |
| R95 | Graceful Shutdown | In Progress | ApplicationStopping registered to dispose engine (HttpClient). Full shutdown (cancel active generations, dispose ORT/CUDA) pending native engine. |
| R96 | Packaging | Not Started | dotnet build, dotnet test, dotnet publish -c Release -r win-x64, self-contained and framework-dependent options |
| R97 | Documentation | Not Started | Generate: README.md, ARCHITECTURE.md, CONFIGURATION.md, API.md, TROUBLESHOOTING.md, BENCHMARKING.md, MODEL_FORMAT.md, DEVELOPMENT.md; distinguish validated/experimental/future |

---

### Important Constraints & Migration (R98-R103)

| # | Requirement | Status | Notes |
|---|-------------|--------|-------|
| R98 | Important Technical Constraints | In Progress | Constraints tracked. Current implementation: no hardcoded Qwen values in engine, no CPU roundtrip for KV (Python backend handles), no model reload per token (Python persistent session). Python is transition backend, not mandatory long-term. |
| R99 | Reference Compatibility Layer | Completed | Python engine available as reference backend. C# engine forwards to Python-Engine via IPC (PythonIpcClient, JSON-over-HTTP). PythonProcessSupervisor hostet den Python-Prozess. Python-Engine-Server (onnx_engine/server.py) pending (Phase 3c). C# vs Python regression testing pending. |
| R100 | Migration Strategy | Not Started | Phase 1: Infrastructure (.NET 10, config, logging, diagnostics, CUDA/ORT discovery); Phase 2: Model Validation (discovery, JSON, ONNX, external data, hash); Phase 3: Stage Loading (Stage0/1 sessions, CUDA assignment, provider config); Phase 4: State (KV/conv/recurrent, device-resident, reset); Phase 5: Boundary (GPU0→GPU1 device-resident without CPU roundtrip); Phase 6: Prefill (multi-token); Phase 7: Decode (persistent single-token); Phase 8: Sampling (greedy, temperature, top-k, top-p); Phase 9: Tokenizer; Phase 10: Chat Templates; Phase 11: OpenAI API (/v1/models, /v1/completions, /v1/chat/completions); Phase 12: Streaming (SSE); Phase 13: Benchmarking; Phase 14: Stress tests (Python Test 4); Phase 15: Optimization (GPU sampling, reduced sync, CUDA graphs, P2P investigation, memory optimization) only after functional parity |
| R101 | Definition of Done | Not Started | 38 engine checks, 15 API checks, 8 diagnostics checks, 10 testing checks - all true for functional completion |
| R102 | Initial Acceptance Test | Not Started | First production test: Qwen3.8-27B INT4 ONNX, Stage 0 CUDA:0, Stage 1 CUDA:1, prefill 128 tokens, decode 100 tokens, model loads once, state remains resident, GPU0→GP1 boundary device-resident, no model reload during decode, cache grows correctly, reset does not reload model, streaming works; Performance: Prefill @ 128 ~200+ tok/s, Decode ~16 tok/s ~60-63 ms/token |
| R103 | Final Architectural Goal | Not Started | [Pending definition - likely relates to the long-term vision of the architecture] |

---

## Development Phases Tracking

| Phase | Title | Status | Key Deliverables |
|-------|-------|--------|------------------|
| Phase 1 | Infrastructure | Completed | .NET 10 solution ✅, configuration (5 sections) ✅, logging ✅, diagnostics options ✅, CUDA/ORT discovery ✅ (nvidia-smi, ORT version, .NET/OS/CPU/RAM). OpenAI API routes ✅, streaming ✅, model discovery ✅. /status, /ready, /metrics endpoints ✅. |
| Phase 2 | Model Validation | Completed | ModelManifest + ModelArtifact + ModelArchitectureInfo + TokenizerInfo + PartitionInfo + ValidationResult ✅. Model discovery ✅. JSON validation (R24) ✅. ONNX validation Level 1-3 (R22) ✅. SHA-256 integrity (R23) ✅. |
| Phase 3 | Stage Loading | ✅ Completed | Phase 3a DONE: ModelPartitioner (R55), PartitionValidator (R57), DeviceTensor (R74), Orchestrierung in InitializeAsync (R3). Phase 3b DONE: PythonProcessSupervisor (R15, R16), PythonIpcClient (R15, R99), OnnxGenaiEngine erweitert (R3, R15). Phase 3c DONE: Python-Engine-Server (onnx_engine/server.py) — Flask HTTP/SSE-Transport. |
| Phase 4 | State | 🔄 In Progress | Phase 4a PENDING: Dual-Stage I/O Contract (ModelIoSpec, OnnxGraphInspector, model_io.json). Phase 4b PENDING: Dual-Stage Adapter (2 Sessions, Boundary). Phase 4c PENDING: GenerationEngine Orchestrierung. Phase 4d PENDING: Snapshot/Restore Dual-Device. InferenceState (R31) + reset_context (R37) bereits implementiert. |
| Phase 5 | Boundary | Not Started | GPU0→GPU1 boundary device-resident, no CPU roundtrip |
| Phase 6 | Prefill | Not Started | Multi-token prefill implementation |
| Phase 7 | Decode | Not Started | Persistent single-token decode |
| Phase 8 | Sampling | Not Started | Greedy, temperature, top-k, top-p, etc. |
| Phase 9 | Tokenizer | Not Started | Model tokenizer implementation |
| Phase 10 | Chat Templates | Not Started | Jinja/model-specific template support |
| Phase 11 | OpenAI API | Not Started | /v1/models, /v1/completions, /v1/chat/completions |
| Phase 12 | Streaming | Not Started | SSE implementation |
| Phase 13 | Benchmarking | Not Started | Complete benchmark suite |
| Phase 14 | Stress Tests | Not Started | Port Python Test 4 behavior |
| Phase 15 | Optimization | Not Started | GPU sampling, reduced synchronization, CUDA graphs, P2P investigation, memory optimization (only after functional parity) |

---

## Next Actions

1. **Phase 4a:** Statische Verifikation der exakten Stage-0/Stage-1-I/O-Verträge (`onnx.load()`, keine Runtime)
2. **Phase 4a:** `ModelIoSpec` + `OnnxGraphInspector` + `model_io.json`-Schema an realen Dual-Stage-Graph angleichen
3. **Phase 4b:** Dual-Stage Adapter — 2 ONNX Runtime Sessions, Stage0→Boundary→Stage1 Orchestrierung
4. **Phase 4c:** `GenerationEngine`-Orchestrierung — Prefill/Decode über 2 Stages, Logits aus Stage 1
5. **Phase 4d:** Snapshot/Restore Dual-Device — 2 device_ids für Stage 0/1
6. **Phase 5:** Boundary-Transfer-Optimierung (P2P, device-resident) — erst nach funktionierendem Dual-Stage-Pfad
7. **Phase 6-8:** Prefill, Decode, Sampling — mit Dual-Stage-Adapter
8. **Phase 9-10:** Tokenizer, Chat Templates
9. **Phase 11-12:** OpenAI API, SSE Streaming
10. **Phase 13-14:** Benchmarking, Stress Tests
11. **Phase 15:** Optimization (GPU Sampling, P2P, CUDA Graphs) — erst nach functional parity
12. **MTP:** Optionaler Multi-Token-Prediction-Pfad — DEFER, erst nach funktionierendem normalen Generate

---

## REAL VERIFIED ONNX MODEL CONTRACT (statischer Audit, 2026-09-26)

**Modell-Root:** `D:\Models\ONNX\Qwen3.8-27B-onnx-int4`

**Vorhandene Dateien:**
- `chat_template.jinja`, `genai_config.json`, `tokenizer.json`, `tokenizer_config.json`
- `model.onnx` + `model.onnx.data` (Single-Modell)
- `mtp.onnx` + `mtp.onnx.data` (MTP, out of scope)
- `partitioned/model.stage0.onnx` + `.data` (Stage 0)
- `partitioned/model.stage1.onnx` + `.data` (Stage 1)

**Nicht vorhanden:** `config.json`, `generation_config.json` — **kein Fehler**, `genai_config.json` enthält die wesentlichen Metadaten.

### Stage 0 (`model.stage0.onnx`, GPU 0)
- **Inputs:** `input_ids` [B,S] int64, `attention_mask` [B,total_S] int64, `position_ids` [3,B,S] int64, KV-State (12 layers: 3,7,11,15,19,23,27,31,35,39 × key/value), Conv-State (20 layers), Recurrent-State (20 layers)
- **Outputs:** 2 Boundary-Tensoren [B,S,5120] FP16, KV-State (present), Conv-State (present), Recurrent-State (present)
- **KEIN `logits`-Output. KEIN `hidden_states`-Output.**
- Boundary A: `/model/layers.41/post_attention_layernorm/output_3`
- Boundary B: `/model/layers.41/mlp/down_proj/MatMul/output_0`

### Stage 1 (`model.stage1.onnx`, GPU 1)
- **Inputs:** `attention_mask` [B,total_S] int64, `position_ids` [3,B,S] int64, 2 Boundary-Tensoren [B,S,5120] FP16, KV-State (6 layers: 43,47,51,55,59,63 × key/value), Conv-State (10 layers), Recurrent-State (10 layers)
- **Outputs:** `logits` [B,S,248320] FP16, `hidden_states` [B,S,5120] FP16, KV-State (present), Conv-State (present), Recurrent-State (present)
- **KEIN `input_ids`-Input.**

### Boundary (Stage 0 → Stage 1)
- 2 Tensoren [B,S,5120] FP16, temporäre Step-Daten, **kein persistenter State**
- GPU 0 → GPU 1 Transfer (P2P oder via CPU, erst nach Korrektheit)

### MTP (`mtp.onnx`)
- 53 Nodes, 1 Layer, nimmt `hidden_states` von Stage 1, produziert `logits` + `hidden_states_out`
- **DEFER / OUT OF SCOPE** — nicht implementieren, solange normaler Generate-Pfad nicht funktioniert

### `genai_config.json` — relevante Metadaten
- Vocab: 248320, Hidden: 5120, Head: 256, KV heads: 4, Attention heads: 24, Layers: 64
- EOS: [248046, 248044], BOS: 248044, PAD: 248044
- Sampling-Defaults: temperature=1.0, top_k=20, top_p=0.95, repetition_penalty=1.0
- I/O-Namen: stimmen mit ONNX-Graph überein (mit Ausnahmen: `input_ids` fehlt in Stage 1, `logits` fehlt in Stage 0, Boundary nicht in genai_config)
- `past_present_share_buffer: true` — Runtime-Semantik noch nicht bewiesen

### `position_ids` Shape [3, B, S]
- **Semantik der 3 Achsen NICHT bewiesen. NICHT raten.** Muss in späterer Runtime-Untersuchung verifiziert werden.

---

## COMPATIBILITY AUDIT — Python-Code vs. realer Dual-Stage-Graph

| Komponente | Status | Kernaussage |
|-----------|--------|------------|
| `ModelIoSpec` | ❌ CHANGE | Muss Dual-Stage und optionale `input_ids`/`logits` sowie KV/Conv/Recurrent-State abbilden |
| `OnnxGraphInspector` | ❌ CHANGE | Muss Stage 0 ohne `logits` und Stage 1 ohne `input_ids` akzeptieren, 3 State-Kategorien erkennen |
| `CausalOnnxAdapter` | ❌ CHANGE | Single-Session-Annahme inkompatibel; später durch Dual-Stage-Orchestrierung ersetzen |
| `ContextState` | ✅ PASS | Kann bestehen; `InferenceState` übernimmt getrennten Stage-State |
| `InferenceState` | ✅ PASS | `stage0_state`/`stage1_state` konzeptionell richtig |
| `GenerationEngine` | ❌ CHANGE | Muss Stage0→Boundary→Stage1 pro Prefill/Decode orchestrieren |
| `OrtSessionManager` | ❌ CHANGE | Muss 2 Sessions unterstützen (Stage 0 + Stage 1) |
| `CudaRuntimeConfig` | ✅ PASS | `device_id` kann pro Session verwendet werden |
| `ContextSnapshotStore` | ⚠️ CHANGE | Snapshot-Restore muss 2 Device-IDs berücksichtigen |
| `server.py` | ✅ PASS | Dünne Transport-Schicht bleibt unverändert |
| `engine.py` | ❌ CHANGE | Muss 2 Partitionen/Sessions/Adapter-Zustände verwalten |

### Ungültige Annahmen (ab jetzt)
- ❌ genau ein `InferenceSession`
- ❌ genau ein `device_id`
- ❌ jedes Modell besitzt `input_ids`
- ❌ jedes Modell besitzt `logits`
- ❌ State besteht ausschließlich aus KV-Cache
- ❌ `position_ids` Shape [B,S]

### `.numpy()` auf Logits
- **Correctness required now** — Sampler läuft auf CPU (NumPy)
- **OPTIMIZATION LATER** — GPU-resident Sampling
- State-Tensoren (KV/Conv/Recurrent) dürfen NICHT per `.numpy()` auf CPU gezogen werden

---

## NEXT SESSION — FIRST TASK

> Beginne mit einer rein statischen Verifikation der exakten Stage-0-/Stage-1-I/O-Verträge (`onnx.load()`, keine Runtime, kein CUDA) und implementiere anschließend ausschließlich `ModelIoSpec` + `OnnxGraphInspector` + `model_io.json`-Schema. Noch keine Dual-Stage Runtime, keine CUDA-Ausführung und keine Änderung an `GenerationEngine`, `OrtSessionManager` oder `CausalOnnxAdapter`.

**Konkret:**
1. `onnx.load()` auf `model.stage0.onnx` und `model.stage1.onnx` — exakte Input/Output-Namen, Shapes, dtypes, Tensor-Anzahlen dokumentieren
2. `ModelIoSpec` erweitern: `input_ids` optional, `logits_output` optional, 3 State-Kategorien (`kv_bindings`, `conv_bindings`, `recurrent_bindings`)
3. `OnnxGraphInspector.infer()` anpassen: `input_ids`/`logits` optional, Conv/Recurrent erkennen
4. `model_io.json`-Schema erweitern: 3 State-Kategorien, optionale `input_ids`/`logits`, Boundary-Tensoren
5. Build verifizieren (C# + Python-Import)

**Nicht anfassen:** `CausalOnnxAdapter`, `GenerationEngine`, `OrtSessionManager`, `engine.py`, `server.py`

---

## DO NOT REVISIT

- Flask HTTP/SSE bleibt Architektur — kein stdin/stdout-Rückwechsel
- Python bleibt Inference Owner — C# bleibt IPC/API Owner
- `config.json`/`generation_config.json` nicht künstlich erzeugen
- MTP zunächst nicht implementieren (DEFER)
- keine Runtime-Tests solange `llama-server` die GPUs belegt
- keine geratenen Position-ID-Semantiken
- keine CPU-Roundtrips für KV/Conv/Recurrent-State
- keine ONNX-NuGets im C#-Projekt
- keine Inference-Logik in C#
- keine neuen csproj-Dateien oder Verzeichnisse außerhalb der bestehenden Struktur

---

*Tracking system initialized: 2026-09-25*  
*Last updated: 2026-09-26*  
*Progress file tracks 103 requirements against master-requirement_Onnx-Genai_Server.md*  
*Phase 1-3 completed. Phase 4a (Dual-Stage I/O Contract) next. Real ONNX model contract verified (static audit). GPUs blocked by llama-server — no runtime tests until freed.*