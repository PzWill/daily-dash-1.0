using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DailyDash.Models
{
    public partial class TaskItem : ObservableObject
    {
        [ObservableProperty]
        private string title = string.Empty;

        /// <summary>
        /// Optional description for the task/goal.
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private bool isCompleted;

        /// <summary>
        /// Completion percentage (0–100). Used for weekly/monthly goals with slider.
        /// </summary>
        [ObservableProperty]
        private int completionPercent;

        /// <summary>
        /// Sub-tasks for weekly/monthly goals.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TaskItem> subTasks = new();

        public TaskItem(string title)
        {
            Title = title;
        }

        public TaskItem() { }
    }
}
