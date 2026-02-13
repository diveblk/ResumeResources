using Newtonsoft.Json;
using NodeNetworkApp.ViewModels.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeShortsClass.Tables;
using static YoutubeShortsApp.VideoComposer;
using static YoutubeShortsClass.ChatGptReddit;
using YoutubeShortsApp.Tables;
using static YoutubeShortsClass.ChatGptClient;

namespace YoutubeShortsClass
{
    public class LLMClient
    {
        private static readonly AppSettingsRepository _appSettingsRepository;

        static LLMClient()
        {
            _appSettingsRepository = new AppSettingsRepository();
        }

        public enum LLMOptions
        {
            ChatGPT,
        }

        public enum ChatGPTOptions
        {
            [Description("o4-mini")]
            O4Mini,

            [Description("gpt-4")]
            GPT4,

            [Description("gpt-4o")]
            GPT4o,

            [Description("gpt-3.5-turbo")]
            GPT3_5
        }

        public enum OutputTypes
        {
            [Description("Text")]
            Text,
            [Description("Text List")]
            ListText,
            [Description("Timestamped Video Keywords")]
            TimestampedVideoKeyword,
            [Description("SubRip Subtitle (SRT)")]
            SRT,
            [Description("Amazon Polly SSML")]
            AmazonPollySSML,
            [Description("Reaction Text")]
            ReactionText,
            [Description("Reaction Text With Breaks")]
            ReactionTextBreaks,
            [Description("Reaction Text With Breaks and News")]
            ReactionTextBreaksNews,
        }

        public enum VoiceType
        {
            alloy,
            echo,
            fable,
            onyx,
            nova,
            shimmer
        }

        //public static async Task<string> GetResponseFromLLM(string Prompt, LLMOptions LLMOption, ChatGPTOptions ChatGPTOption, OutputTypes OutputType)
        //{
        //    string LLMRetuenText  = string.Empty;

        //    switch (LLMOption)
        //    {
        //        case LLMOptions.ChatGPT:
        //            return await HandleChatGPT(Prompt, ChatGPTOption);

        //        default:
        //            throw new NotSupportedException($"LLMOption '{LLMOption}' is not supported.");
        //    }
        //}

