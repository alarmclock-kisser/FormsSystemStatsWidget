using System.Text;
using System.Text.Json.Nodes;
using FormsSystemStatsWidget.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FormsSystemStatsWidget.Core.Tests;

[TestClass]
public sealed class LlamaStreamTransformerTests
{
    private const string ModelName = "test-model";

    [TestMethod]
    public void LoopDetection_DiagnosesRepeatedMessagesWithoutInterjectingOrAborting()
    {
        var config = new LoopDetectionConfig
        {
            Enabled = true,
            TriggerAfter = 3,
            DetectionWindow = 5,
            SimilarityThreshold = 1.0,
            InterjectionEnabled = false,
            AbortEnabled = true
        };
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" }
        };

        LoopDetectionResult result = new LoopDetectionService(config).DetectLoop(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.IsNull(result.InterjectionMessage);
        Assert.IsFalse(result.ShouldAbort);
        Assert.AreEqual(3, messages.Count);
    }

    [TestMethod]
    public void LoopDetection_RepeatedToolCallAcrossResults_AddsInterjectionAfterHistory()
    {
        var config = new LoopDetectionConfig { TriggerAfter = 3, DetectionWindow = 5, SimilarityThreshold = 0.9 };
        JsonArray messages = CreateRepeatedToolCalls("lookup", "{\"query\":\"same\"}");

        LoopDetectionResult result = new LoopDetectionService(config).ApplyToConversation(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.IsNotNull(result.InterjectionMessage);
        Assert.AreEqual(1, result.InterjectionCount);
        Assert.AreEqual("tool-3", messages[4]?["tool_calls"]?[0]? ["id"]?.ToString());
        Assert.AreEqual("loop_detection_interjection", messages[5]?["name"]?.ToString());
    }

    [TestMethod]
    public void LoopDetection_DifferentArgumentsAndReadEditRead_DoNotTrigger()
    {
        var service = new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3, DetectionWindow = 5 });
        JsonArray differentArguments = new()
        {
            CreateToolMessage("1", "read", "{\"path\":\"one.cs\"}"),
            CreateToolMessage("2", "read", "{\"path\":\"two.cs\"}"),
            CreateToolMessage("3", "read", "{\"path\":\"three.cs\"}")
        };
        JsonArray readEditRead = new()
        {
            CreateToolMessage("1", "read", "{\"path\":\"same.cs\"}"),
            CreateToolMessage("2", "edit", "{\"path\":\"same.cs\"}"),
            CreateToolMessage("3", "read", "{\"path\":\"same.cs\"}")
        };

