using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using DailyDash.Models;
using DailyDash.ViewModels;

namespace DailyDash.Services
{
    public class AppState
    {
        public string DailyFocus { get; set; } = "Qual a sua principal meta hoje?";
        public string ScratchpadText { get; set; } = "";
        public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    public static class DataManager
    {
        private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyDash");
        private static readonly string FilePath = Path.Combine(FolderPath, "appdata.json");

        public static async Task SaveStateAsync(MainViewModel viewModel)
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }

                var state = new AppState
                {
                    DailyFocus = viewModel.DailyFocus,
                    ScratchpadText = viewModel.ScratchpadText,
                    Tasks = new List<TaskItem>(viewModel.Tasks)
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(state, options);
                
                await File.WriteAllTextAsync(FilePath, json);
            }
            catch (Exception ex)
            {
                // Simple logging or handle exception
                System.Diagnostics.Debug.WriteLine("Error saving data: " + ex.Message);
            }
        }

        public static async Task<AppState?> LoadStateAsync()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                string json = await File.ReadAllTextAsync(FilePath);
                return JsonSerializer.Deserialize<AppState>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading data: " + ex.Message);
                return null;
            }
        }
    }
}
