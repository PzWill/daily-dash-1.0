using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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
        /// File path per week: metas_semana_2026-W09.md
        /// </summary>
        public static string GetWeeklyFilePath(DateTime date)
        {
            int week = GetIsoWeekNumber(date);
            return Path.Combine(SemanasFolder, $"semana_{date:yyyy}-W{week:D2}.md");
        }

        /// <summary>
        /// File path per month: meses/mes_2026-02.md
        /// </summary>
        public static string GetMonthlyFilePath(DateTime date)
        {
            return Path.Combine(MesesFolder, $"mes_{date:yyyy-MM}.md");
        }

        public static async Task SaveDailyStateAsync(DateTime date, MainViewModel viewModel)
        {
            try
            {
                if (!Directory.Exists(DiasFolder)) Directory.CreateDirectory(DiasFolder);

                string filePath = GetDailyFilePath(date);
                using StreamWriter sw = new StreamWriter(filePath, false);

                await sw.WriteLineAsync("# Foco do Dia");
                await sw.WriteLineAsync(viewModel.DailyFocus);
                await sw.WriteLineAsync();

                await sw.WriteLineAsync("## Tarefas");
                foreach (var t in viewModel.Tasks)
                {
                    string check = t.IsCompleted ? "[x]" : "[ ]";
                    await sw.WriteLineAsync($"- {check} {t.Title}");
                    // Save description if present
                    if (!string.IsNullOrWhiteSpace(t.Description))
                    {
                        await sw.WriteLineAsync($"  > {t.Description}");
                    }
                }
                await sw.WriteLineAsync();

                await sw.WriteLineAsync("## Scratchpad");
                await sw.WriteLineAsync(viewModel.ScratchpadText);
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
                string[] lines = await File.ReadAllLinesAsync(filePath);
                string currentSection = "";

                foreach (var line in lines)
                {
                    if (line.Trim() == "# Foco do Dia") { currentSection = "Focus"; continue; }
                    if (line.Trim() == "## Tarefas") { currentSection = "Tasks"; continue; }
                    if (line.Trim() == "## Scratchpad") { currentSection = "Scratchpad"; continue; }

                    if (string.IsNullOrWhiteSpace(line) && currentSection != "Scratchpad") continue;

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
                                string title = line.Substring(6).Trim();
                                state.Tasks.Add(new TaskItem(title) { IsCompleted = isCompleted });
                            }
                            break;
                        case "Scratchpad":
                            state.ScratchpadText += line + Environment.NewLine;
                            break;
                    }
                }
                
                state.ScratchpadText = state.ScratchpadText.TrimEnd();
                return state;
            }
            catch { return null; }
        }

        /// <summary>
        /// Save weekly goals with sub-tasks, completion percentage, and descriptions.
        /// Format:
        ///   - [ ] Goal Title | 45%
        ///     > Goal description
        ///     - [ ] Sub Task 1
        ///     - [x] Sub Task 2
        /// </summary>
        public static async Task SaveWeeklyGoalsAsync(DateTime date, IEnumerable<TaskItem> goals)
        {
            var filePath = GetWeeklyFilePath(date);
            try
            {
                if (!Directory.Exists(SemanasFolder)) Directory.CreateDirectory(SemanasFolder);

                using var sw = new StreamWriter(filePath, false);
                await sw.WriteLineAsync("# Metas Semanais");
                await sw.WriteLineAsync();
                foreach (var g in goals)
                {
                    string check = g.IsCompleted ? "[x]" : "[ ]";
                    await sw.WriteLineAsync($"- {check} {g.Title} | {g.CompletionPercent}%");
                    // Save goal description
                    if (!string.IsNullOrWhiteSpace(g.Description))
                    {
                        await sw.WriteLineAsync($"  > {g.Description}");
                    }
                    foreach (var sub in g.SubTasks)
                    {
                        string subCheck = sub.IsCompleted ? "[x]" : "[ ]";
                        await sw.WriteLineAsync($"  - {subCheck} {sub.Title}");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Load weekly goals with sub-tasks, completion percentage, and descriptions.
        /// </summary>
        public static async Task<List<TaskItem>> LoadWeeklyGoalsAsync(DateTime date)
        {
            var filePath = GetWeeklyFilePath(date);
            var goals = new List<TaskItem>();
            if (!File.Exists(filePath)) return goals;

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                TaskItem? currentGoal = null;

                foreach (var line in lines)
                {
                    // Skip header and blank lines
                    if (line.TrimStart().StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        continue;

                    // Description line: starts with "  > "
                    if (line.StartsWith("  > ") && currentGoal != null)
                    {
                        currentGoal.Description = line.Substring(4).Trim();
                        continue;
                    }

                    // Sub-task: starts with "  - "
                    if (line.StartsWith("  - ") && currentGoal != null)
                    {
                        var subLine = line.TrimStart();
                        if (subLine.StartsWith("- [ ] ") || subLine.StartsWith("- [x] "))
                        {
                            bool subCompleted = subLine.StartsWith("- [x] ");
                            string subTitle = subLine.Substring(6).Trim();
                            currentGoal.SubTasks.Add(new TaskItem(subTitle) { IsCompleted = subCompleted });
                        }
                        continue;
                    }

                    // Top-level goal: starts with "- "
                    if (line.StartsWith("- [ ] ") || line.StartsWith("- [x] "))
                    {
                        bool isCompleted = line.StartsWith("- [x] ");
                        string rest = line.Substring(6).Trim();

                        // Parse optional completion percentage: "Title | 45%"
                        int percent = 0;
                        string title = rest;
                        var pipeIdx = rest.LastIndexOf(" | ");
                        if (pipeIdx >= 0)
                        {
                            var percentStr = rest.Substring(pipeIdx + 3).Trim().TrimEnd('%');
                            if (int.TryParse(percentStr, out int parsed))
                            {
                                percent = parsed;
                                title = rest.Substring(0, pipeIdx).Trim();
                            }
                        }

                        currentGoal = new TaskItem(title)
                        {
                            IsCompleted = isCompleted,
                            CompletionPercent = percent
                        };
                        goals.Add(currentGoal);
                    }
                }
            }
            catch { }

            return goals;
        }

        // ─── Monthly Goals ──────────────────────────────────────────

        /// <summary>
        /// Save monthly goals with sub-tasks, completion percentage, and descriptions.
        /// Same format as weekly goals.
        /// </summary>
        public static async Task SaveMonthlyGoalsAsync(DateTime date, IEnumerable<TaskItem> goals)
        {
            var filePath = GetMonthlyFilePath(date);
            try
            {
                if (!Directory.Exists(MesesFolder)) Directory.CreateDirectory(MesesFolder);

                using var sw = new StreamWriter(filePath, false);
                await sw.WriteLineAsync("# Metas Mensais");
                await sw.WriteLineAsync();
                foreach (var g in goals)
                {
                    string check = g.IsCompleted ? "[x]" : "[ ]";
                    await sw.WriteLineAsync($"- {check} {g.Title} | {g.CompletionPercent}%");
                    if (!string.IsNullOrWhiteSpace(g.Description))
                    {
                        await sw.WriteLineAsync($"  > {g.Description}");
                    }
                    foreach (var sub in g.SubTasks)
                    {
                        string subCheck = sub.IsCompleted ? "[x]" : "[ ]";
                        await sw.WriteLineAsync($"  - {subCheck} {sub.Title}");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Load monthly goals with sub-tasks, completion percentage, and descriptions.
        /// </summary>
        public static async Task<List<TaskItem>> LoadMonthlyGoalsAsync(DateTime date)
        {
            var filePath = GetMonthlyFilePath(date);
            var goals = new List<TaskItem>();
            if (!File.Exists(filePath)) return goals;

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                TaskItem? currentGoal = null;

                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        continue;

                    // Description line
                    if (line.StartsWith("  > ") && currentGoal != null)
                    {
                        currentGoal.Description = line.Substring(4).Trim();
                        continue;
                    }

                    // Sub-task
                    if (line.StartsWith("  - ") && currentGoal != null)
                    {
                        var subLine = line.TrimStart();
                        if (subLine.StartsWith("- [ ] ") || subLine.StartsWith("- [x] "))
                        {
                            bool subCompleted = subLine.StartsWith("- [x] ");
                            string subTitle = subLine.Substring(6).Trim();
                            currentGoal.SubTasks.Add(new TaskItem(subTitle) { IsCompleted = subCompleted });
                        }
                        continue;
                    }

                    // Top-level goal
                    if (line.StartsWith("- [ ] ") || line.StartsWith("- [x] "))
                    {
                        bool isCompleted = line.StartsWith("- [x] ");
                        string rest = line.Substring(6).Trim();

                        int percent = 0;
                        string title = rest;
                        var pipeIdx = rest.LastIndexOf(" | ");
                        if (pipeIdx >= 0)
                        {
                            var percentStr = rest.Substring(pipeIdx + 3).Trim().TrimEnd('%');
                            if (int.TryParse(percentStr, out int parsed))
                            {
                                percent = parsed;
                                title = rest.Substring(0, pipeIdx).Trim();
                            }
                        }

                        currentGoal = new TaskItem(title)
                        {
                            IsCompleted = isCompleted,
                            CompletionPercent = percent
                        };
                        goals.Add(currentGoal);
                    }
                }
            }
            catch { }

            return goals;
        }
    }
}
