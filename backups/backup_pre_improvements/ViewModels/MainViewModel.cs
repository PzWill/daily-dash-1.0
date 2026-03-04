using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using DailyDash.Models;
using DailyDash.Services;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System;

namespace DailyDash.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // Flag to prevent saving while loading data
        private bool _isLoading;

        [ObservableProperty]
        private string dailyFocus = "Qual a sua principal meta hoje?";

        [ObservableProperty]
        private string scratchpadText = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private ObservableCollection<TaskItem> tasks = new();

        [ObservableProperty]
        private string newTaskTitle = "";
        
        [ObservableProperty]
        private DateTime selectedDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<TaskItem> weeklyGoals = new();

        [ObservableProperty]
        private ObservableCollection<TaskItem> monthlyGoals = new();

        public double ProgressPercentage 
        {
            get
            {
                if (Tasks.Count == 0) return 0;
                var completed = Tasks.Count(t => t.IsCompleted);
                return (double)completed / Tasks.Count * 100;
            }
        }

        public MainViewModel()
        {
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            _ = LoadDataAsync();
        }

        partial void OnDailyFocusChanged(string value)
        {
            if (!_isLoading)
                _ = SaveDataAsync();
        }

        partial void OnScratchpadTextChanged(string value)
        {
            if (!_isLoading)
                _ = SaveDataAsync();
        }

        private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (e.PropertyName == nameof(TaskItem.IsCompleted))
            {
                OnPropertyChanged(nameof(ProgressPercentage));
                _ = SaveDataAsync();
            }
            else if (e.PropertyName == nameof(TaskItem.Title) || e.PropertyName == nameof(TaskItem.Description))
            {
                _ = SaveDataAsync();
            }
        }

        [RelayCommand]
        private void AddTask()
        {
            if (!string.IsNullOrWhiteSpace(NewTaskTitle))
            {
                var task = new TaskItem(NewTaskTitle);
                task.PropertyChanged += Task_PropertyChanged;
                Tasks.Add(task);
                NewTaskTitle = string.Empty;
                OnPropertyChanged(nameof(ProgressPercentage));
                _ = SaveDataAsync();
            }
        }

        [RelayCommand]
        private void RemoveTask(TaskItem? task)
        {
            if (task != null)
            {
                task.PropertyChanged -= Task_PropertyChanged;
                Tasks.Remove(task);
                OnPropertyChanged(nameof(ProgressPercentage));
                _ = SaveDataAsync();
            }
        }

        public async Task LoadDataAsync()
        {
            _isLoading = true;
            try
            {
                var state = await MarkdownDataManager.LoadDailyStateAsync(SelectedDate);

                // Unsubscribe before clearing to avoid memory leaks/unwanted saves
                foreach (var t in Tasks) t.PropertyChanged -= Task_PropertyChanged;
                Tasks.Clear();

                if (state != null)
                {
                    DailyFocus = state.DailyFocus ?? "Qual a sua principal meta hoje?";
                    ScratchpadText = state.ScratchpadText ?? "";

                    foreach (var t in state.Tasks)
                    {
                        t.PropertyChanged += Task_PropertyChanged;
                        Tasks.Add(t);
                    }
                }
                else
                {
                    DailyFocus = "Qual a sua principal meta hoje?";
                    ScratchpadText = "";
                }
                OnPropertyChanged(nameof(ProgressPercentage));

                // Load Weekly Goals
                await LoadWeeklyGoalsAsync();
                // Load Monthly Goals
                await LoadMonthlyGoalsAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Task SaveDataAsync()
        {
            if (_isLoading) return;
            await MarkdownDataManager.SaveDailyStateAsync(SelectedDate, this);
        }

        // ─── Weekly Goals ───────────────────────────────────────────

        private async Task LoadWeeklyGoalsAsync()
        {
            // Unsubscribe old
            foreach (var g in WeeklyGoals)
            {
                g.PropertyChanged -= Goal_PropertyChanged;
                foreach (var sub in g.SubTasks)
                    sub.PropertyChanged -= SubTask_PropertyChanged;
            }
            WeeklyGoals.Clear();

            var goals = await MarkdownDataManager.LoadWeeklyGoalsAsync(SelectedDate);

            if (goals.Count == 0)
            {
                // Default Goals
                var g1 = new TaskItem("Atingir 10h de Deep Work") { IsCompleted = true, CompletionPercent = 100 };
                var g2 = new TaskItem("Finalizar Dashboard UI");
                g1.PropertyChanged += Goal_PropertyChanged;
                g2.PropertyChanged += Goal_PropertyChanged;
                WeeklyGoals.Add(g1);
                WeeklyGoals.Add(g2);
                _ = SaveWeeklyGoalsAsync();
                return;
            }

            foreach (var g in goals)
            {
                g.PropertyChanged += Goal_PropertyChanged;
                foreach (var sub in g.SubTasks)
                    sub.PropertyChanged += SubTask_PropertyChanged;
                WeeklyGoals.Add(g);
            }
        }

        private async Task SaveWeeklyGoalsAsync()
        {
            if (_isLoading) return;
            await MarkdownDataManager.SaveWeeklyGoalsAsync(SelectedDate, WeeklyGoals);
        }

        private void Goal_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem goal && e.PropertyName == nameof(TaskItem.CompletionPercent))
            {
                // Auto-check when completion reaches 100%
                if (goal.CompletionPercent >= 100 && !goal.IsCompleted)
                {
                    goal.IsCompleted = true;
                }
                // Auto-uncheck if slider goes below 100%
                else if (goal.CompletionPercent < 100 && goal.IsCompleted)
                {
                    goal.IsCompleted = false;
                }
            }

            _ = SaveWeeklyGoalsAsync();
        }

        private void SubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;
            _ = SaveWeeklyGoalsAsync();
        }

        private string newGoalTitle = "";
        public string NewGoalTitle
        {
            get => newGoalTitle;
            set => SetProperty(ref newGoalTitle, value);
        }

        [RelayCommand]
        private void AddGoal()
        {
            if (!string.IsNullOrWhiteSpace(NewGoalTitle))
            {
                var goal = new TaskItem(NewGoalTitle);
                goal.PropertyChanged += Goal_PropertyChanged;
                WeeklyGoals.Add(goal);
                NewGoalTitle = string.Empty;
                _ = SaveWeeklyGoalsAsync();
            }
        }
        
        [RelayCommand]
        private void RemoveGoal(TaskItem? goal)
        {
            if (goal != null)
            {
                goal.PropertyChanged -= Goal_PropertyChanged;
                foreach (var sub in goal.SubTasks)
                    sub.PropertyChanged -= SubTask_PropertyChanged;
                WeeklyGoals.Remove(goal);
                _ = SaveWeeklyGoalsAsync();
            }
        }

        // ─── Sub-tasks for Weekly Goals ─────────────────────────────

        [RelayCommand]
        private void AddSubTask(TaskItem? parentGoal)
        {
            if (parentGoal == null) return;

            // Use a temporary prompt title—user can edit inline
            var sub = new TaskItem("Nova sub-tarefa");
            sub.PropertyChanged += SubTask_PropertyChanged;
            parentGoal.SubTasks.Add(sub);
            _ = SaveWeeklyGoalsAsync();
        }

        [RelayCommand]
        private void RemoveSubTask(TaskItem? subTask)
        {
            if (subTask == null) return;

            // Find parent goal in weekly goals
            foreach (var goal in WeeklyGoals)
            {
                if (goal.SubTasks.Contains(subTask))
                {
                    subTask.PropertyChanged -= SubTask_PropertyChanged;
                    goal.SubTasks.Remove(subTask);
                    _ = SaveWeeklyGoalsAsync();
                    return;
                }
            }

            // Find parent goal in monthly goals
            foreach (var goal in MonthlyGoals)
            {
                if (goal.SubTasks.Contains(subTask))
                {
                    subTask.PropertyChanged -= MonthlySubTask_PropertyChanged;
                    goal.SubTasks.Remove(subTask);
                    _ = SaveMonthlyGoalsAsync();
                    return;
                }
            }
        }

        // ─── Monthly Goals ──────────────────────────────────────────

        private async Task LoadMonthlyGoalsAsync()
        {
            // Unsubscribe old
            foreach (var g in MonthlyGoals)
            {
                g.PropertyChanged -= MonthlyGoal_PropertyChanged;
                foreach (var sub in g.SubTasks)
                    sub.PropertyChanged -= MonthlySubTask_PropertyChanged;
            }
            MonthlyGoals.Clear();

            var goals = await MarkdownDataManager.LoadMonthlyGoalsAsync(SelectedDate);

            foreach (var g in goals)
            {
                g.PropertyChanged += MonthlyGoal_PropertyChanged;
                foreach (var sub in g.SubTasks)
                    sub.PropertyChanged += MonthlySubTask_PropertyChanged;
                MonthlyGoals.Add(g);
            }
        }

        private async Task SaveMonthlyGoalsAsync()
        {
            if (_isLoading) return;
            await MarkdownDataManager.SaveMonthlyGoalsAsync(SelectedDate, MonthlyGoals);
        }

        private void MonthlyGoal_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem goal && e.PropertyName == nameof(TaskItem.CompletionPercent))
            {
                // Auto-check when completion reaches 100%
                if (goal.CompletionPercent >= 100 && !goal.IsCompleted)
                {
                    goal.IsCompleted = true;
                }
                else if (goal.CompletionPercent < 100 && goal.IsCompleted)
                {
                    goal.IsCompleted = false;
                }
            }

            _ = SaveMonthlyGoalsAsync();
        }

        private void MonthlySubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;
            _ = SaveMonthlyGoalsAsync();
        }

        private string newMonthlyGoalTitle = "";
        public string NewMonthlyGoalTitle
        {
            get => newMonthlyGoalTitle;
            set => SetProperty(ref newMonthlyGoalTitle, value);
        }

        [RelayCommand]
        private void AddMonthlyGoal()
        {
            if (!string.IsNullOrWhiteSpace(NewMonthlyGoalTitle))
            {
                var goal = new TaskItem(NewMonthlyGoalTitle);
                goal.PropertyChanged += MonthlyGoal_PropertyChanged;
                MonthlyGoals.Add(goal);
                NewMonthlyGoalTitle = string.Empty;
                _ = SaveMonthlyGoalsAsync();
            }
        }

        [RelayCommand]
        private void RemoveMonthlyGoal(TaskItem? goal)
        {
            if (goal != null)
            {
                goal.PropertyChanged -= MonthlyGoal_PropertyChanged;
                foreach (var sub in goal.SubTasks)
                    sub.PropertyChanged -= MonthlySubTask_PropertyChanged;
                MonthlyGoals.Remove(goal);
                _ = SaveMonthlyGoalsAsync();
            }
        }

        [RelayCommand]
        private void AddMonthlySubTask(TaskItem? parentGoal)
        {
            if (parentGoal == null) return;

            var sub = new TaskItem("Nova sub-tarefa");
            sub.PropertyChanged += MonthlySubTask_PropertyChanged;
            parentGoal.SubTasks.Add(sub);
            _ = SaveMonthlyGoalsAsync();
        }
    }
}