        public static async Task<List<object>> GetResponseFromLLM(
            string Prompt,
            LLMOptions LLMOption,
            ChatGPTOptions ChatGPTOption,
            OutputTypes OutputType,
            PlanRunHistoryRecord PlanRunHistoryRecord = null)
        {
            string llmReturnText;
            ModelGenerationRecord modelInfo = new ModelGenerationRecord()
            {
                Guid = Guid.NewGuid().ToString(),
                PromptActual = Prompt,
                ExecutionTime = DateTime.Now
            };

            if (PlanRunHistoryRecord != null)
            {
                modelInfo.PlanRunHistoryRecordGuid = PlanRunHistoryRecord.Guid;
            }

            // Add output format instructions to the prompt based on OutputType
            switch (OutputType)
            {
                case OutputTypes.Text:
                    // No special format instruction needed for plain text
                    break;

                case OutputTypes.ListText:
                    Prompt += "\n- Output ONLY the list, one item per line, no numbering, no bullet points, no extra text.";
                    break;

                case OutputTypes.TimestampedVideoKeyword:
                    Prompt +=
                        "\n- Output ONLY as a JSON array of objects, where each object has:" +
                        "\n  - \"Start\": the start time as a string (e.g., \"00:00:01.000\")" +
                        "\n  - \"End\": the end time as a string (e.g., \"00:00:05.000\")" +
                        "\n  - \"Keyword\": the keyword or short description as a string" +
                        "\nNo extra explanation. No bullet points. No numbering. Output valid JSON only.";
                    break;
                case OutputTypes.AmazonPollySSML:
                    Prompt +=
                        "\n- Output must be pure Amazon Polly SSML XML format (inside <speak> tags)." +
                        "\n  - No explanations or extra commentary — output only the raw XML.";
                    break;
                case OutputTypes.ReactionText:
                    Prompt +=
                        "\n  - You are able to MuteOriginalAudio as you think is best" +
                        "\n  - Output ONLY as a JSON array of objects, where each object has:" +
                        "\n  - \"Timestamp\": the start time of the reaction (e.g., \"00:00:14.500\")" +
                        "\n  - \"Duration\": how long the reaction should last (e.g., \"00:00:03.000\")" +
                        "\n  - \"ReactionText\": what the person says or expresses as a reaction" +
                        "\n  - \"MuteOriginalAudio\": true or false, whether to mute the original audio during the reaction" +
                        "\n  - \"PauseVideo\": false, don't allow breaks" +
                        "\n  - \"Emotion\": one of the following values: Neutral, Happy, Sad, Angry, Shocked, Laughing, Confused, Excited, Bored" +
                        "\n  - \"ReactionType\": one of the following: Verbal, Laugh, Expression, Gasp, Sigh, FacePalm, Silent" +
                        "\n  - \"NewsLookupTopics\": return empty string" +
                        "\nNo explanations, no extra text, no bullet points. Output valid JSON only.";
                    break;
                case OutputTypes.ReactionTextBreaks:
                    Prompt +=
                        "\n  - You are able to MuteOriginalAudio and PauseVideo as you think is best" +
                        "\n- Output ONLY as a JSON array of objects, where each object has:" +
                        "\n  - \"Timestamp\": the start time of the reaction (e.g., \"00:00:14.500\")" +
                        "\n  - \"Duration\": how long the reaction should last (e.g., \"00:00:03.000\")" +
                        "\n  - \"ReactionText\": what the person says or expresses as a reaction" +
                        "\n  - \"MuteOriginalAudio\": true or false, whether to mute the original audio during the reaction" +
                        "\n  - \"PauseVideo\": true or false, whether to pause the video during the reaction" +
                        "\n  - \"Emotion\": one of the following values: Neutral, Happy, Sad, Angry, Shocked, Laughing, Confused, Excited, Bored" +
                        "\n  - \"ReactionType\": one of the following: Verbal, Laugh, Expression, Gasp, Sigh, FacePalm, Silent" +
                        "\n  - \"NewsLookupTopics\": return empty string" +
                        "\nNo explanations, no extra text, no bullet points. Output valid JSON only.";
                    break;
                case OutputTypes.ReactionTextBreaksNews:
                    Prompt +=
                        "\n  - You are able to MuteOriginalAudio and PauseVideo as you think is best, you can also look up NewsLookupTopics" +
                        "\n- Output ONLY as a JSON array of objects, where each object has:" +
                        "\n  - \"Timestamp\": the start time of the reaction (e.g., \"00:00:14.500\")" +
                        "\n  - \"Duration\": how long the reaction should last (e.g., \"00:00:03.000\")" +
                        "\n  - \"ReactionText\": what the person says or expresses as a reaction" +
                        "\n  - \"MuteOriginalAudio\": true or false, whether to mute the original audio during the reaction" +
                        "\n  - \"PauseVideo\": true or false, whether to pause the video during the reaction" +
                        "\n  - \"Emotion\": one of the following values: Neutral, Happy, Sad, Angry, Shocked, Laughing, Confused, Excited, Bored" +
                        "\n  - \"ReactionType\": one of the following: Verbal, Laugh, Expression, Gasp, Sigh, FacePalm, Silent" +
                        "\n  - \"NewsLookupTopics\": ability to look up news articles, return search terms to look up as a string, must have PauseVideo = true, ReactionText is not used in this case" +
                        "\nNo explanations, no extra text, no bullet points. Output valid JSON only.";
                    break;
                case OutputTypes.SRT:
                    Prompt += "\n Generate subtitles in valid SRT format. Follow these strict rules:" +
                        "\n - Use sequential numbering (1, 2, 3…)" +
                        "\n - Each block must contain:" +
                        "\n   - A start and end timestamp in `HH:MM:SS,mmm --> HH:MM:SS,mmm` format" +
                        "\n   - Subtitle text (can be 1–2 short lines per block)" +
                        "\n - Use realistic speaking timing: average speech rate is ~13 characters per second." +
                        "\n - Maximum 2 lines of text per subtitle." +
                        "\n - Do not include any metadata, explanations, or formatting outside of standard SRT." +
                        "\n - Ensure spacing between entries and no overlapping timestamps.";
                    break;

                default:
                    throw new NotSupportedException($"OutputType '{OutputType}' is not supported.");
            }

            // Get the LLM response
            switch (LLMOption)
            {
                case LLMOptions.ChatGPT:
                    modelInfo.ModelType = ModelInfoRecord.ModelTypes.ChatGPT;

                    switch (ChatGPTOption)
                    {
                        case ChatGPTOptions.O4Mini:
                            modelInfo.ModelName = "o4-mini";
                            modelInfo.ModelOption = ModelInfoRecord.ModelOptions.Reasoning;
                            break;
                        case ChatGPTOptions.GPT4:
                            modelInfo.ModelName = "gpt-4";
                            break;
                        case ChatGPTOptions.GPT4o:
                            modelInfo.ModelName = "gpt-4o";
                            break;
                        case ChatGPTOptions.GPT3_5:
                            modelInfo.ModelName = "gpt-3.5-turbo";
                            break;
                        default:
                            throw new NotSupportedException($"ChatGPTOption '{ChatGPTOption}' is not supported.");
                    }

                    ReasoningResult results = await HandleChatGPT(Prompt, ChatGPTOption);

                    llmReturnText = results.Response;

                    modelInfo.CompletionTokens = results.CompletionTokens;
                    modelInfo.PromptTokens = results.PromptTokens;
                    modelInfo.TotalTokens = results.TotalTokens;

                    modelInfo.Completion = llmReturnText?.Trim() ?? "";

                    break;
                default:
                    throw new NotSupportedException($"LLMOption '{LLMOption}' is not supported.");
            }

            modelInfo.Insert();

            // Convert the response based on OutputType
            switch (OutputType)
            {
                case OutputTypes.Text:
                    // Return a single-item list containing the string
                    return new List<object> { llmReturnText };

                case OutputTypes.AmazonPollySSML:
                    return new List<object> { llmReturnText };

                case OutputTypes.ListText:
                    // Try to parse as JSON array, fallback to splitting by newlines
                    try
                    {
                        llmReturnText = llmReturnText.Replace("```json", "").Replace("```", "");
                        var list = JsonConvert.DeserializeObject<List<string>>(llmReturnText);
                        if (list != null)
                            return list.Cast<object>().ToList();
                    }
                    catch { }
                    // Fallback: split by newlines
                    return llmReturnText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Cast<object>()
                        .ToList();

                case OutputTypes.TimestampedVideoKeyword:
                    // Try to parse as JSON array of objects
                    try
                    {
                        llmReturnText = llmReturnText.Replace("```json", "").Replace("```", "");
                        var list = JsonConvert.DeserializeObject<List<TimestampedVideoKeyword>>(llmReturnText);
                        if (list != null && list.All(x => x != null))
                            return list.Cast<object>().ToList();
                    }
                    catch { }

                    // Try to parse as JSON array of objects with string properties, then map to TimestampedVideoKeyword
                    try
                    {
                        llmReturnText = llmReturnText.Replace("```json", "").Replace("```", "");
                        var tempList = JsonConvert.DeserializeObject<List<TempTimestampedVideoKeyword>>(llmReturnText);
                        if (tempList != null)
                        {
                            var mapped = tempList
                                .Select(x => new TimestampedVideoKeyword
                                {
                                    Start = TimeSpan.TryParse(x.Start, out var s) ? s : TimeSpan.Zero,
                                    End = TimeSpan.TryParse(x.End, out var e) ? e : TimeSpan.Zero,
                                    Keyword = x.Keyword ?? ""
                                })
                                .Cast<object>()
                                .ToList();
                            return mapped;
                        }
                    }
                    catch { }

                    // Fallback: parse from plain text (legacy format)
                    return ParseTimestampedVideoKeywordsFromText(llmReturnText)
                        .Cast<object>()
                        .ToList();

                case OutputTypes.ReactionTextBreaks:
                case OutputTypes.ReactionTextBreaksNews:
                case OutputTypes.ReactionText:
                    // Try direct deserialization
                    try
                    {
                        llmReturnText = llmReturnText.Replace("```json", "").Replace("```", "");
                        var list = JsonConvert.DeserializeObject<List<ReactionCue>>(llmReturnText);
                        if (list != null && list.All(x => x != null))
                        {
                            list.ForEach(x => x.TimestampOriginal = x.Timestamp); // Copy Timestamp to TimestampOriginal
                            return list.Cast<object>().ToList();
                        }
                    }
                    catch { }

                    // Try temp deserialization and manual mapping
                    try
                    {
                        llmReturnText = llmReturnText.Replace("```json", "").Replace("```", "");
                        var tempList = JsonConvert.DeserializeObject<List<TempReactionCue>>(llmReturnText);
                        if (tempList != null)
                        {
                            var mapped = tempList
                                .Select(x => new ReactionCue
                                {
                                    Timestamp = TimeSpan.TryParse(x.Timestamp, out var ts) ? ts : TimeSpan.Zero,
                                    TimestampOriginal = TimeSpan.TryParse(x.Timestamp, out var ts2) ? ts2 : TimeSpan.Zero,
                                    Duration = TimeSpan.TryParse(x.Duration, out var dur) ? dur : TimeSpan.Zero,
                                    ReactionText = x.ReactionText ?? "",
                                    MuteOriginalAudio = x.MuteOriginalAudio,
                                    PauseVideo = x.PauseVideo,
                                    Emotion = Enum.TryParse<EmotionType>(x.Emotion, true, out var em) ? em : EmotionType.Neutral,
                                    ReactionType = Enum.TryParse<ReactionCategory>(x.ReactionType, true, out var rc) ? rc : ReactionCategory.Verbal,
                                    //NewsLookupTopics = x.NewsLookupTopics ?? "",
                                })
                                .Cast<object>()
                                .ToList();
                            return mapped;
                        }
                    }
                    catch { }

                    // Fallback: parse from plain text (legacy format)
                    return ParseTimestampedVideoKeywordsFromText(llmReturnText)
                        .Cast<object>()
                        .ToList();

                case OutputTypes.SRT:
                    try
                    {
                        var parsedEntries = ParseSrtToSubtitleEntries(llmReturnText);
                        return parsedEntries.Cast<object>().ToList();
                    }
                    catch
                    {
                        return new List<object>(); // or throw if you prefer
                    }

                default:
                    throw new NotSupportedException($"OutputType '{OutputType}' is not supported.");
            }
        }


