using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using DailyDash.Models;
using DailyDash.ViewModels;

namespace DailyDash.Services
{
    public static class MarkdownDataManager
    {
        private static readonly string BaseFolder    = @"C:\Users\willi\Documents\antigravity projects\daily dash 2\data";
        private static readonly string DiasFolder    = Path.Combine(BaseFolder, "dias");
        private static readonly string SemanasFolder = Path.Combine(BaseFolder, "semanas");
        private static readonly string MesesFolder   = Path.Combine(BaseFolder, "meses");

        public static string GetDailyFilePath(DateTime date)
        {
            return Path.Combine(DiasFolder, $"{date:yyyy-MM-dd}.md");
        }
        
        /// <summary>
        /// Gets the ISO 8601 week number for a given date.
        /// </summary>
        private static int GetIsoWeekNumber(DateTime date)
        {
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        /// <summary>
        /// File path per week: metas_semana_2026-W09.json
        /// </summary>
        public static string GetWeeklyFilePath(DateTime date)
        {
            int week = GetIsoWeekNumber(date);
            return Path.Combine(SemanasFolder, $"semana_{date:yyyy}-W{week:D2}.json");
        }

        /// <summary>
        /// File path per month: meses/mes_2026-02.json
        /// </summary>
        public static string GetMonthlyFilePath(DateTime date)
        {
            return Path.Combine(MesesFolder, $"mes_{date:yyyy-MM}.json");
        }

        public static async Task SaveDailyStateAsync(DateTime date, MainViewModel viewModel)
        {
            try
            {
                if (!Directory.Exists(DiasFolder)) Directory.CreateDirectory(DiasFolder);

                string filePath = GetDailyFilePath(date);
                // Use UTF8 with BOM for better compatibility with Windows/Visual Studio
                using StreamWriter sw = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

                await sw.WriteLineAsync("# Foco do Dia");
                await sw.WriteLineAsync(viewModel.DailyFocus);
                await sw.WriteLineAsync();

                await sw.WriteLineAsync("## Tarefas");
                foreach (var t in viewModel.Tasks)
                {
                    string check = t.IsCompleted ? "[x]" : "[ ]";
                    // Append Tag and TagColor if they exist
                    string tagInfo = string.Empty;
                    if (!string.IsNullOrWhiteSpace(t.Tag))
                    {
                        string safeColor = t.TagColor;
                        if (!safeColor.StartsWith("#") && safeColor != "Transparent") safeColor = $"#{safeColor}";
                        if (safeColor == "Transparent") safeColor = "#Transparent";
                        tagInfo = $" | {safeColor} #{t.Tag}";
                    }
                    string linkInfo = string.IsNullOrEmpty(t.LinkedGoalId) ? "" : $" <!-- Link:{t.LinkedGoalId} -->";

                    await sw.WriteLineAsync($"- {check} {t.Title}{tagInfo}{linkInfo}");
                    // Save description if present
                    if (!string.IsNullOrWhiteSpace(t.Description))
                    {
                        await sw.WriteLineAsync($"  > {t.Description}");
                    }
                }
                await sw.WriteLineAsync();

                await sw.WriteLineAsync("## Scratchpad");
                await sw.WriteLineAsync(viewModel.ScratchpadText);

                foreach (var t in viewModel.Tasks)
                {
                    if (t.NoteBlocks.Count > 0)
                    {
                        await sw.WriteLineAsync();
                        await sw.WriteLineAsync($"### {t.Title} Notas");
                        
                        foreach (var block in t.NoteBlocks)
                        {
                            if (block is TaskItem st)
                            {
                                string check = st.IsCompleted ? "[x]" : "[ ]";
                                // Ensure it starts with bullet point for parsing
                                await sw.WriteLineAsync($"- {check} {st.Title} | {st.CompletionPercent}%");
                            }
                            else if (block is TextBlockItem textBlock)
                            {
                                await sw.WriteLineAsync(textBlock.Text);
                            }
                        }
                    }
                }
            }
            catch { /* Ignore error on auto-save */ }
        }

        public static async Task<AppState?> LoadDailyStateAsync(DateTime date)
        {
            string filePath = GetDailyFilePath(date);
            if (!File.Exists(filePath)) return null;

            var state = new AppState();
            
            try
            {
                string[] lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);
                string currentSection = "";

                foreach (var rawLine in lines)
                {
                    string line = rawLine;
                    // Handle BOM if present on the first line
                    if (line.StartsWith("\uFEFF", StringComparison.Ordinal)) line = line.Substring(1);

                    string trimmed = line.Trim();
                    if (trimmed == "# Foco do Dia") { currentSection = "Focus"; continue; }
                    if (trimmed == "## Tarefas") { currentSection = "Tasks"; continue; }
                    if (trimmed == "## Scratchpad") { currentSection = "Scratchpad"; continue; }
                    
                    if (trimmed.StartsWith("### ") && trimmed.EndsWith(" Notas"))
                    {
                        string taskRef = trimmed.Substring(4, trimmed.Length - 10).Trim();
                        currentSection = "TaskNotes_" + taskRef;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line) && currentSection != "Scratchpad" && !currentSection.StartsWith("TaskNotes_")) continue;

                    switch (currentSection)
                    {
                        case "Focus":
                            state.DailyFocus = line;
                            break;
                        case "Tasks":
                            // Check for description line (starts with "  > ")
                            if (line.StartsWith("  > ") && state.Tasks.Count > 0)
                            {
                                var lastTask = state.Tasks[state.Tasks.Count - 1];
                                lastTask.Description = line.Substring(4).Trim();
                            }
                            else if (line.StartsWith("- [ ] ") || line.StartsWith("- [x] "))
                            {
                                bool isCompleted = line.StartsWith("- [x] ");
                                string rest = line.Substring(6).Trim();
                                
                                string title = rest;
                                string tag = string.Empty;
                                string tagColor = "Transparent";
                                string linkedGoalId = string.Empty;

                                // Parse <!-- Link:ID -->
                                var linkIdx = rest.IndexOf(" <!-- Link:");
                                if (linkIdx >= 0)
                                {
                                    var linkEnd = rest.IndexOf(" -->", linkIdx);
                                    if (linkEnd > linkIdx)
                                    {
                                        linkedGoalId = rest.Substring(linkIdx + 11, linkEnd - (linkIdx + 11));
                                        rest = rest.Remove(linkIdx, linkEnd + 4 - linkIdx).Trim();
                                    }
                                }

                                // Parse tag information: Title | #ColorHex #TagName
                                var pipeIdx = rest.LastIndexOf(" | ");
                                if (pipeIdx >= 0)
                                {
                                    title = rest.Substring(0, pipeIdx).Trim();
                                    string tagPart = rest.Substring(pipeIdx + 3).Trim();
                                    
                                    // Parse #ColorHex #TagName
                                    var parts = tagPart.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2 && parts[1].StartsWith("#"))
                                    {
                                        tagColor = parts[0];
                                        if (tagColor.StartsWith("##")) tagColor = tagColor.Substring(1);
                                        if (tagColor == "#Transparent") tagColor = "Transparent";
                                        tag = parts[1].Substring(1); // remove the #
                                    }
                                }
                                else
                                {
                                    title = rest;
                                }

                                state.Tasks.Add(new TaskItem(title) { IsCompleted = isCompleted, Tag = tag, TagColor = tagColor, LinkedGoalId = linkedGoalId });
                            }
                            break;
                        case "Scratchpad":
                            state.ScratchpadText += line + Environment.NewLine;
                            break;
                        default:
                            if (currentSection.StartsWith("TaskNotes_"))
                            {
                                string taskTitle = currentSection.Substring(10);
                                var task = state.Tasks.LastOrDefault(t => t.Title == taskTitle);
                                if (task != null)
                                {
                                    if (line.TrimStart().StartsWith("- [") && line.Contains("] "))
                                    {
                                        var trimmedLine = line.TrimStart();
                                        bool isSubCompleted = trimmedLine.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase);
                                        string restOfLine = trimmedLine.Length > 6 ? trimmedLine.Substring(6).Trim() : ""; 
                                        
                                        int percent = 0;
                                        string stTitle = restOfLine;
                                        
                                        var pipeIdx = restOfLine.LastIndexOf(" | ");
                                        if (pipeIdx > 0)
                                        {
                                            string pStr = restOfLine.Substring(pipeIdx + 3).Replace("%", "").Trim();
                                            if (int.TryParse(pStr, out int stPercent))
                                            {
                                                percent = stPercent;
                                            }
                                            stTitle = restOfLine.Substring(0, pipeIdx).Trim();
                                        }
                                        
                                        var st = new TaskItem(stTitle) { IsCompleted = isSubCompleted, CompletionPercent = percent };
                                        task.NoteBlocks.Add(st);
                                    }
                                    else
                                    {
                                        task.NoteBlocks.Add(new TextBlockItem(line));
                                    }
                                }
                            }
                            break;
                    }
                }
                