        Assert.IsFalse(service.DetectLoop(differentArguments).IsLoopDetected);
        Assert.IsFalse(service.DetectLoop(readEditRead).IsLoopDetected);
    }

    [TestMethod]
    public void LoopDetection_TwoMatchingCallsAreNotEnoughToTrigger()
    {
        JsonArray messages = new()
        {
            CreateToolMessage("tool-1", "lookup", "{\"query\":\"same\"}"),
            CreateToolMessage("tool-2", "lookup", "{\"query\":\"same\"}")
        };

        Assert.IsFalse(new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3 }).DetectLoop(messages).IsLoopDetected);
    }

    [TestMethod]
    public void LoopDetection_ReorderedParallelToolCallsAreTheSameRepeatedAction()
    {
        JsonArray messages = new()
        {
            CreateParallelToolMessage("lookup", "read"),
            CreateParallelToolMessage("read", "lookup")
        };

        LoopDetectionResult result = new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 2 }).DetectLoop(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.AreEqual(2, result.RepeatCount);
    }

    [TestMethod]
    public void LoopDetection_RepeatedAssistantOutputInterjectsAndCanContinueNormally()
    {
        var service = new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3 });
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "assistant", ["content"] = "I will repeat the same action." },
            new JsonObject { ["role"] = "assistant", ["content"] = "I will repeat the same action." },
            new JsonObject { ["role"] = "assistant", ["content"] = "I will repeat the same action." }
        };

        LoopDetectionResult result = service.ApplyToConversation(messages);
        messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = "I will do something different now." });

        Assert.IsNotNull(result.InterjectionMessage);
        Assert.IsFalse(service.DetectLoop(messages).IsLoopDetected);
    }

    [TestMethod]
    public void LoopDetection_AbortRequiresEnabledAndConfiguredInterjectionCount()
    {
        var config = new LoopDetectionConfig { TriggerAfter = 3, AbortEnabled = true, AbortAfterInterjections = 2 };
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "user", ["name"] = "loop_detection_interjection", ["content"] = "hint 1" },
            new JsonObject { ["role"] = "user", ["name"] = "loop_detection_interjection", ["content"] = "hint 2" }
        };

        Assert.IsTrue(new LoopDetectionService(config).DetectLoop(messages).ShouldAbort);
        config.AbortEnabled = false;
        Assert.IsFalse(new LoopDetectionService(config).DetectLoop(messages).ShouldAbort);
    }

    [TestMethod]
    public void LoopDetection_InterjectionCountsAreConversationScoped()
    {
        var service = new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3 });
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" }
        };

        Assert.AreEqual(1, service.ApplyToConversation(messages).InterjectionCount);
        Assert.AreEqual(1, service.DetectLoop(messages).InterjectionCount);
        Assert.AreEqual(0, service.DetectLoop(new JsonArray()).InterjectionCount);
    }

    [TestMethod]
    public void LoopDetection_InterjectionLimitCarriesAcrossGenerationsWithoutCrossingRequests()
    {
        var service = new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3, MaxInterjections = 2 });
        JsonArray firstGeneration = CreateRepeatedToolCalls("lookup", "{\"query\":\"same\"}");
        JsonArray independentConversation = CreateRepeatedToolCalls("lookup", "{\"query\":\"same\"}");

        Assert.AreEqual(1, service.ApplyToConversation(firstGeneration).InterjectionCount);
        Assert.AreEqual(1, service.ApplyToConversation(independentConversation).InterjectionCount);
    }

    [TestMethod]
    public void LoopDetection_NewUserTurnDoesNotReuseEarlierLoopHistory()
    {
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "user", ["content"] = "first task" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "assistant", ["content"] = "repeat" },
            new JsonObject { ["role"] = "user", ["content"] = "new task" }
        };

        Assert.IsFalse(new LoopDetectionService(new LoopDetectionConfig { TriggerAfter = 3 }).DetectLoop(messages).IsLoopDetected);
    }

    [TestMethod]
    public void LoopDetection_ExactRepeatedToolCallsTriggerInterjection()
    {
        var config = new LoopDetectionConfig { TriggerAfter = 3, DetectionWindow = 5, SimilarityThreshold = 0.95 };
        JsonArray messages = CreateRepeatedToolCalls("build", "{}");

        LoopDetectionResult result = new LoopDetectionService(config).ApplyToConversation(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.AreEqual(3, result.RepeatCount);
        Assert.IsNotNull(result.InterjectionMessage);
        Assert.AreEqual(1, result.InterjectionCount);
    }

    [TestMethod]
    public void LoopDetection_ToolCallInterjectionAlreadyPresentTriggersAbort()
    {
        var config = new LoopDetectionConfig
        {
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true,
            AbortAfterInterjections = 1
        };
        JsonArray messages = CreateRepeatedToolCalls("build", "{}");
        messages.Add(new JsonObject { ["role"] = "user", ["name"] = "loop_detection_interjection", ["content"] = "hint" });

        LoopDetectionResult result = new LoopDetectionService(config).DetectLoop(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.IsTrue(result.ShouldAbort);
        Assert.AreEqual(1, result.InterjectionCount);
    }

    [TestMethod]
    public void LoopDetection_InterjectionsBeforeLatestUserTurnAreNotCounted()
    {
        var config = new LoopDetectionConfig
        {
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true,
            AbortAfterInterjections = 1
        };
        JsonArray messages = new JsonArray
        {
            CreateToolMessage("tool-1", "build", "{}"),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-1", ["content"] = "r1" },
            CreateToolMessage("tool-2", "build", "{}"),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-2", ["content"] = "r2" },
            CreateToolMessage("tool-3", "build", "{}"),
            new JsonObject { ["role"] = "user", ["name"] = "loop_detection_interjection", ["content"] = "old hint" },
            new JsonObject { ["role"] = "user", ["content"] = "new task" },
            CreateToolMessage("tool-4", "build", "{}"),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-4", ["content"] = "r4" },
            CreateToolMessage("tool-5", "build", "{}"),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-5", ["content"] = "r5" },
            CreateToolMessage("tool-6", "build", "{}")
        };

        LoopDetectionResult result = new LoopDetectionService(config).DetectLoop(messages);

        Assert.AreEqual(0, result.InterjectionCount);
        Assert.IsFalse(result.ShouldAbort);
    }

    [TestMethod]
    public void LoopDetection_TextOutputLoopTriggersInterjection()
    {
        var config = new LoopDetectionConfig { TriggerAfter = 3, DetectionWindow = 5, SimilarityThreshold = 0.95 };
        JsonArray messages = new JsonArray
        {
            new JsonObject { ["role"] = "assistant", ["content"] = "The build is running, let me check again" },
            new JsonObject { ["role"] = "assistant", ["content"] = "The build is running, let me check again" },
            new JsonObject { ["role"] = "assistant", ["content"] = "The build is running, let me check again" }
        };

        LoopDetectionResult result = new LoopDetectionService(config).ApplyToConversation(messages);

        Assert.IsTrue(result.IsLoopDetected);
        Assert.IsNotNull(result.InterjectionMessage);
        Assert.AreEqual(1, result.InterjectionCount);
    }

    private static JsonArray CreateRepeatedToolCalls(string toolName, string arguments)
    {
        return new JsonArray
        {
            CreateToolMessage("tool-1", toolName, arguments),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-1", ["content"] = "result 1" },
            CreateToolMessage("tool-2", toolName, arguments),
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "tool-2", ["content"] = "result 2" },
            CreateToolMessage("tool-3", toolName, arguments)
        };
    }

    private static JsonObject CreateToolMessage(string id, string toolName, string arguments) => new()
    {
        ["role"] = "assistant",
        ["tool_calls"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = id,
                ["function"] = new JsonObject { ["name"] = toolName, ["arguments"] = arguments }
            }
        }
    };

    [TestMethod]
    public void StreamingLoopDetector_RepeatedContentTriggersAbort()
    {
        var config = new LoopDetectionConfig
        {
            Enabled = true,
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true,
            AbortAfterInterjections = 1,
            InterjectionEnabled = true,
            MaxInterjections = 2
        };
        var detector = new StreamingLoopDetector(config);
        JsonObject chunk = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["delta"] = new JsonObject { ["content"] = "The build is running, let me check again" }
                }
            }
        };

        Assert.IsNull(detector.ObserveChunk(chunk));
        Assert.IsNull(detector.ObserveChunk(chunk));
        StreamingLoopDecision? decision = detector.ObserveChunk(chunk);

        Assert.IsNotNull(decision);
        Assert.AreEqual(3, decision!.RepeatCount);
        Assert.AreEqual(StreamingLoopKind.TextOutput, decision.LoopKind);
        Assert.IsTrue(decision.InterjectionMessage is not null);
        Assert.IsFalse(decision.ShouldAbort);

        decision = detector.ObserveChunk(chunk);
        Assert.IsNotNull(decision);
        Assert.IsTrue(decision!.ShouldAbort);
    }

    [TestMethod]
    public void StreamingLoopDetector_RepeatedToolCallArgumentFragmentsDoNotTrigger()
    {
        var config = new LoopDetectionConfig
        {
            Enabled = true,
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true,
            AbortAfterInterjections = 1,
            InterjectionEnabled = true,
            MaxInterjections = 2
        };
        var detector = new StreamingLoopDetector(config);
        JsonObject chunk = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["function"] = new JsonObject { ["name"] = "grep", ["arguments"] = "{\"pattern\":\"" }
                            }
                        }
                    }
                }
            }
        };

        for (int index = 0; index < 5; index++)
        {
            Assert.IsNull(detector.ObserveChunk(chunk));
        }
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_RepeatedToolArgumentFragmentsArePreserved()
    {
        const string initialArguments = "{\"pattern\":\"";
        var initialChunk = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] = "call-1",
                                ["type"] = "function",
                                ["function"] = new JsonObject { ["name"] = "grep", ["arguments"] = initialArguments }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        var repeatedArgumentChunk = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["function"] = new JsonObject { ["arguments"] = " " }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        var finalArgumentChunk = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["function"] = new JsonObject { ["arguments"] = string.Concat('"', '}') }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        string[] frames = new[] { initialChunk.ToJsonString() }
            .Concat(Enumerable.Repeat(repeatedArgumentChunk.ToJsonString(), 7))
            .Append(finalArgumentChunk.ToJsonString())
            .Append("{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}")
            .Append("[DONE]")
            .ToArray();
        var config = new LoopDetectionConfig
        {
            Enabled = true,
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true,
            AbortAfterInterjections = 1
        };

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) =
            await TransformAsync(CreateSse(frames), loopDetectionConfig: config);
        JsonObject[] toolChunks = ParseChunks(output)
            .Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray)
            .ToArray();
        string arguments = string.Concat(toolChunks.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString()));

        Assert.AreEqual(9, toolChunks.Length);
        JsonNode? parsedArguments = JsonNode.Parse(arguments);
        Assert.AreEqual(new string(' ', 7), parsedArguments?["pattern"]?.ToString());
        Assert.IsTrue(result.DoneReceived);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public void StreamingLoopDetector_DifferentContentDoesNotTrigger()
    {
        var config = new LoopDetectionConfig
        {
            Enabled = true,
            TriggerAfter = 3,
            DetectionWindow = 5,
            AbortEnabled = true
        };
        var detector = new StreamingLoopDetector(config);
        JsonObject chunk1 = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject { ["content"] = "Reading the file" } }
            }
        };
        JsonObject chunk2 = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject { ["content"] = "Editing the file" } }
            }
        };

        Assert.IsNull(detector.ObserveChunk(chunk1));
        Assert.IsNull(detector.ObserveChunk(chunk2));
        Assert.IsNull(detector.ObserveChunk(chunk1));
        Assert.IsNull(detector.ObserveChunk(chunk2));
    }

    [TestMethod]
    public void StreamingLoopDetector_DisabledConfigDoesNotTrigger()
    {
        var config = new LoopDetectionConfig
        {
            Enabled = false,
            TriggerAfter = 3,
            AbortEnabled = true
        };
        var detector = new StreamingLoopDetector(config);
        JsonObject chunk = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject { ["content"] = "repeat" } }
            }
        };

        Assert.IsNull(detector.ObserveChunk(chunk));
        Assert.IsNull(detector.ObserveChunk(chunk));
        Assert.IsNull(detector.ObserveChunk(chunk));
    }

    private static JsonObject CreateParallelToolMessage(string firstName, string secondName) => new()
    {
        ["role"] = "assistant",
        ["tool_calls"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["function"] = new JsonObject { ["name"] = firstName, ["arguments"] = "{}" }
            },
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["function"] = new JsonObject { ["name"] = secondName, ["arguments"] = "{}" }
            }
        }
    };

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NormalText_PreservesStopAndDone()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hello\"},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject contentChunk = ParseChunks(output).First(chunk => chunk["choices"]?[0]?["delta"]?["content"] != null);

        Assert.AreEqual("Hello", contentChunk["choices"]?[0]?["delta"]?["content"]?.ToString());
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.IsTrue(result.DoneReceived);
        Assert.AreEqual(5, result.GeneratedContentLength);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, result.Status);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NemotronXmlToolCall_ConvertsFunctionAndPreservesParameterValues()
    {
        const string toolMarkup = "<tool_call>\n<function=lookup_project>\n<parameter=name>\nalpha\n</parameter>\n<parameter=query>\nfirst line\nsecond line\n</parameter>\n<parameter=options>\n{\"dry_run\":true}\n</parameter>\n</function>\n</tool_call>";
        JsonObject contentFrame = new()
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject { ["content"] = toolMarkup },
                    ["finish_reason"] = null
                }
            }
        };

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(CreateSse(
            contentFrame.ToJsonString(),
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "[DONE]"));
        JsonObject[] chunks = ParseChunks(output).ToArray();
        JsonObject toolDelta = chunks.Single(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray)["choices"]![0]!["delta"]!["tool_calls"]![0]!.AsObject();
        JsonObject arguments = JsonNode.Parse(toolDelta["function"]?["arguments"]?.ToString() ?? "{}")!.AsObject();

        Assert.AreEqual("lookup_project", toolDelta["function"]?["name"]?.ToString());
        Assert.AreEqual("alpha", arguments["name"]?.ToString());
        Assert.AreEqual("first line\nsecond line", arguments["query"]?.ToString());
        Assert.IsTrue(arguments["options"]?["dry_run"]!.GetValue<bool>());
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        Assert.IsFalse(chunks.Any(chunk => (chunk["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty).Contains("<tool_call>", StringComparison.Ordinal)));
        Assert.AreEqual(1, chunks.Count(chunk => chunk["choices"]?[0]?["finish_reason"]?.ToString() == "tool_calls"));
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ToolCallWithoutText_PreservesFragmentedArgumentsAndFinish()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-1\",\"type\":\"function\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"file.cs\\\"}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream, getGenerationStatsText: true);
        JsonObject[] chunks = ParseChunks(output).ToArray();
        JsonObject[] toolChunks = chunks.Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray).ToArray();
        string arguments = string.Concat(toolChunks.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString()));

        Assert.AreEqual(2, toolChunks.Length);
        Assert.AreEqual("call-1", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("function", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["type"]?.ToString());
        Assert.AreEqual("read_file", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("{\"path\":\"file.cs\"}", arguments);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.AreEqual(1, result.ToolCallCount);
        Assert.AreEqual(0, result.GeneratedContentLength);
        Assert.IsTrue(result.DoneReceived);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        Assert.IsFalse(output.Contains("Generation Stats", StringComparison.Ordinal));
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NullContentMetadataChunk_PreservesToolCallAndFragments()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"delta\":{\"content\":null,\"tool_calls\":[{\"index\":0,\"id\":\"abc123\",\"type\":\"function\",\"function\":{\"name\":\"task_complete\",\"arguments\":\"{\\\"x\\\"\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\":1}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject[] chunks = ParseChunks(output).ToArray();
        JsonObject[] toolChunks = chunks.Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray).ToArray();

        Assert.AreEqual(2, toolChunks.Length);
        Assert.AreEqual("abc123", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("function", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["type"]?.ToString());
        Assert.AreEqual("task_complete", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("{\"x\":1}", string.Concat(toolChunks.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString())));
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.IsTrue(result.DoneReceived);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NativeToolDeltaAfterLegacyToolCall_IsStillForwarded()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"delta\":{\"content\":\"{\\\"name\\\":\\\"legacy_tool\\\",\\\"arguments\\\":\\\"{}\\\"}\"},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{\"content\":null,\"tool_calls\":[{\"index\":0,\"id\":\"abc123\",\"type\":\"function\",\"function\":{\"name\":\"task_complete\",\"arguments\":\"{\\\"x\\\"\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\":1}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, _) = await TransformAsync(stream);
        JsonObject[] nativeChunks = ParseChunks(output)
            .Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["id"]?.ToString() == "abc123")
            .ToArray();

        Assert.AreEqual(1, nativeChunks.Length);
        Assert.AreEqual("task_complete", nativeChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_FourFragmentNativeToolCall_PreservesEveryArgumentFragment()
    {
        string[] fragments = ["{", "\"path\":\"", "foo.cs", "\"}"];
        string stream = CreateSse(
            CreateToolDelta(0, "fragmented", "function", "read_file", fragments[0]),
            CreateToolDelta(0, null, null, null, fragments[1]),
            CreateToolDelta(0, null, null, null, fragments[2]),
            CreateToolDelta(0, null, null, null, fragments[3]),
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject[] toolChunks = ParseChunks(output)
            .Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray)
            .ToArray();
        string[] forwardedFragments = toolChunks
            .Select(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString() ?? string.Empty)
            .ToArray();

        CollectionAssert.AreEqual(fragments, forwardedFragments);
        Assert.AreEqual("fragmented", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("function", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["type"]?.ToString());
        Assert.AreEqual("read_file", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NativeDeltasAfterFirstMetadata_AreNeverSuppressed()
    {
        string[] fragments = ["{\"", "path", "\":\"", "foo.cs\"}"];
        string[] frames = fragments.Select((fragment, index) =>
            CreateToolDelta(0, index == 0 ? "call-many" : null, index == 0 ? "function" : null,
                index == 0 ? "read_file" : null, fragment)).ToArray();
        (string output, _) = await TransformAsync(CreateSse(frames
            .Concat(["{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}", "[DONE]"])
            .ToArray()));

        JsonObject[] forwarded = ParseChunks(output)
            .Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray)
            .ToArray();
        Assert.AreEqual(fragments.Length, forwarded.Length);
        CollectionAssert.AreEqual(fragments, forwarded.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString() ?? string.Empty).ToArray());
    }

    [TestMethod]
    public void ToolHistory_AssistantCallAndResultSurviveNextSanitizedRequest()
    {
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "system", ["content"] = "system" },
            new JsonObject { ["role"] = "user", ["content"] = "inspect" },
            CreateAssistantToolCall("A", "read_file", "{\"path\":\"a.cs\"}"),
            CreateToolResult("A", "contents")
        };

        JsonArray sanitized = SanitizeMessages(messages);
        Assert.IsTrue(JsonNode.DeepEquals(messages, sanitized));
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(sanitized).IsValid);
    }

    [TestMethod]
    public void ToolHistory_MultipleAssistantToolPairsRemainOrdered()
    {
        JsonArray messages = CreateMultiPairHistory(("A", "read_file"), ("B", "sql"), ("C", "write_file"));

        JsonArray sanitized = SanitizeMessages(messages);
        Assert.IsTrue(JsonNode.DeepEquals(messages, sanitized));
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(sanitized).IsValid);
        CollectionAssert.AreEqual(
            new[] { "A", "B", "C" },
            sanitized.Where(message => message?["role"]?.ToString() == "tool")
                .Select(message => message?["tool_call_id"]?.ToString()).ToArray());
    }

    [TestMethod]
    public void ToolHistory_MismatchedResultId_IsRejectedWithDiagnostic()
    {
        JsonArray messages = new() { CreateAssistantToolCall("A", "read_file", "{}"), CreateToolResult("B", "result") };

        ToolHistoryInspection result = LlamaAgentLoopDiagnostics.ValidateToolHistory(messages);
        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join(";", result.Issues), "tool result id B");
    }

    [TestMethod]
    public void ToolHistory_DuplicateResultId_IsRejectedWithDiagnostic()
    {
        JsonArray messages = new()
        {
            CreateAssistantToolCall("A", "read_file", "{}"),
            CreateToolResult("A", "result"),
            CreateToolResult("A", "duplicate")
        };

        ToolHistoryInspection result = LlamaAgentLoopDiagnostics.ValidateToolHistory(messages);
        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join(";", result.Issues), "duplicated");
    }

    [TestMethod]
    public void ToolHistory_NullAssistantContentWithToolCallIsPreserved()
    {
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "assistant", ["content"] = null, ["tool_calls"] = CreateToolCalls(("A", "read_file", "{}")) },
            CreateToolResult("A", "result"),
            new JsonObject { ["role"] = "assistant", ["content"] = null }
        };

        JsonArray sanitized = SanitizeMessages(messages);
        Assert.IsTrue(JsonNode.DeepEquals(messages, sanitized));
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(sanitized).IsValid);
        Assert.IsNull(sanitized[0]?["content"]);
        Assert.AreEqual("A", sanitized[0]?["tool_calls"]?[0]?["id"]?.ToString());
    }

    [TestMethod]
    public void ToolHistory_ParallelCallsAndResultsRemainMatched()
    {
        JsonArray messages = new()
        {
            new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = null,
                ["tool_calls"] = CreateToolCalls(("A", "read_file", "{}"), ("B", "sql", "{}"))
            },
            CreateToolResult("A", "read"),
            CreateToolResult("B", "rows")
        };

        JsonObject request = new() { ["parallel_tool_calls"] = true, ["messages"] = messages };
        JsonObject sanitized = JsonNode.Parse(LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100000))!.AsObject();

        Assert.IsTrue(sanitized["parallel_tool_calls"]!.GetValue<bool>());
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(sanitized["messages"]!.AsArray()).IsValid);
        CollectionAssert.AreEqual(new[] { "A", "B" }, sanitized["messages"]![0]!["tool_calls"]!.AsArray()
            .Select(call => call?["id"]?.ToString()).ToArray());
    }

    [TestMethod]
    public void ToolHistory_ParallelToolCallsFalseAndToolDefinitionsArePreserved()
    {
        JsonObject request = new()
        {
            ["parallel_tool_calls"] = false,
            ["tools"] = new JsonArray
            {
                new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = "read_file" } },
                new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = "sql" } }
            },
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "go" } }
        };

        JsonObject sanitized = JsonNode.Parse(LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100000))!.AsObject();
        Assert.IsFalse(sanitized["parallel_tool_calls"]!.GetValue<bool>());
        Assert.AreEqual(2, sanitized["tools"]!.AsArray().Count);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_LargeFragmentedArguments_AreNotTruncated()
    {
        string arguments = "{\"value\":\"" + new string('x', 25000) + "\"}";
        const int fragmentLength = 37;
        var frames = new List<string>();
        for (int offset = 0, fragment = 0; offset < arguments.Length; offset += fragmentLength, fragment++)
        {
            string part = arguments.Substring(offset, Math.Min(fragmentLength, arguments.Length - offset));
            frames.Add(CreateToolDelta(0, fragment == 0 ? "large-call" : null, fragment == 0 ? "function" : null,
                fragment == 0 ? "large_tool" : null, part));
        }
        frames.Add("{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}");
        frames.Add("[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(CreateSse(frames.ToArray()));
        JsonObject[] chunks = ParseChunks(output)
            .Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray)
            .ToArray();
        string reconstructed = string.Concat(chunks.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString()));

        Assert.AreEqual(arguments, reconstructed);
        Assert.AreEqual(frames.Count - 2, chunks.Length);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        Assert.IsTrue(result.DoneReceived);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_IncompleteNativeToolCall_DoesNotInventFinishReason()
    {
        string stream = CreateSse(CreateToolDelta(0, "unfinished", "function", "read_file", "{\"path\":"));
        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);

        Assert.IsFalse(result.DoneReceived);
        Assert.IsNull(result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.UpstreamError, result.Status);
        Assert.IsFalse(output.Contains("\"finish_reason\":\"tool_calls\"", StringComparison.Ordinal));
        Assert.IsFalse(output.Contains("[DONE]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_CompletedToolCallAndDone_IsCompletedByToolCall()
    {
        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(
            CreateSse(CreateToolDelta(0, "complete", "function", "read_file", "{}"),
                "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}", "[DONE]"));

        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.IsTrue(result.DoneReceived);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NormalTextAfterPreviousToolCall_IsASeparateNormalCompletion()
    {
        (string toolOutput, _) = await TransformAsync(CreateSse(
            CreateToolDelta(0, "prior", "function", "read_file", "{}"),
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}", "[DONE]"));
        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(CreateSse(
            "{\"choices\":[{\"delta\":{\"content\":\"Done\"},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}", "[DONE]"));

        Assert.IsTrue(ParseChunks(toolOutput).Any(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray));
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, result.Status);
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.AreEqual("Done", ParseChunks(output).First()["choices"]?[0]?["delta"]?["content"]?.ToString());
    }

    [TestMethod]
    public async Task AgentLoop_ThreeExplicitRequestsDoNotCreateAnAutonomousFourthResponse()
    {
        JsonArray requestOne = new() { new JsonObject { ["role"] = "user", ["content"] = "inspect and edit" } };
        (string firstOutput, _) = await TransformAsync(CreateSse(
            CreateToolDelta(0, "A", "function", "read_file", "{}"),
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}", "[DONE]"));

        JsonArray requestTwo = requestOne.DeepClone().AsArray();
        requestTwo.Add(CreateAssistantToolCall("A", "read_file", "{}"));
        requestTwo.Add(CreateToolResult("A", "file contents"));
        (string secondOutput, _) = await TransformAsync(CreateSse(
            CreateToolDelta(0, "B", "function", "write_file", "{\"path\":\"x\"}"),
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"tool_calls\"}]}", "[DONE]"));

        JsonArray requestThree = requestTwo.DeepClone().AsArray();
        requestThree.Add(CreateAssistantToolCall("B", "write_file", "{\"path\":\"x\"}"));
        requestThree.Add(CreateToolResult("B", "written"));
        (string thirdOutput, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(CreateSse(
            "{\"choices\":[{\"delta\":{\"content\":\"Finished\"},\"finish_reason\":null}]}",
            "{\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}", "[DONE]"));

        Assert.IsTrue(ParseChunks(firstOutput).Any(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray));
        Assert.IsTrue(ParseChunks(secondOutput).Any(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray));
        Assert.AreEqual("Finished", ParseChunks(thirdOutput).First()["choices"]?[0]?["delta"]?["content"]?.ToString());
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.AreEqual(3, new[] { firstOutput, secondOutput, thirdOutput }.Length);
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(requestThree).IsValid);
    }

    [TestMethod]
    public void AgentLoop_HistoryWithPreviouslyReturnedToolResultIsNotTreatedAsDuplicate()
    {
        JsonArray messages = CreateMultiPairHistory(("A", "read_file"), ("B", "sql"));
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = "continue" });
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(messages).IsValid);
        Assert.AreEqual(
            LlamaAgentLoopDiagnostics.HashForTests(messages),
            LlamaAgentLoopDiagnostics.HashForTests(messages.DeepClone()));
    }

    [TestMethod]
    public void AgentLoop_RequestMessageHashChangesWhenToolResultChanges()
    {
        JsonObject firstRequest = new()
        {
            ["temperature"] = 0.2,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "assistant", ["tool_calls"] = CreateToolCalls(("A", "read_file", "{}")) },
                CreateToolResult("A", "first result")
            }
        };
        JsonObject sameHistoryDifferentSettings = new()
        {
            ["temperature"] = 0.8,
            ["messages"] = firstRequest["messages"]!.DeepClone()
        };
        JsonObject changedHistory = new()
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "assistant", ["tool_calls"] = CreateToolCalls(("A", "read_file", "{}")) },
                CreateToolResult("A", "second result")
            }
        };

        Assert.AreEqual(
            LlamaAgentLoopDiagnostics.HashRequestMessages(firstRequest.ToJsonString()),
            LlamaAgentLoopDiagnostics.HashRequestMessages(sameHistoryDifferentSettings.ToJsonString()));
        Assert.AreNotEqual(
            LlamaAgentLoopDiagnostics.HashRequestMessages(firstRequest.ToJsonString()),
            LlamaAgentLoopDiagnostics.HashRequestMessages(changedHistory.ToJsonString()));

        JsonObject sameRequestDifferentPropertyOrder = new()
        {
            ["messages"] = firstRequest["messages"]!.DeepClone(),
            ["temperature"] = 0.2
        };
        Assert.AreEqual(
            LlamaAgentLoopDiagnostics.HashRequestBody(firstRequest.ToJsonString()),
            LlamaAgentLoopDiagnostics.HashRequestBody(sameRequestDifferentPropertyOrder.ToJsonString()));
        Assert.AreNotEqual(
            LlamaAgentLoopDiagnostics.HashRequestBody(firstRequest.ToJsonString()),
            LlamaAgentLoopDiagnostics.HashRequestBody(sameHistoryDifferentSettings.ToJsonString()));
        Assert.AreNotEqual(
            LlamaAgentLoopDiagnostics.HashRequestBody(firstRequest.ToJsonString()),
            LlamaAgentLoopDiagnostics.HashRequestBody(changedHistory.ToJsonString()));
    }

    [TestMethod]
    public void AgentLoop_TracksCallCreationResultReceiptAndReappearanceAcrossRequests()
    {
        const string callId = "cross-request-call";
        const string firstRequestId = "910001";
        const string resultRequestId = "910002";
        const string reappearanceRequestId = "910003";
        JsonObject firstRequest = new()
        {
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "inspect" } }
        };
        string firstBody = firstRequest.ToJsonString();
        Assert.IsTrue(LlamaAgentLoopDiagnostics.BeginRequest(
            "/test", firstRequestId, firstBody, firstBody, "llama").IsValid);
        LlamaAgentLoopDiagnostics.BeginResponse(firstRequestId);
        LlamaAgentLoopDiagnostics.ObserveResponseChunk(
            firstRequestId, JsonNode.Parse(CreateToolDelta(0, callId, "function", "read_file", "{\"path\":\"a.cs\"}")));
        LlamaAgentLoopDiagnostics.CompleteResponse(firstRequestId, true, "tool_calls");

        JsonObject secondRequest = new()
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "inspect" },
                CreateAssistantToolCall(callId, "read_file", "{\"path\":\"a.cs\"}"),
                CreateToolResult(callId, "file contents")
            }
        };
        string secondBody = secondRequest.ToJsonString();
        Assert.IsTrue(LlamaAgentLoopDiagnostics.BeginRequest(
            "/test", resultRequestId, secondBody, secondBody, "llama").IsValid);
        AgentToolCallSnapshot afterResult = LlamaAgentLoopDiagnostics.GetToolCallSnapshot(callId)!;
        Assert.AreEqual(910001L, afterResult.CreatedRequest);
        Assert.AreEqual(910002L, afterResult.ResultReceivedRequest);
        Assert.IsNull(afterResult.ReappearedRequest);

        Assert.IsTrue(LlamaAgentLoopDiagnostics.BeginRequest(
            "/test", reappearanceRequestId, secondBody, secondBody, "llama").IsValid);
        LlamaAgentLoopDiagnostics.BeginResponse(reappearanceRequestId);
        LlamaAgentLoopDiagnostics.ObserveResponseChunk(
            reappearanceRequestId, JsonNode.Parse(CreateToolDelta(0, callId, "function", "read_file", "{\"path\":\"a.cs\"}")));
        LlamaAgentLoopDiagnostics.CompleteResponse(reappearanceRequestId, true, "tool_calls");

        AgentToolCallSnapshot reappeared = LlamaAgentLoopDiagnostics.GetToolCallSnapshot(callId)!;
        Assert.AreEqual(910001L, reappeared.CreatedRequest);
        Assert.AreEqual(910002L, reappeared.ResultReceivedRequest);
        Assert.AreEqual(910003L, reappeared.ReappearedRequest);
        Assert.AreEqual(64, reappeared.ArgumentsHash.Length);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_IncompleteToolCallWithClientDisconnectIsNotCompleted()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateSse(
            CreateToolDelta(0, "partial", "function", "read_file", "{\"path\":"))));
        using var output = new DisconnectingStream();

        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(input, output, ModelName);

        Assert.IsTrue(result.ClientDisconnected);
        Assert.IsFalse(result.DoneReceived);
        Assert.IsNull(result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.ClientDisconnected, result.Status);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_LengthFinishReasonIsNotReportedAsStopOrToolCall()
    {
        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(CreateSse(
            "{\"choices\":[{\"delta\":{\"content\":\"partial\"},\"finish_reason\":\"length\"}]}", "[DONE]"));

        Assert.AreEqual("length", result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, result.Status);
        Assert.IsTrue(result.DoneReceived);
        Assert.IsFalse(output.Contains("finish_reason\":\"stop", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToolHistory_RealisticCopilotSequenceRetainsOrderAfterSanitize()
    {
        JsonArray messages = new()
        {
            new JsonObject { ["role"] = "system", ["content"] = "system" },
            new JsonObject { ["role"] = "user", ["content"] = "task" },
            CreateAssistantToolCall("A", "read", "{}"), CreateToolResult("A", "1"),
            CreateAssistantToolCall("B", "read", "{}"), CreateToolResult("B", "2"),
            CreateAssistantToolCall("C", "sql", "{}"), CreateToolResult("C", "3"),
            new JsonObject { ["role"] = "user", ["content"] = "follow-up" },
            CreateAssistantToolCall("D", "read", "{}"), CreateToolResult("D", "4"),
            CreateAssistantToolCall("E", "sql", "{}"), CreateToolResult("E", "5"),
            CreateAssistantToolCall("F", "write", "{}"), CreateToolResult("F", "6"),
            new JsonObject { ["role"] = "assistant", ["content"] = null }
        };

        JsonArray sanitized = SanitizeMessages(messages);
        Assert.IsTrue(JsonNode.DeepEquals(messages, sanitized));
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(sanitized).IsValid);
        CollectionAssert.AreEqual(
            messages.Select(message => message?["role"]?.ToString()).ToArray(),
            sanitized.Select(message => message?["role"]?.ToString()).ToArray());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_ContextTrimmingRemovesWholeOldToolTurn()
    {
        bool originalOptimization = SmartPromptOptimizationSettings.IsEnabled;
        SmartPromptOptimizationSettings.IsEnabled = false;
        try
        {
            var request = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = "system" },
                    new JsonObject { ["role"] = "user", ["content"] = "old request" },
                    CreateAssistantToolCall("old-call", "read_file", "{}"),
                    CreateToolResult("old-call", new string('r', 500)),
                    new JsonObject { ["role"] = "user", ["content"] = "new request" }
                }
            };

            JsonObject sanitized = JsonNode.Parse(
                LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100))!.AsObject();
            JsonArray messages = sanitized["messages"]!.AsArray();
            Assert.AreEqual("new request", messages[^1]?["content"]?.ToString());
            Assert.IsFalse(messages.Any(message => message?["tool_call_id"]?.ToString() == "old-call"));
            Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(messages).IsValid);
        }
        finally
        {
            SmartPromptOptimizationSettings.IsEnabled = originalOptimization;
        }
    }

    private static JsonArray CreateToolCalls(params (string Id, string Name, string Arguments)[] calls)
    {
        var result = new JsonArray();
        for (int index = 0; index < calls.Length; index++)
        {
            result.Add(new JsonObject
            {
                ["index"] = index,
                ["id"] = calls[index].Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = calls[index].Name,
                    ["arguments"] = calls[index].Arguments
                }
            });
        }
        return result;
    }

    private static JsonObject CreateAssistantToolCall(string id, string name, string arguments) => new()
    {
        ["role"] = "assistant",
        ["content"] = null,
        ["tool_calls"] = CreateToolCalls((id, name, arguments))
    };

    private static JsonObject CreateToolResult(string id, string content) => new()
    {
        ["role"] = "tool",
        ["tool_call_id"] = id,
        ["content"] = content
    };

    private static JsonArray CreateMultiPairHistory(params (string Id, string Name)[] calls)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = "system" } };
        for (int index = 0; index < calls.Length; index++)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = $"request {index}" });
            messages.Add(CreateAssistantToolCall(calls[index].Id, calls[index].Name, "{}"));
            messages.Add(CreateToolResult(calls[index].Id, $"result {index}"));
        }
        return messages;
    }

    private static JsonArray SanitizeMessages(JsonArray messages)
    {
        var request = new JsonObject
        {
            ["messages"] = messages.DeepClone()
        };
        bool originalStrictRules = SmartPromptOptimizationSettings.InjectStrictToolCallingRules;
        SmartPromptOptimizationSettings.InjectStrictToolCallingRules = false;
        try
        {
            JsonObject sanitized = JsonNode.Parse(
                LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100000))!.AsObject();
            return sanitized["messages"]!.AsArray();
        }
        finally
        {
            SmartPromptOptimizationSettings.InjectStrictToolCallingRules = originalStrictRules;
        }
    }

    private static string CreateToolDelta(int index, string? id, string? type, string? name, string arguments)
    {
        var toolCall = new JsonObject { ["index"] = index };
        if (id != null) toolCall["id"] = id;
        if (type != null) toolCall["type"] = type;
        var function = new JsonObject();
        if (name != null) function["name"] = name;
        function["arguments"] = arguments;
        toolCall["function"] = function;

        return new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["delta"] = new JsonObject { ["tool_calls"] = new JsonArray(toolCall) },
                    ["finish_reason"] = null
                }
            }
        }.ToJsonString();
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ContentAlongsideNativeToolCall_PreservesBothDeltas()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"{\\\"name\\\":\\\"not_a_tool\\\"}\",\"tool_calls\":[{\"index\":0,\"id\":\"call-native\",\"type\":\"function\",\"function\":{\"name\":\"native_tool\",\"arguments\":\"{}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject chunk = ParseChunks(output).First(item => item["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray);

        Assert.AreEqual("{\"name\":\"not_a_tool\"}", chunk["choices"]?[0]?["delta"]?["content"]?.ToString());
        Assert.AreEqual("native_tool", chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual(1, result.ToolCallCount);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_SequentialToolResponses_AllowFinalAssistantAnswer()
    {
        (string firstOutput, LlamaStreamTransformer.OpenAiStreamTransformResult firstResult) =
            await TransformAsync(CreateToolCallResponse("call-1", "read_file"));
        (string secondOutput, LlamaStreamTransformer.OpenAiStreamTransformResult secondResult) =
            await TransformAsync(CreateToolCallResponse("call-2", "run_command"));
        (string finalOutput, LlamaStreamTransformer.OpenAiStreamTransformResult finalResult) =
            await TransformAsync(CreateSse(
                "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Finished\"},\"finish_reason\":null}]}",
                "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
                "[DONE]"));

        Assert.AreEqual("tool_calls", firstResult.FinalFinishReason);
        Assert.AreEqual("read_file", ParseChunks(firstOutput).First()["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("tool_calls", secondResult.FinalFinishReason);
        Assert.AreEqual("run_command", ParseChunks(secondOutput).First()["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, finalResult.Status);
        Assert.AreEqual("Finished", ParseChunks(finalOutput).First()["choices"]?[0]?["delta"]?["content"]?.ToString());
    }

    [TestMethod]
    public async Task TransformOpenAiStreamToOllamaAsync_ToolCall_ProducesOllamaToolResultChunk()
    {
        string stream = CreateToolCallResponse("call-ollama", "read_file");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
        using var output = new MemoryStream();

        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamToOllamaAsync(input, output, ModelName);
        JsonObject[] chunks = Encoding.UTF8.GetString(output.ToArray())
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .ToArray();
        JsonObject finalChunk = chunks[^1];

        Assert.AreEqual(1, chunks.Length);
        Assert.IsTrue(finalChunk["done"]!.GetValue<bool>());
        Assert.AreEqual("tool_calls", finalChunk["done_reason"]?.ToString());
        Assert.AreEqual("read_file", finalChunk["message"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("file.cs", finalChunk["message"]?["tool_calls"]?[0]?["function"]?["arguments"]?["path"]?.ToString());
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_MissingDone_ReportsIncompleteUpstreamStream()
    {
        (string _, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(
            CreateSse("{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"partial\"},\"finish_reason\":\"stop\"}] }"));

        Assert.IsFalse(result.DoneReceived);
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.UpstreamError, result.Status);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ClientDisconnect_ReportsDisconnectWithoutSuccessfulCompletion()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}",
            "[DONE]")));
        using var output = new DisconnectingStream();

        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(input, output, ModelName);

        Assert.IsTrue(result.ClientDisconnected);
        Assert.IsFalse(result.DoneReceived);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.ClientDisconnected, result.Status);
    }

    [TestMethod]
    public void SanitizeIncomingRequest_ThinkingBlockTrimmingIsOptIn()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = "<think>private reasoning</think>Visible answer",
                    ["reasoning_content"] = "separate private reasoning"
                }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100000))!.AsObject();
        JsonObject assistant = sanitized["messages"]!.AsArray().OfType<JsonObject>().First();

        Assert.AreEqual("<think>private reasoning</think>Visible answer", assistant["content"]?.ToString());
        Assert.AreEqual("separate private reasoning", assistant["reasoning_content"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_TrimsOnlyOlderMessagesAndHandlesNestedBlocks()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = "A<think>outer<think>nested</think>remainder</think>M<think>second</think>B",
                    ["reasoning_content"] = "separate private reasoning"
                },
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-1", ["content"] = "Tool before<think>tool thought</think>after" },
                new JsonObject { ["role"] = "assistant", ["content"] = "<think>recent thought</think>Recent answer" },
                new JsonObject { ["role"] = "user", ["content"] = "Keep <think>quoted text</think> intact." }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "llama", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 2))!.AsObject();
        JsonArray messages = sanitized["messages"]!.AsArray();
        JsonObject[] assistantMessages = messages.OfType<JsonObject>()
            .Where(message => string.Equals(message["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase)).ToArray();
        JsonObject toolMessage = messages.OfType<JsonObject>()
            .First(message => string.Equals(message["role"]?.ToString(), "tool", StringComparison.OrdinalIgnoreCase));
        JsonObject userMessage = messages
            .OfType<JsonObject>()
            .First(message => string.Equals(message["role"]?.ToString(), "user", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual("AMB", assistantMessages[0]["content"]?.ToString());
        Assert.IsFalse(assistantMessages[0].ContainsKey("reasoning_content"));
        Assert.AreEqual("Tool before<think>tool thought</think>after", toolMessage["content"]?.ToString());
        Assert.AreEqual("call-1", toolMessage["tool_call_id"]?.ToString());
        Assert.AreEqual("<think>recent thought</think>Recent answer", assistantMessages[1]["content"]?.ToString());
        Assert.AreEqual("Keep <think>quoted text</think> intact.", userMessage["content"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_TrimThinkingBlocksPreservesMultimodalParts()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = "Before<think>private</think>After" },
                        new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = "data:image/png;base64,AA==" } }
                    }
                }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "llama", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 0))!.AsObject();
        JsonObject assistant = sanitized["messages"]!.AsArray().OfType<JsonObject>()
            .First(message => message["role"]?.ToString() == "assistant");
        JsonArray content = assistant["content"]!.AsArray();

        Assert.AreEqual("BeforeAfter", content[0]?["text"]?.ToString());
        Assert.AreEqual("image_url", content[1]?["type"]?.ToString());
        Assert.AreEqual("data:image/png;base64,AA==", content[1]?["image_url"]?["url"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_TrimToolResultsOnlyWhenEnabled()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-1", ["content"] = "Tool before<think>private</think>after" },
                new JsonObject { ["role"] = "user", ["content"] = "Continue." }
            }
        };

        JsonArray untrimmedMessages = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "llama", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 1))!["messages"]!.AsArray();
        JsonArray trimmedMessages = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "llama", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 1, trimToolResults: true))!["messages"]!.AsArray();
        JsonObject untrimmedTool = untrimmedMessages.OfType<JsonObject>().First(message => message["role"]?.ToString() == "tool");
        JsonObject trimmedTool = trimmedMessages.OfType<JsonObject>().First(message => message["role"]?.ToString() == "tool");

        Assert.AreEqual("Tool before<think>private</think>after", untrimmedTool["content"]?.ToString());
        Assert.AreEqual("Tool beforeafter", trimmedTool["content"]?.ToString());
        Assert.AreEqual("call-1", trimmedTool["tool_call_id"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_ToolCallModesPreserveOrSkeletonizeCallsWithoutLosingHistory()
    {
        var originalToolCalls = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "call-A",
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = "read_file", ["arguments"] = "{\"path\":\"old.cs\"}" }
            },
            new JsonObject
            {
                ["id"] = "call-B",
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = "write_file", ["arguments"] = "{\"path\":\"new.cs\"}" }
            }
        };
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = "Before<think>old reasoning</think>After",
                    ["tool_calls"] = originalToolCalls
                },
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-A", ["content"] = "read result" },
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-B", ["content"] = "write result" },
                new JsonObject { ["role"] = "user", ["content"] = "Next request." }
            }
        };

        JsonArray keptMessages = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "llama", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 1, toolCallMode: "Keep"))!["messages"]!.AsArray();
        JsonArray skeletonMessages = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(
                request.ToJsonString(), "qwen", numCtx: 100000,
                trimThinkingBlocks: true, keepLastMessages: 1, toolCallMode: "Skeleton"))!["messages"]!.AsArray();
        JsonObject keptAssistant = keptMessages.OfType<JsonObject>().First(message => message["role"]?.ToString() == "assistant");
        JsonObject skeletonAssistant = skeletonMessages.OfType<JsonObject>().First(message => message["role"]?.ToString() == "assistant");
        JsonObject[] skeletonCalls = skeletonAssistant["tool_calls"]!.AsArray().OfType<JsonObject>().ToArray();
        JsonObject[] skeletonToolResults = skeletonMessages.OfType<JsonObject>().Where(message => message["role"]?.ToString() == "tool").ToArray();

        Assert.IsTrue(JsonNode.DeepEquals(originalToolCalls, keptAssistant["tool_calls"]));
        Assert.AreEqual("BeforeAfter", keptAssistant["content"]?.ToString());
        CollectionAssert.AreEqual(new[] { "call-A", "call-B" }, skeletonCalls.Select(call => call["id"]?.ToString()).ToArray());
        CollectionAssert.AreEqual(new[] { "read_file", "write_file" }, skeletonCalls.Select(call => call["function"]?["name"]?.ToString()).ToArray());
        CollectionAssert.AreEqual(new[] { "{}", "{}" }, skeletonCalls.Select(call => call["function"]?["arguments"]?.ToString()).ToArray());
        CollectionAssert.AreEqual(new[] { "call-A", "call-B" }, skeletonToolResults.Select(message => message["tool_call_id"]?.ToString()).ToArray());
        Assert.IsTrue(LlamaAgentLoopDiagnostics.ValidateToolHistory(skeletonMessages).IsValid);
    }

    [TestMethod]
    public void SanitizeIncomingRequest_ThinkingTrimmingIsIdempotent()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "assistant", ["content"] = "A<think>nested<think>inner</think>outer</think>B" }
            }
        };

        string once = LlamaStreamTransformer.SanitizeIncomingRequest(
            request.ToJsonString(), "llama", numCtx: 100000,
            trimThinkingBlocks: true, keepLastMessages: 0);
        string twice = LlamaStreamTransformer.SanitizeIncomingRequest(
            once, "llama", numCtx: 100000,
            trimThinkingBlocks: true, keepLastMessages: 0);
        JsonObject firstAssistant = JsonNode.Parse(once)!["messages"]!.AsArray()
            .OfType<JsonObject>().First(message => message["role"]?.ToString() == "assistant");
        JsonObject secondAssistant = JsonNode.Parse(twice)!["messages"]!.AsArray()
            .OfType<JsonObject>().First(message => message["role"]?.ToString() == "assistant");

        Assert.AreEqual("AB", firstAssistant["content"]?.ToString());
        Assert.AreEqual(firstAssistant["content"]?.ToString(), secondAssistant["content"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_PreservesLlamaToolHistoryAndSupportedFields()
    {
        var request = new JsonObject
        {
            ["parallel_tool_calls"] = false,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = null,
                    ["reasoning_content"] = "prior reasoning",
                    ["tool_calls"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "call-1",
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = "read_file", ["arguments"] = "{}" }
                        }
                    }
                },
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-1", ["content"] = "result" }
            }
        };

        JsonObject sanitized = JsonNode.Parse(LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama"))!.AsObject();
        JsonArray messages = sanitized["messages"]!.AsArray();

        Assert.IsFalse(sanitized["parallel_tool_calls"]!.GetValue<bool>());
        Assert.AreEqual("prior reasoning", messages[0]?["reasoning_content"]?.ToString());
        Assert.AreEqual("call-1", messages[0]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("function", messages[0]?["tool_calls"]?[0]?["type"]?.ToString());
        Assert.AreEqual("read_file", messages[0]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("{}", messages[0]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString());
        Assert.AreEqual("tool", messages[1]?["role"]?.ToString());
        Assert.AreEqual("call-1", messages[1]?["tool_call_id"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_PreservesTokenLimitsAndNemotronTemplateKwargs()
    {
        var request = new JsonObject
        {
            ["max_tokens"] = 448,
            ["max_completion_tokens"] = 512,
            ["n_predict"] = 448,
            ["reasoning_effort"] = "medium",
            ["reasoning_budget"] = 0,
            ["parallel_tool_calls"] = false,
            ["tool_choice"] = "auto",
            ["chat_template_kwargs"] = new JsonObject
            {
                ["enable_thinking"] = false,
                ["force_nonempty_content"] = true
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = "lookup_project" }
                }
            },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "Check project alpha." }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", reasoningEffort: null))!.AsObject();

        Assert.AreEqual(448, sanitized["max_tokens"]!.GetValue<int>());
        Assert.AreEqual(512, sanitized["max_completion_tokens"]!.GetValue<int>());
        Assert.AreEqual(448, sanitized["n_predict"]!.GetValue<int>());
        Assert.AreEqual("medium", sanitized["reasoning_effort"]!.GetValue<string>());
        Assert.AreEqual(0, sanitized["reasoning_budget"]!.GetValue<int>());
        Assert.IsFalse(sanitized["parallel_tool_calls"]!.GetValue<bool>());
        Assert.AreEqual("auto", sanitized["tool_choice"]!.GetValue<string>());
        Assert.IsFalse(sanitized["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.IsTrue(sanitized["chat_template_kwargs"]!["force_nonempty_content"]!.GetValue<bool>());
        Assert.AreEqual("lookup_project", sanitized["tools"]?[0]?["function"]?["name"]?.ToString());
    }

    [TestMethod]
    public void SanitizeIncomingRequest_DoesNotInventAnOutputTokenLimit()
    {
        var request = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "Continue." }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama"))!.AsObject();

        Assert.IsFalse(sanitized.ContainsKey("max_tokens"));
        Assert.IsFalse(sanitized.ContainsKey("max_completion_tokens"));
        Assert.IsFalse(sanitized.ContainsKey("n_predict"));
    }

    [TestMethod]
    public void SanitizeIncomingRequest_UsesLlamaSamplingNamesAndOmitsUnsetOverrides()
    {
        var request = new JsonObject
        {
            ["repetition_penalty"] = 0,
            ["top_p"] = 0,
            ["min_p"] = 0,
            ["top_k"] = 0,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = "Continue." }
            }
        };

        JsonObject sanitized = JsonNode.Parse(LlamaStreamTransformer.SanitizeIncomingRequest(
            request.ToJsonString(), "llama", temperature: 0.6, repetitionPenalty: 0,
            presencePenalty: 0, userDefinedTopP: 0, userDefinedMinP: 0, userDefinedTopK: 0))!.AsObject();

        Assert.AreEqual(0.6, sanitized["temperature"]!.GetValue<double>(), 0.0001);
        Assert.AreEqual(0, sanitized["presence_penalty"]!.GetValue<double>());
        Assert.IsFalse(sanitized.ContainsKey("repetition_penalty"));
        Assert.IsFalse(sanitized.ContainsKey("repeat_penalty"));
        Assert.IsFalse(sanitized.ContainsKey("top_p"));
        Assert.IsFalse(sanitized.ContainsKey("min_p"));
        Assert.IsFalse(sanitized.ContainsKey("top_k"));
    }

    [TestMethod]
    public void ChatTemplateProfile_MapsUnsupportedReasoningEffortToTemplateKeyword()
    {
        var props = new JsonObject
        {
            ["chat_template"] = "{% set enable_thinking = enable_thinking if enable_thinking is defined else True %}",
            ["chat_template_caps"] = new JsonObject
            {
                ["supports_reasoning_effort"] = false,
                ["supports_parallel_tool_calls"] = false,
                ["supports_tools"] = true,
                ["supports_tool_calls"] = true
            }
        };
        LlamaChatTemplateProfile profile = LlamaChatTemplateProfile.FromProps(props)!;
        var request = new JsonObject
        {
            ["reasoning_effort"] = "xhigh",
            ["parallel_tool_calls"] = true,
            ["tools"] = new JsonArray { new JsonObject { ["type"] = "function" } }
        };

        bool changed = profile.ApplyCompatibility(request);

        Assert.IsTrue(changed);
        Assert.IsFalse(request.ContainsKey("reasoning_effort"));
        Assert.IsTrue(request["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.IsFalse(request["parallel_tool_calls"]!.GetValue<bool>());
        Assert.AreEqual(1, request["tools"]!.AsArray().Count);
        Assert.AreEqual(false, profile.SupportsReasoningEffort);
        Assert.AreEqual(props["chat_template"]!.ToString(), profile.Template);
    }

    [TestMethod]
    public void ChatTemplateProfile_UsesLegacyPromptWhenNativeToolsAreUnsupported()
    {
        LlamaChatTemplateProfile profile = LlamaChatTemplateProfile.FromProps(new JsonObject
        {
            ["chat_template"] = "basic-template",
            ["chat_template_caps"] = new JsonObject
            {
                ["supports_tools"] = false,
                ["supports_tool_calls"] = false
            }
        })!;
        var request = new JsonObject
        {
            ["tool_choice"] = "auto",
            ["parallel_tool_calls"] = true,
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = "grep",
                        ["description"] = "Search text",
                        ["parameters"] = new JsonObject { ["type"] = "object" }
                    }
                }
            },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = "Work carefully." },
                new JsonObject { ["role"] = "user", ["content"] = "Find a symbol." }
            }
        };

        bool changed = profile.ApplyCompatibility(request);
        string systemPrompt = request["messages"]![0]!["content"]!.ToString();

        Assert.IsTrue(changed);
        Assert.IsFalse(request.ContainsKey("tools"));
        Assert.IsFalse(request.ContainsKey("tool_choice"));
        Assert.IsFalse(request.ContainsKey("parallel_tool_calls"));
        StringAssert.Contains(systemPrompt, "[Bridge legacy tool definitions]");
        StringAssert.Contains(systemPrompt, "\"name\":\"grep\"");
        StringAssert.Contains(systemPrompt, "emit one JSON object");
    }

    [TestMethod]
    public void ChatTemplateProfile_RetryCandidatesRemoveOptionalControlsIncrementally()
    {
        var request = new JsonObject
        {
            ["reasoning_effort"] = "xhigh",
            ["reasoning_budget"] = 1024,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = true },
            ["tool_choice"] = "auto",
            ["parallel_tool_calls"] = true,
            ["tools"] = new JsonArray { new JsonObject { ["type"] = "function" } },
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "Hi" } }
        };

        IReadOnlyList<string> candidates = LlamaChatTemplateProfile.BuildCompatibilityRetryCandidates(request.ToJsonString());
        JsonObject first = JsonNode.Parse(candidates[0])!.AsObject();
        JsonObject second = JsonNode.Parse(candidates[1])!.AsObject();
        JsonObject third = JsonNode.Parse(candidates[2])!.AsObject();

        Assert.AreEqual(3, candidates.Count);
        Assert.IsFalse(first.ContainsKey("reasoning_effort"));
        Assert.IsTrue(first.ContainsKey("chat_template_kwargs"));
        Assert.IsFalse(second.ContainsKey("chat_template_kwargs"));
        Assert.IsFalse(third.ContainsKey("tool_choice"));
        Assert.IsFalse(third.ContainsKey("parallel_tool_calls"));
        Assert.AreEqual(1, third["tools"]!.AsArray().Count);
    }

    [TestMethod]
    public void SanitizeIncomingRequest_NativeToolsDoNotReceiveLegacyJsonOnlyInstructions()
    {
        var request = new JsonObject
        {
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = "read_file" }
                }
            },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = "Keep the requested answer concise." },
                new JsonObject { ["role"] = "user", ["content"] = "Read a file." }
            }
        };

        JsonObject sanitized = JsonNode.Parse(
            LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama", numCtx: 100000))!.AsObject();
        Assert.AreEqual("Keep the requested answer concise.", sanitized["messages"]?[0]?["content"]?.ToString());
        Assert.AreEqual("read_file", sanitized["tools"]?[0]?["function"]?["name"]?.ToString());
    }

    private static string CreateToolCallResponse(string id, string name)
    {
        var firstDelta = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] = id,
                                ["type"] = "function",
                                ["function"] = new JsonObject { ["name"] = name, ["arguments"] = "{\"path\":" }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        var secondDelta = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["function"] = new JsonObject { ["arguments"] = "\"file.cs\"}" }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        return CreateSse(
            firstDelta.ToJsonString(),
            secondDelta.ToJsonString(),
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");
    }

    private static LoopDetectionConfig? _savedLoopDetectionConfig;

    private static void SaveAndDisableLoopDetection()
    {
        _savedLoopDetectionConfig = LlamaOllamaBridge.LoopDetectionConfig;
        LlamaOllamaBridge.LoopDetectionConfig = new LoopDetectionConfig { Enabled = false };
    }

    private static void RestoreLoopDetection()
    {
        LlamaOllamaBridge.LoopDetectionConfig = _savedLoopDetectionConfig ?? new LoopDetectionConfig { Enabled = false };
    }

    private static async Task<(string Output, LlamaStreamTransformer.OpenAiStreamTransformResult Result)> TransformAsync(string stream, bool getGenerationStatsText = false, LoopDetectionConfig? loopDetectionConfig = null)
    {
        SaveAndDisableLoopDetection();
        try
        {
            if (loopDetectionConfig is not null)
            {
                LlamaOllamaBridge.LoopDetectionConfig = loopDetectionConfig;
            }

            using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
            using var output = new MemoryStream();
            LlamaStreamTransformer.OpenAiStreamTransformResult result =
                await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(input, output, ModelName, getGenerationStatsText);
            return (Encoding.UTF8.GetString(output.ToArray()), result);
        }
        finally
        {
            RestoreLoopDetection();
        }
    }

    private static string CreateSse(params string[] dataFrames) =>
        string.Join("\r\n\r\n", dataFrames.Select(frame => frame == "[DONE]" ? "data: [DONE]" : "data: " + frame)) + "\r\n\r\n";

    private static IEnumerable<JsonObject> ParseChunks(string output) => output
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.StartsWith("data: ", StringComparison.Ordinal) && line != "data: [DONE]")
        .Select(line => JsonNode.Parse(line["data: ".Length..])!.AsObject());

    private sealed class DisconnectingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new IOException("Client disconnected.");
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(new IOException("Client disconnected."));
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Client disconnected.");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(new IOException("Client disconnected."));
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException("Client disconnected."));
    }
}