        private static List<TimestampedVideoKeyword> ParseTimestampedVideoKeywordsFromText(string text)
        {
            var results = new List<TimestampedVideoKeyword>();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(';', 2);
                if (parts.Length == 2)
                {
                    var times = parts[0].Split("-->");
                    if (times.Length == 2 &&
                        TimeSpan.TryParse(times[0].Trim(), out var start) &&
                        TimeSpan.TryParse(times[1].Trim(), out var end))
                    {
                        var keyword = parts[1].Trim();
                        results.Add(new TimestampedVideoKeyword
                        {
                            Start = start,
                            End = end,
                            Keyword = keyword
                        });
                    }
                }
            }
            return results;
        }

        private static async Task<ReasoningResult> HandleChatGPT(string Prompt, ChatGPTOptions ChatGPTOption)
        {
            string model = EnumHelper.GetDescription(ChatGPTOption); // Get the description of the ChatGPTOption

            // Handle different cases for ChatGPTOptions
            return ChatGPTOption switch
            {
                ChatGPTOptions.O4Mini => await ProcessWithO4Mini(Prompt, model),
                ChatGPTOptions.GPT4 => await ProcessWithGPT4(Prompt, model),
                ChatGPTOptions.GPT4o => await ProcessWithGPT4o(Prompt, model),
                ChatGPTOptions.GPT3_5 => await ProcessWithGPT3_5(Prompt, model),
                _ => throw new NotSupportedException($"ChatGPTOption '{ChatGPTOption}' is not supported.")
            };
        }
        private static async Task<ReasoningResult> ProcessWithO4Mini(string Prompt, string model)
        {
            // Simulate processing with the "gpt-o4-mini" model
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            // Initialize the ChatGptClient with the API key
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);

