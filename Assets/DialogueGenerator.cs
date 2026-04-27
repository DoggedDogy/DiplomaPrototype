using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

public static class DialogueGenerator
{
    private static string openRouterApiUrl = "https://api.groq.com/openai/v1/responses"; //"https://openrouter.ai/api/v1/chat/completions";

    private static string apiKey;
        
    static DialogueGenerator()
    {
        string json = File.ReadAllText("Assets/Config/secrets.json");
        Secrets secrets = JsonUtility.FromJson<Secrets>(json);
        apiKey = secrets.GROQ_API_KEY;
    }

    private static readonly PlayerAction[] memoryActions = new[]
    {
        PlayerAction.Calm,
        PlayerAction.Threaten,
        PlayerAction.Lie,
        PlayerAction.Persuade,
        PlayerAction.Ask,
        PlayerAction.Observe
    };

    public class DialogueResponse
    {
        public string npcReply; // short reply (1-2 sentences)

        public List<FollowUp> followUps = new List<FollowUp>();

        // Optional: model can suggest whether to add a memory for the player's action
        public bool suggestAddMemory = false;
        public string note = ""; // any short reason text (optional)
    }

    public class FollowUp
    {
        public PlayerAction action;
        public bool applicable;
        public string actionText; // short text to present as follow-up / UI button tooltip
        public string suggestedMemoryTag; // optional short tag for the memory if this action is performed
    }

    public static async Task<DialogueResponse> GenerateNPCDialogue(NPCInteractionReceiver npc, PlayerAction action, ScenePremise premise, string npcId, bool isFirstInteraction, int recentMemoryCount = 6)
    {
        string prompt = BuildPrompt(npc, action, premise, recentMemoryCount, npcId, isFirstInteraction);
        Debug.Log(prompt);
        var requestBody = new
        {
            model = "openai/gpt-oss-120b",   // Choose your Groq model
            input = prompt,                  // The player action prompt
            instructions = "You are an NPC in a detective game scene. Answer concisely in JSON.", // optional
            max_output_tokens = 2000,
            temperature = 0.6
        };
        // openrouter
        //var requestBody = new
        //{
        //    model = "openai/gpt-3.5-turbo",
        //    messages = new object[]
        //    {
        //        new { role = "system", content = "You are an NPC in a tightly-constrained detective game scene. Answer concisely and follow the JSON output schema described." },
        //        new { role = "user", content = prompt }
        //    },
        //    max_tokens = 350,
        //    temperature = 0.6f
        //};

        string jsonPayload = JsonConvert.SerializeObject(requestBody);

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API key not found in environment variables!");
        }

        using (UnityWebRequest request = new UnityWebRequest(openRouterApiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"OpenRouter error: {request.downloadHandler.text}");
                // return a safe fallback DialogueResponse
                return new DialogueResponse
                {
                    npcReply = "…",
                    followUps = memoryActions.Select(a => new FollowUp { action = a, applicable = false, actionText = "" }).ToList()
                };
            }

            var jsonResponse = request.downloadHandler.text;

            // Extract assistant content out of chat-completion wrapper if present
            // The OpenRouter response typically mirrors OpenAI: choices[0].message.content
            string assistantContent = TryExtractAssistantContent(jsonResponse);

            if (string.IsNullOrEmpty(assistantContent))
            {
                Debug.LogWarning("Empty assistant content; returning fallback.");
                return new DialogueResponse
                {
                    npcReply = "...",
                    followUps = memoryActions.Select(a => new FollowUp { action = a, applicable = false, actionText = "" }).ToList()
                };
            }