                state.ScratchpadText = state.ScratchpadText.TrimEnd();
                foreach (var t in state.Tasks)
                {
                    if (t.Notes != null) t.Notes = t.Notes.TrimEnd();
                }
                return state;
            }
            catch { return null; }
        }

        /// <summary>
        /// Save weekly goals with JSON format to improve efficiency and reliability.
        /// </summary>
        public static async Task SaveWeeklyGoalsAsync(DateTime date, IEnumerable<TaskItem> goals)
        {
            var filePath = GetWeeklyFilePath(date);
            try
            {
                if (!Directory.Exists(SemanasFolder)) Directory.CreateDirectory(SemanasFolder);

                var options = new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(fs, goals, options);
            }
            catch { }
        }

        /// <summary>
        /// Load weekly goals from JSON format.
        /// </summary>
        public static async Task<List<TaskItem>> LoadWeeklyGoalsAsync(DateTime date)
        {
            var filePath = GetWeeklyFilePath(date);
            if (!File.Exists(filePath)) return new List<TaskItem>();

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var options = new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
                var goals = await JsonSerializer.DeserializeAsync<List<TaskItem>>(fs, options);
                
                // Re-attach parent tasks
                if (goals != null)
                {
                    foreach (var g in goals)
                    {
                        foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                        {
                            sub.ParentTask = g;
                        }
                    }
                    return goals;
                }
            }
            catch { }

            return new List<TaskItem>();
        }

        // ─── Monthly Goals ──────────────────────────────────────────

        /// <summary>
        /// Save monthly goals with JSON format to improve efficiency and reliability.
        /// </summary>
        public static async Task SaveMonthlyGoalsAsync(DateTime date, IEnumerable<TaskItem> goals)
        {
            var filePath = GetMonthlyFilePath(date);
            try
            {
                if (!Directory.Exists(MesesFolder)) Directory.CreateDirectory(MesesFolder);

                var options = new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(fs, goals, options);
            }
            catch { }
        }

        /// <summary>
        /// Load monthly goals from JSON format.
        /// </summary>
        public static async Task<List<TaskItem>> LoadMonthlyGoalsAsync(DateTime date)
        {
            var filePath = GetMonthlyFilePath(date);
            if (!File.Exists(filePath)) return new List<TaskItem>();

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var options = new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
                var goals = await JsonSerializer.DeserializeAsync<List<TaskItem>>(fs, options);
                
                // Re-attach parent tasks
                if (goals != null)
                {
                    foreach (var g in goals)
                    {
                        foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                        {
                            sub.ParentTask = g;
                        }
                    }
                    return goals;
                }
            }
            catch { }

            return new List<TaskItem>();
        }

        public static async Task<Dictionary<DateTime, int>> LoadContributionHistoryAsync()
        {
            var history = new Dictionary<DateTime, int>();
            try
            {
                if (!Directory.Exists(DiasFolder)) return history;

                var files = Directory.GetFiles(DiasFolder, "*.md");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        var state = await LoadDailyStateAsync(date);
                        if (state != null)
                        {
                            int completedTasks = state.Tasks.Count(t => t.IsCompleted);
                            history[date.Date] = completedTasks;
                        }
                    }
                }
            }
            catch { }
            return history;
        }
    }
}
