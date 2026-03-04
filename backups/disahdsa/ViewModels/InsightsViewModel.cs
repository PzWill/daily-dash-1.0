using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DailyDash.Services;

namespace DailyDash.ViewModels
{
    public partial class InsightsViewModel : ObservableObject
    {
        [ObservableProperty]
        private int totalTasksCompleted = 0;

        [ObservableProperty]
        private int totalGoalsCompleted = 0;

        [ObservableProperty]
        private double averageDailyTasks = 0;

        [ObservableProperty]
        private ObservableCollection<TagDistributionItem> tagDistribution = new();

        public async Task LoadInsightsAsync()
        {
            var dailyData = await MarkdownDataManager.LoadContributionHistoryAsync();
            
            // Total Tasks Completed
            TotalTasksCompleted = dailyData.Values.Sum();

            // Average Daily Tasks
            if (dailyData.Count > 0)
            {
                AverageDailyTasks = Math.Round((double)TotalTasksCompleted / dailyData.Count, 1);
            }

            // --- Note: For a real dashboard, we'd need a more extensive parsing method in MarkdownDataManager
            // to extract ALL tags across all days, and ALL completed weekly/monthly goals.
            // For now, we will add a simplified placeholder or an extended method in MarkdownDataManager.
            
            // For demonstration of the UI, we'll populate some dummy data if real data parser isn't fully implemented yet.
            // Let's implement the real basic tag counting if possible, but keep it simple.
            
            var allTags = await LoadAllTagsFromHistoryAsync();
            TagDistribution.Clear();
            foreach (var kvp in allTags.OrderByDescending(x => x.Value))
            {
                TagDistribution.Add(new TagDistributionItem { TagName = kvp.Key.Name, ColorHex = kvp.Key.Color, Count = kvp.Value });
            }
        }

        private async Task<Dictionary<(string Name, string Color), int>> LoadAllTagsFromHistoryAsync()
        {
            // Ideally, MarkdownDataManager should have a method for this so we don't duplicate logic.
            // Since it might be heavy to load ALL files every time, this is just a basic implementation.
            var tagsCount = new Dictionary<(string, string), int>();
            
            try
            {
                var baseFolder = System.IO.Path.Combine(@"C:\Users\willi\Documents\antigravity projects\daily dash 2\data", "dias");
                if (!System.IO.Directory.Exists(baseFolder)) return tagsCount;

                var files = System.IO.Directory.GetFiles(baseFolder, "*.md");
                foreach (var file in files)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
                    {
                        var state = await MarkdownDataManager.LoadDailyStateAsync(date);
                        if (state != null)
                        {
                            foreach (var task in state.Tasks)
                            {
                                if (!string.IsNullOrEmpty(task.Tag))
                                {
                                    var key = (task.Tag, task.TagColor ?? "#888888");
                                    if (tagsCount.ContainsKey(key))
                                        tagsCount[key]++;
                                    else
                                        tagsCount[key] = 1;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return tagsCount;
        }
    }

    public class TagDistributionItem
    {
        public string TagName { get; set; } = "";
        public string ColorHex { get; set; } = "#FFFFFF";
        public int Count { get; set; }
    }
}