            // Call the GetChatCompletionAsync method to process the prompt
            ReasoningResult response = await chatGptClient.GetReasoningResponseAsync(Prompt, model);

            // Return the response
            return response;
        }

        private static async Task<ReasoningResult> ProcessWithGPT4(string Prompt, string model)
        {
            // Simulate processing with the "gpt-4" model
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);

            // Call the GetChatCompletionAsync method to process the prompt
            ReasoningResult response = await chatGptClient.GetChatCompletionAsync(Prompt, model);

            // Return the response
            return response;
        }

        private static async Task<ReasoningResult> ProcessWithGPT4o(string Prompt, string model)
        {
            // Simulate processing with the "gpt-4o" model
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            // Initialize the ChatGptClient with the API key
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);

            // Call the GetChatCompletionAsync method to process the prompt
            ReasoningResult response = await chatGptClient.GetChatCompletionAsync(Prompt, model);

            // Return the response
            return response;
        }

        private static async Task<ReasoningResult> ProcessWithGPT3_5(string Prompt, string model)
        {
            // Simulate processing with the "gpt-3.5-turbo" model
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);

            // Call the GetChatCompletionAsync method to process the prompt
            ReasoningResult response = await chatGptClient.GetChatCompletionAsync(Prompt, model);

            // Return the response
            return response;
        }

        public static async Task ProcessWithChatGPTVoice(string text, string MP3FilePath, string voice = "nova", string model = "tts-1")
        {
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");

            if(text == "")
            {
                return;
            }

            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);
            byte[] mp3Bytes = await chatGptClient.GetSpeechAsync(text, voice, model);

            // Save to file
            await File.WriteAllBytesAsync(MP3FilePath, mp3Bytes);
        }

        public static async Task ProcessWithChatGPTWisper(string MP3FilePath, string SRTFilePath)
        {
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");
            }
            ChatGptClient chatGptClient = new ChatGptClient(openAiApiKey);
            string transcript = await chatGptClient.TranscribeAudioAsync(MP3FilePath, "srt");
            File.WriteAllText(SRTFilePath, transcript);
        }

        public static async Task<List<string>> ProcessWithChatGPTImage(
                string prompt,
                string outputDirectory,
                int numberOfImages = 1,
                ImageSize imageSize = ImageSize.Square)
        {
            // 1) Get your key
            var openAiApiKey = _appSettingsRepository.GetSetting("OpenAiApiKey");

            if (string.IsNullOrWhiteSpace(openAiApiKey))
                throw new InvalidOperationException("OpenAI API Key is not configured in settings.");

            // 2) Generate the image URLs
            var chatGptClient = new ChatGptClient(openAiApiKey);
            string[] urls = await chatGptClient.GenerateImageAsync(
                prompt: prompt,
                n: numberOfImages,
                size: imageSize
            );

            // 3) Make sure our output folder exists
            var savedPaths = new List<string>(urls.Length);
            using var http = new HttpClient();

            // 4) Download & save each image
            for (int i = 0; i < urls.Length; i++)
            {
                var url = urls[i];
                byte[] imageBytes = await http.GetByteArrayAsync(url);

                // Name them image_1.png, image_2.png, etc.
                string fileName = $"image_{i + 1}.png";
                string fullPath = Path.Combine(outputDirectory, fileName);

                await File.WriteAllBytesAsync(fullPath, imageBytes);
                savedPaths.Add(fullPath);
            }

            // 5) Return the list of saved file paths
            return savedPaths;
        }

        public static string ReactionCueToSrt(List<ReactionCue> cues)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                string start = FormatTimeSpan(cue.Timestamp);
                string end = FormatTimeSpan(cue.Timestamp + cue.Duration);

                sb.AppendLine((i + 1).ToString());
                sb.AppendLine($"{start} --> {end}");
                sb.AppendLine(cue.ReactionText);
                sb.AppendLine(); // Empty line between entries
            }

            return sb.ToString();
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            return string.Format("{0:00}:{1:00}:{2:00},{3:000}",
                (int)ts.TotalHours,
                ts.Minutes,
                ts.Seconds,
                ts.Milliseconds);
        }

        private static List<SubtitleEntry> ParseSrtToSubtitleEntries(string srtText)
        {
            var entries = new List<SubtitleEntry>();
            var blocks = Regex.Split(srtText.Trim(), @"\r?\n\r?\n");

            foreach (var block in blocks)
            {
                var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3) continue;

                var timeMatch = Regex.Match(lines[1], @"(?<start>\d{2}:\d{2}:\d{2},\d{3}) --> (?<end>\d{2}:\d{2}:\d{2},\d{3})");
                if (!timeMatch.Success) continue;

                var start = TimeSpan.ParseExact(timeMatch.Groups["start"].Value.Replace(',', '.'), @"hh\:mm\:ss\.fff", null);
                var end = TimeSpan.ParseExact(timeMatch.Groups["end"].Value.Replace(',', '.'), @"hh\:mm\:ss\.fff", null);
                var text = string.Join(" ", lines.Skip(2)).Trim();

                entries.Add(new SubtitleEntry
                {
                    Start = start,
                    End = end,
                    Text = text
                });
            }

            return entries;
        }


        private class TempReactionCue
        {
            public string Timestamp { get; set; }
            public string Duration { get; set; }
            public string ReactionText { get; set; }
            public bool MuteOriginalAudio { get; set; }
            public bool PauseVideo { get; set; }
            public string Emotion { get; set; }
            public string ReactionType { get; set; }
            public string NewsLookupTopics { get; set; }
        }

        public class ReactionCue
        {
            public TimeSpan Timestamp { get; set; }
            public TimeSpan TimestampOriginal { get; set; }
            public TimeSpan Duration { get; set; }
            public string ReactionText { get; set; }
            public bool MuteOriginalAudio { get; set; }
            public bool PauseVideo { get; set; }
            public EmotionType Emotion { get; set; }
            public ReactionCategory ReactionType { get; set; }
            //public string NewsLookupTopics { get; set; }
        }

        public enum EmotionType
        {
            Neutral,
            Happy,
            Sad,
            Angry,
            Shocked,
            Laughing,
            Confused,
            Excited,
            Bored
        }

        public enum ReactionCategory
        {
            Verbal,
            Laugh,
            Expression,
            Gasp,
            Sigh,
            FacePalm,
            Silent
        }

        private class TempTimestampedVideoKeyword
        {
            public string? Start { get; set; }
            public string? End { get; set; }
            public string? Keyword { get; set; }
        }
    }
}
