using Godot;
using System;
using LLama;
using LLama.Common;
using LLama.Sampling;
using System.Text;
using System.Collections.Generic;
using LLama.Native;

namespace RPG.AI;

public partial class AiTest : Node3D
{

    public override async void _Ready()
    {
        NativeLibraryConfig.All.WithLogCallback(delegate (LLamaLogLevel level, string message) { GD.Print($"{level}: {message}"); });

        // Path to the GGUF model included in the project root.
        // Using an absolute Windows path for the gguf file in this repository.
        string modelPath = @"c:\Git\rpg\gemma-2-2b-it-Q4_K_M.gguf";

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024, // The longest length of chat as memory.
            GpuLayerCount = 26 // How many layers to offload to GPU. Please adjust it according to your GPU memory.
        };
        using var model = await LLamaWeights.LoadFromFileAsync(parameters);
        using var context = model.CreateContext(parameters);
        var executor = new InteractiveExecutor(context);

        // Previously this file started an interactive chat session. For this
        // task we only generate a single JSON object, so the interactive
        // session is not needed.

        InferenceParams inferenceParams = new InferenceParams()
        {
            MaxTokens = 256, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = new List<string> { "User:" }, // Stop generation once antiprompts appear.

            SamplingPipeline = new DefaultSamplingPipeline(),
        };

        // Instead of free chat, ask the model to generate a typical MMO weapon
        // in strict JSON format. We'll supply a short schema in the system prompt
        // and then request a single JSON object describing a weapon.

        // JSON schema (informal) the model should follow:
        // {
        //   "name": string,
        //   "type": string, // e.g., "sword", "bow", "staff"
        //   "tier": string, // e.g., "common", "rare", "epic", "legendary"
        //   "attack": { "min": int, "max": int },
        //   "speed": float, // attacks per second
        //   "attributes": { "strength": int, "agility": int, "intelligence": int },
        //   "special": string | null
        // }

        // Build the prompt to instruct the assistant strictly to output only JSON.
        string systemPrompt = "You are an assistant that outputs exactly one JSON object and nothing else. " +
                              "The object must follow this schema: name (string), type (string), tier (string), " +
                              "attack (object with min and max ints), speed (float), attributes (object with strength, agility, intelligence ints), special (string or null). " +
                              "Do not include any explanatory text, code fences, or extra fields.";

        string userPrompt = "Generate a typical MMO weapon as a single JSON object following the schema. Make values sensible and balanced for a game.";

        // Prepare the chat history with system instruction only; the user message
        // will be supplied when starting the chat to avoid adding two consecutive
        // user messages (the API enforces alternating roles).
        var jsonChatHistory = new ChatHistory();
        jsonChatHistory.AddMessage(AuthorRole.System, systemPrompt);

        ChatSession jsonSession = new(executor, jsonChatHistory);

        GD.Print("Generating MMO weapon JSON...");

        // Collect the generated text (wrap in try/catch to log any API errors)
        var sb = new StringBuilder();
        try
        {
            await foreach (var text in jsonSession.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, userPrompt),
                inferenceParams))
            {
                GD.Print(text);
                sb.Append(text);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n\nError during chat generation: {ex.GetType().Name}: {ex.Message}");
            // Optionally return or handle fallback here; for now we stop further processing.
            return;
        }
        string generated = sb.ToString();

        // Try to do a lightweight JSON validation using System.Text.Json
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(generated);
            var root = doc.RootElement;
            GD.Print("\n\nValid JSON received. Root element kind: " + root.ValueKind);
        }
        catch (System.Text.Json.JsonException)
        {
            GD.PrintErr("\n\nWarning: generated output is not valid JSON. Check model output or prompts.");
        }
    }
}