            // The assistant was instructed to return strict JSON — parse it.
            try
            {
                Debug.Log(assistantContent);
                var parsed = JsonConvert.DeserializeObject<DialogueResponse>(assistantContent);
                // Add sanity defaults for any missing followups
                foreach (var act in memoryActions)
                {
                    if (!parsed.followUps.Any(f => f.action == act))
                    {
                        parsed.followUps.Add(new FollowUp { action = act, applicable = false, actionText = "" });
                    }
                }

                return parsed;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to parse assistant JSON:\n{ex}\nRaw content:\n{assistantContent}");
                // As a fallback, return simple reply
                return new DialogueResponse
                {
                    npcReply = assistantContent.Truncate(250),
                    followUps = memoryActions.Select(a => new FollowUp { action = a, applicable = false, actionText = "" }).ToList()
                };
            }
        }
    }

    private static string TryExtractAssistantContent(string jsonResponse)
    {
        try
        {
            var wrapper = JsonConvert.DeserializeObject<GroqResponse>(jsonResponse); //ChatCompletionResponse
            if (wrapper?.output != null && wrapper.output.Length > 0)
            {
                foreach (var o in wrapper.output)
                {
                    if (o.type == "message" && o.content != null)
                    {
                        foreach (var c in o.content)
                        {
                            if (c.type == "output_text")
                                return c.text?.Trim();
                        }
                    }
                }
            }
            //if (wrapper?.choices != null && wrapper.choices.Length > 0 && wrapper.choices[0].message != null)
            //    return wrapper.choices[0].message.content?.Trim();
        }
        catch
        {
            // Not the standard wrapper; maybe assistant returned raw JSON already
        }

        // last resort: return the raw response (caller will try parsing)
        return jsonResponse.Trim();
    }

    private static string BuildPrompt(NPCInteractionReceiver npc, PlayerAction action, ScenePremise premise, int recentMemoryCount, string npcId, bool isFirstInteraction)
    {
        // Gather recent memory events from this NPC's memory component (if present)
        var memList = !isFirstInteraction ? npc.memory.GetRecentEvents(recentMemoryCount) : new List<MemoryEvent>();

        var sb = new StringBuilder();

        // Scene premise — keep it short and authoritative
        sb.AppendLine("==== SCENE PREMISE (STRICT FACTS) ====");
        if (premise != null)
        {
            sb.AppendLine($"Location: {premise.locationName}");
            sb.AppendLine($"Summary: {premise.sceneSummary}");
            sb.AppendLine("Your npc:");
            foreach (var p in premise.people)
            {
                if (p.id == npc.npcId)
                    sb.AppendLine($"- id: {p.id}; name: {p.displayName}; role: {p.role}; description: {p.shortDescription}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("==== PLAYER ACTION DEFINITIONS ====");
        sb.AppendLine("Calm: The player reassures or soothes the NPC.");
        sb.AppendLine("Threaten: The player intimidates or pressures the NPC.");
        sb.AppendLine("Lie: The player attempts to mislead or deceive the NPC.");
        sb.AppendLine("Persuade: The player reasons with the NPC to cooperate.");
        sb.AppendLine("Ask: The player asks a question.");
        sb.AppendLine("Observe: The player silently watches the NPC; NPC should describe their current state or behavior, not respond in dialogue.");
        sb.AppendLine();

        // NPC internal state (do not allow creation of new facts)
        sb.AppendLine("==== NPC STATE ====");
        sb.AppendLine($"npcId: {npcId}"); // add this!
        sb.AppendLine($"current intent toward investigation: {npc.decisionSystem.CurrentIntent}");
        sb.AppendLine($"stress (from 0 to 100): {npc.emotions.stress}");
        sb.AppendLine($"fear (from 0 to 100): {npc.emotions.fear}");
        sb.AppendLine($"trust (from -100 to 100): {npc.relationship.trust}");
        sb.AppendLine($"respect (from -100 to 100): {npc.relationship.respect}");
        sb.AppendLine();

        // Last player interactions (tag + fullText) - instruct that exact texts are authoritative
        sb.AppendLine("==== RECENT INTERACTIONS (most recent first) ====");
        if (memList.Count == 0)
            sb.AppendLine("(none)");
        else
        {
            foreach (var e in memList)
            {
                sb.AppendLine($"- action: {e.action}; tag: {e.contextTag}; text: \"{e.fullText}\"; perceived: {e.influence}; ageSec: {Time.time - e.time}");
            }
        }
        sb.AppendLine();

        // The current player action that triggered the request
        sb.AppendLine("==== CURRENT PLAYER ACTION ====");
        sb.AppendLine($"action: {action}");
        sb.AppendLine("NOTE: The CURRENT PLAYER ACTION corresponds to the most recent interaction listed under RECENT INTERACTIONS.");
        sb.AppendLine("Use the 'fullText' field from that interaction as the authoritative, detailed description of the action.");
        sb.AppendLine("Do not ignore any details from RECENT INTERACTIONS when generating your response or followUps.");
        sb.AppendLine();

        // Strict JSON schema instruction
        sb.AppendLine("==== INSTRUCTIONS ====");
        sb.AppendLine("You MUST NOT invent new characters, items, or physical evidence beyond the SCENE PREMISE above.");
        sb.AppendLine("Answer as the NPC would in a detective game scene, reacting naturally and concisely, 1-3 sentences per reply.");
        sb.AppendLine("IMPORTANT: When generating npcReply and followUps, prioritize information from the first RECENT INTERACTION for this action.");
        sb.AppendLine("Treat CURRENT PLAYER ACTION as a shorthand; the full authoritative description is in RECENT INTERACTIONS.");
        sb.AppendLine("Return ONLY valid JSON (no surrounding text) strictly following this schema:");
        sb.AppendLine(@"{
  ""npcReply"": ""short 1-3 sentence reply the NPC would say"",
  ""followUps"": [
    { ""action"": ""Calm"", ""applicable"": true|false, ""actionText"": ""concise text for the UI button, reflecting part of the NPC reply or context"", ""suggestedMemoryTag"": ""optional_tag"" },
    ...
  ],
  ""suggestAddMemory"": true|false,
  ""note"": ""optional short explanation for developer""
}");
        sb.AppendLine();

        if (isFirstInteraction)
        {
            sb.AppendLine("NOTE: !!! EXTRAMLY IMPORTANT !!!: This is the first interaction. React naturally in-character based on your current emotional state, trust, respect, fear, and memories. You must greet or any other way accnowledge the detective but can also show nervousness, suspicion, or other traits in this reply. Try not to reveal evidence or conclusions yet. You now that the pearson you is talking with is the detective.");

            sb.AppendLine("For every reply:");
            sb.AppendLine("- DO NOT mention objects, evidence, sounds, witnesses, or events unless the detective explicitly asks about them.");
            sb.AppendLine("- Focus ONLY on greeting + attitude(nervous, curious, guarded, annoyed, etc.).");
            sb.AppendLine("- You may acknowledge the investigation in general, but stay vague.");
            sb.AppendLine("- If you feel like revealing a detail, instead deflect or minimize it.");
            sb.AppendLine("- Ask options should focus on rapport, emotional state, or cooperation");
        }
        else
        {
            sb.AppendLine("FollowUps must not be generic. They must help the player actively investigate and be based upon given npctext");
            sb.AppendLine("Keep in mind what is your current nps saw, felt, and thought based on his description info");
            sb.AppendLine("what the NPC just implied, felt, avoided, or hinted at.");

            sb.AppendLine("For every reply:");
            sb.AppendLine("- Include at least 1 Ask option that digs deeper into something specific the NPC said.");
            sb.AppendLine("- Include at least 1 Ask or Observe that explores the NPC's emotional state or behavior");
            sb.AppendLine("- Include at least 1 option other than Ask or Observe that givves player an ability to affect npc");
            sb.AppendLine("- If the NPC mentions an object, sound, person, or place, include a follow-up that");
            sb.AppendLine("  targets that specific detail (e.g., 'Ask about the metallic sound near the fireplace').");
            sb.AppendLine("- Prefer follow-ups that clarify uncertainty, contradictions, or hidden motivation.");
        }
        sb.AppendLine("- Prefer follow-ups that clarify uncertainty, contradictions, or hidden motivation.");
        sb.AppendLine("- ACTIONS SHOULD NOT BE WORDED AS IF THEY AFFECT CHARACTERS NOT TAKING PART IN THIS DIALOG");
        sb.AppendLine("  (e.g., fear, hesitation, defensiveness, curiosity).");
        sb.AppendLine("Avoid generic labels like:");
        sb.AppendLine("- 'Ask a question'");
        sb.AppendLine("- 'Calm the NPC'");
        sb.AppendLine("- 'Persuade to cooperate'");
        sb.AppendLine();
        sb.AppendLine("Make every follow-up tied directly to the NPC’s last reply or visible behavior.");
        sb.AppendLine();
        sb.AppendLine("If the NPC appears nervous, guarded, angry, frightened, or evasive,");
        sb.AppendLine("followUps should include options that let the player explore WHY");
        sb.AppendLine("(e.g., fear of someone, hiding guilt, worried about consequences),");
        sb.AppendLine("without inventing new facts.");
        sb.AppendLine(); 
        sb.AppendLine("For Observe: describe NPC behavior or state from a side perspective, highlight key points with **double asterisks**, do not produce dialogue.");
        sb.AppendLine("For repeated actions, NPC should acknowledge verbally (e.g., 'As I said…').");
        sb.AppendLine("NPC must react according to personality, trust, respect, stress, fear, and memories.");
        sb.AppendLine("NPC must respond appropriately to the current player action:");
        sb.AppendLine("- Threaten: respond aggressively, defensively, or scared.");
        sb.AppendLine("- Calm: respond to reassurance or soothing.");
        sb.AppendLine("- Lie: respond to deception, possibly skeptical or cautious.");
        sb.AppendLine("- Persuade: respond to reasoning attempts, consider trust and respect.");
        sb.AppendLine("- Ask: answer questions with relevant, concise information.");
        sb.AppendLine("- Observe: describe behavior or emotional state without direct speech.");
        sb.AppendLine("Ensure replies feel varied and context-sensitive, not formulaic.");
        sb.AppendLine("NPC must act as if the incident is important to them and show interest in the investigation.");
        sb.AppendLine("if NPC is a culprit, it must try to hide icriminating information as much as possible unless it is to stressed, threatened they or cornered with questions");
        sb.AppendLine("NPC replies should reflect curiosity, concern, or personal perspective on the events, depending on their personality, stress, trust, respect, and fear.");
        sb.AppendLine("FollowUps should suggest actions that explore or question specific aspects of the scene, evidence, or other characters’ behavior.");
        sb.AppendLine("NPC should actively hint at or respond to clues in the room without revealing solutions directly, guiding the player to ask or observe further.");
        sb.AppendLine("If no followUps suggested, try to elaborate why inside npcReply from the postition of npc.");
        sb.AppendLine("Tone: engaged, curious, human-like, detective game style, 1-3 sentences per reply.");
        sb.AppendLine("Return ONLY valid JSON following the schema above, with realistic follow-up options reflecting specific details from the NPC's reply or the scene.");
        //        sb.AppendLine("You MUST NOT invent new characters, items, or physical evidence beyond the SCENE PREMISE above.");
        //        sb.AppendLine("Answer concisely as the NPC would in a detective game.");
        //        sb.AppendLine("Return ONLY valid JSON (no surrounding text) following this schema exactly:");
        //        sb.AppendLine(@"{
        //  ""npcReply"": ""short 1-2 sentence reply the NPC would say"",
        //  ""followUps"": [
        //    { ""action"": ""Calm"", ""applicable"": true|false, ""actionText"": ""short follow-up text for UI"", ""suggestedMemoryTag"": ""optional_tag"" },
        //    ...
        //  ],
        //  ""suggestAddMemory"": true|false,
        //  ""note"": ""optional short explanation for developer""
        //}");
        //        sb.AppendLine();
        //        sb.AppendLine("For followUps: include an entry for each of these actions (Calm, Threaten, Lie, Persuade, Ask, Observe).");
        //        sb.AppendLine("For 'applicable': true means that in the current NPC response context that player action should be presented/enabled in the UI.");
        //        sb.AppendLine("For 'actionText': provide a concise label/tooltip to show in the UI for what the player would say/do next.");
        //        sb.AppendLine("If the player repeated the same question previously, reflect that in the npcReply (e.g., 'As I said...').");
        //        sb.AppendLine("Be terse and avoid speculation. If unsure, set applicable=false and actionText empty.");
        //        sb.AppendLine("NPC must react based on its personality, trust, respect, stress, fear, and memories.");
        //        sb.AppendLine("NPC must react correctly to the current player action:");
        //        sb.AppendLine("NPC must react correctly to the current player action:");
        //        sb.AppendLine("- Threaten: respond aggressively, defensively, or scared.");
        //        sb.AppendLine("- Calm: respond to being reassured.");
        //        sb.AppendLine("- Lie: respond to deception.");
        //        sb.AppendLine("- Persuade: respond to persuasion attempts.");
        //        sb.AppendLine("- Ask: answer the question.");
        //        sb.AppendLine("- Observe: describe NPC behavior from the side with highlighting it in double **, speak less during this action.");
        //        sb.AppendLine("Acknowledge repeated actions verbally (e.g., 'As I said...').");
        //        sb.AppendLine("Tone: concise, natural, human-like, detective game style, 1-3 sentences per reply.");
        //        sb.AppendLine("Return ONLY valid JSON following this schema exactly: ...");
        //        sb.AppendLine();

        // Final single-line prompt
        sb.AppendLine("Now output the JSON only.");

        return sb.ToString();
    }

    // Classes to parse the chat wrapper (OpenRouter/OpenAI style)
    private class ChatCompletionResponse
    {
        public Choice[] choices;
    }
    private class Choice
    {
        public Message message;
    }
    private class Message
    {
        public string content;
    }

    // Classes to parse the chat wrapper (Groq style)
    private class GroqResponse
    {
        public GroqOutput[] output;
    }

    private class GroqOutput
    {
        public string type;
        public GroqContent[] content;
    }

    private class GroqContent
    {
        public string type;
        public string text;
    }
}

// small extension helper
public static class StringExtensions
{
    public static string Truncate(this string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max);
    }
}
