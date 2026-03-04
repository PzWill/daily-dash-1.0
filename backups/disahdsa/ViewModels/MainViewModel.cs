using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using DailyDash.Models;
using DailyDash.Services;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Collections.Generic;

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
        private bool isFocusModeEnabled = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private ObservableCollection<TaskItem> tasks = new();

        [ObservableProperty]
        private string newTaskTitle = "";
        
        [ObservableProperty]
        private ObservableCollection<TagModel> availableTags = new() 
        { 
            new TagModel { Name="Sem Tag", ColorHex="Transparent" },
            new TagModel { Name="Trabalho", ColorHex="#FF3B30" },
            new TagModel { Name="Pessoal", ColorHex="#34C759" },
            new TagModel { Name="Saúde", ColorHex="#007AFF" },
            new TagModel { Name="Estudo", ColorHex="#AF52DE" },
            new TagModel { Name="Finanças", ColorHex="#FF9500" }
        };

        [ObservableProperty]
        private TagModel? selectedTagItem;

        [ObservableProperty]
        private bool isTagMenuOpen;

        [ObservableProperty]
        private bool isEditingTag;

        [ObservableProperty]
        private TagModel editingTag = new();

        private TagModel? _tagBeingEditedOriginal = null;
        private TaskItem? _taskBeingTagged = null;

        [ObservableProperty]
        private TaskItem? selectedTaskForDetails;

        [ObservableProperty]
        private bool isTaskDetailMode;

        [ObservableProperty]
        private bool isScratchpadExpanded;

        [RelayCommand]
        private void NavigateToTaskDetail(TaskItem? task)
        {
            if (task != null)
            {
                SelectedTaskForDetails = task;
                
                // Ensure at least one block exists
                if (task.NoteBlocks.Count == 0)
                {
                    var block = new TextBlockItem("");
                    block.ParentTask = task;
                    block.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(block);
                        _ = SaveDataAsync();
                    };
                    task.NoteBlocks.Add(block);
                    block.IsFocused = true;
                    _ = SaveDataAsync();
                }
                
                IsTaskDetailMode = true;
            }
        }

        [RelayCommand]
        private void CloseTaskDetail()
        {
            SelectedTaskForDetails = null;
            IsTaskDetailMode = false;
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void ToggleScratchpadExpanded()
        {
            IsScratchpadExpanded = !IsScratchpadExpanded;
        }

        [RelayCommand]
        private void OpenTagMenu()
        {
            IsEditingTag = false;
            IsTagMenuOpen = true;
        }

        [RelayCommand]
        private void SelectTag(TagModel? tag)
        {
            if (tag != null)
            {
                if (_taskBeingTagged != null)
                {
                    _taskBeingTagged.Tag = tag.Name == "Sem Tag" ? "" : tag.Name;
                    _taskBeingTagged.TagColor = tag.ColorHex;
                    _ = SaveDataAsync();
                    _taskBeingTagged = null;
                }
                else
                {
                    SelectedTagItem = tag;
                }
                IsTagMenuOpen = false;
            }
        }

        [RelayCommand]
        private void StartCreateTag()
        {
            EditingTag = new TagModel { Name = "Nova Tag", ColorHex = GenerateRandomColor() };
            _tagBeingEditedOriginal = null;
            IsEditingTag = true;
        }

        [RelayCommand]
        private async Task EditSpecificTag(TaskItem? task)
        {
            if (task == null) return;
            
            _taskBeingTagged = task;
            
            // Wait for double-click mouse events to finish propagating so the popup isn't instantly closed
            await Task.Delay(150);
            
            IsTagMenuOpen = true;

            if (!string.IsNullOrEmpty(task.Tag) && task.Tag != "Sem Tag")
            {
                var tagModel = AvailableTags.FirstOrDefault(t => t.Name == task.Tag);
                if (tagModel != null)
                {
                    StartEditTag(tagModel);
                }
            }
            else
            {
                IsEditingTag = false;
            }
        }

        [RelayCommand]
        private void StartEditTag(TagModel? tag)
        {
            if (tag == null || tag.Name == "Sem Tag") return;
            EditingTag = tag.Clone();
            _tagBeingEditedOriginal = tag;
            IsEditingTag = true;
        }

        [RelayCommand]
        private void SaveTag()
        {
            if (string.IsNullOrWhiteSpace(EditingTag.Name)) return;

            if (_tagBeingEditedOriginal != null)
            {
                string oldName = _tagBeingEditedOriginal.Name;
                _tagBeingEditedOriginal.Name = EditingTag.Name;
                _tagBeingEditedOriginal.ColorHex = EditingTag.ColorHex;
                if (SelectedTagItem == _tagBeingEditedOriginal)
                {
                    OnPropertyChanged(nameof(SelectedTagItem));
                }

                UpdateGlobalTag(oldName, EditingTag.Name, EditingTag.ColorHex);
                
                if (_taskBeingTagged != null && _taskBeingTagged.Tag == oldName)
                {
                    _taskBeingTagged.Tag = EditingTag.Name;
                    _taskBeingTagged.TagColor = EditingTag.ColorHex;
                }
            }
            else
            {
                var newTag = EditingTag.Clone();
                AvailableTags.Add(newTag);
            }
            
            _ = SaveTagsAsync();
            IsEditingTag = false;
        }

        private void UpdateGlobalTag(string oldName, string newName, string newColorHex)
        {
            bool anyChanged = false;
            void SyncTagList(ObservableCollection<TaskItem> list)
            {
                foreach (var item in list)
                {
                    if (item.Tag == oldName || (item.Tag == newName && item.TagColor != newColorHex))
                    {
                        item.Tag = newName;
                        item.TagColor = newColorHex;
                        anyChanged = true;
                    }
                }
            }
            SyncTagList(Tasks);
            SyncTagList(WeeklyGoals);
            SyncTagList(MonthlyGoals);

            if (anyChanged)
            {
                _ = SaveDataAsync();
                _ = SaveWeeklyGoalsAsync();
                _ = SaveMonthlyGoalsAsync();
            }
        }

        [RelayCommand]
        private void CancelEditTag()
        {
            IsEditingTag = false;
        }

        [RelayCommand]
        private void SetEditingColor(string colorHex)
        {
            if (!string.IsNullOrEmpty(colorHex))
            {
                EditingTag.ColorHex = colorHex;
            }
        }


        public string[] ColorPalette { get; } = new string[] 
        {
            "#FF3B30", "#FF9500", "#FFCC00", "#4CD964", "#34C759", "#5AC8FA",
            "#007AFF", "#5856D6", "#AF52DE", "#FF2D55", "#A2845E", "#8E8E93"
        };

        
        [ObservableProperty]
        private DateTime selectedDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<TaskItem> weeklyGoals = new();

        [ObservableProperty]
        private ObservableCollection<TaskItem> monthlyGoals = new();

        public System.Collections.Generic.IEnumerable<TaskItem> AllGoalsWithDeadlines => 
            WeeklyGoals.Concat(MonthlyGoals).Where(g => g.Deadline.HasValue);

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
            Tasks.CollectionChanged += (s, e) => { if (!_isLoading) _ = SaveDataAsync(); };
            WeeklyGoals.CollectionChanged += (s, e) => { 
                if (!_isLoading) _ = SaveWeeklyGoalsAsync(); 
                OnPropertyChanged(nameof(AllGoalsWithDeadlines));
            };
            MonthlyGoals.CollectionChanged += (s, e) => { 
                if (!_isLoading) _ = SaveMonthlyGoalsAsync(); 
                OnPropertyChanged(nameof(AllGoalsWithDeadlines));
            };
            _ = LoadTagsAsync().ContinueWith(t => {
                if (SelectedTagItem == null && AvailableTags.Count > 0)
                {
                    SelectedTagItem = AvailableTags[0];
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
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

            if (sender is TaskItem task)
            {
                if (e.PropertyName == nameof(TaskItem.IsCompleted))
                {
                    OnPropertyChanged(nameof(ProgressPercentage));
                    _ = SaveDataAsync();
                    UpdateLinkedGoalStatus(task);
                }
                else if (e.PropertyName == nameof(TaskItem.Title) || 
                         e.PropertyName == nameof(TaskItem.Description) || 
                         e.PropertyName == nameof(TaskItem.NotesMarkdown))
                {
                    _ = SaveDataAsync();
                }
                
                else if (e.PropertyName == nameof(TaskItem.NoteBlocks))
                {
                    if (task.NoteBlocks != null)
                    {
                        foreach (var sub in task.NoteBlocks.OfType<TaskItem>())
                        {
                            sub.PropertyChanged -= DailySubTask_PropertyChanged;
                            sub.PropertyChanged += DailySubTask_PropertyChanged;
                        }
                    }
                    OnPropertyChanged(nameof(ProgressPercentage));
                }
                
                // Refresh heatmap if a task status changed
                if (e.PropertyName == nameof(TaskItem.IsCompleted))
                {
                    _ = LoadContributionHistoryAsync();
                }
            }
        }

        private void UpdateLinkedGoalStatus(TaskItem changedTask)
        {
            if (string.IsNullOrEmpty(changedTask.LinkedGoalId)) return;

            // Determine what the target ID is
            // If the changedTask has its own ID (and no link), it's the source.
            // But if it has a LinkedGoalId, that ID represents the "family" of linked tasks.
            // We'll update anything whose ID matches the LinkedGoalId OR whose LinkedGoalId matches this ID or LinkedGoalId.
            string targetFamilyId = changedTask.LinkedGoalId;

            void SyncList(ObservableCollection<TaskItem> list)
            {
                foreach (var item in list)
                {
                    if (item != changedTask && (item.Id == targetFamilyId || item.LinkedGoalId == targetFamilyId || item.LinkedGoalId == changedTask.Id))
                    {
                        item.IsCompleted = changedTask.IsCompleted;
                    }

                    foreach (var sub in item.NoteBlocks.OfType<TaskItem>())
                    {
                        if (sub != changedTask && (sub.Id == targetFamilyId || sub.LinkedGoalId == targetFamilyId || sub.LinkedGoalId == changedTask.Id))
                        {
                            sub.IsCompleted = changedTask.IsCompleted;
                        }
                    }
                }
            }

            SyncList(Tasks);
            SyncList(WeeklyGoals);
            SyncList(MonthlyGoals);
        }

        [RelayCommand]
        private void ToggleFocusMode()
        {
            IsFocusModeEnabled = !IsFocusModeEnabled;
        }

        [RelayCommand]
        private void AddTask()
        {
            if (!string.IsNullOrWhiteSpace(NewTaskTitle))
            {
                string title = NewTaskTitle.Trim();
                string tag = SelectedTagItem?.Name ?? "Sem Tag";
                string tagColor = SelectedTagItem?.ColorHex ?? "Transparent";
                
                // Keep hashtag parsing for fast entry if typed manually
                int hashIndex = title.LastIndexOf('#');
                if (hashIndex >= 0 && hashIndex < title.Length - 1)
                {
                    string potentialTag = title.Substring(hashIndex + 1).Trim();
                    if (!potentialTag.Contains(" ")) 
                    {
                        tag = potentialTag;
                        title = title.Substring(0, hashIndex).Trim();
                        tagColor = GetColorForTag(tag);
                        
                        if (!AvailableTags.Any(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                        {
                            AvailableTags.Add(new TagModel { Name = tag, ColorHex = tagColor });
                            _ = SaveTagsAsync();
                        }
                    }
                }

                var task = new TaskItem(title) { Tag = tag, TagColor = tagColor };
                task.PropertyChanged += Task_PropertyChanged;
                Tasks.Add(task);
                NewTaskTitle = string.Empty;
                SelectedTagItem = AvailableTags.FirstOrDefault(t => t.Name == "Sem Tag") ?? AvailableTags[0];
                OnPropertyChanged(nameof(ProgressPercentage));
                _ = SaveDataAsync();
            }
        }

        [RelayCommand]
        private void LinkGoalToToday(TaskItem? goalToLink)
        {
            if (goalToLink == null) return;
            if (Tasks.Any(t => t.LinkedGoalId == goalToLink.Id)) return;

            var task = new TaskItem(goalToLink.Title) 
            { 
                Tag = goalToLink.Tag, TagColor = goalToLink.TagColor, LinkedGoalId = goalToLink.Id
            };
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
            OnPropertyChanged(nameof(ProgressPercentage));
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void CopyGoalToToday(TaskItem? goalToCopy)
        {
            if (goalToCopy == null) return;

            var task = new TaskItem(goalToCopy.Title) 
            { 
                Tag = goalToCopy.Tag, TagColor = goalToCopy.TagColor
            };
            task.PropertyChanged += Task_PropertyChanged;
            Tasks.Add(task);
            OnPropertyChanged(nameof(ProgressPercentage));
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void LinkGoalToWeekly(TaskItem? goalToLink)
        {
            if (goalToLink == null) return;
            if (WeeklyGoals.Any(t => t.LinkedGoalId == goalToLink.Id)) return;

            var goal = new TaskItem(goalToLink.Title) 
            { 
                Tag = goalToLink.Tag, TagColor = goalToLink.TagColor, LinkedGoalId = goalToLink.Id
            };
            goal.PropertyChanged += Goal_PropertyChanged;
            WeeklyGoals.Add(goal);
            _ = SaveWeeklyGoalsAsync();
        }

        [RelayCommand]
        private void CopyGoalToWeekly(TaskItem? goalToCopy)
        {
            if (goalToCopy == null) return;

            var goal = new TaskItem(goalToCopy.Title) 
            { 
                Tag = goalToCopy.Tag, TagColor = goalToCopy.TagColor
            };
            goal.PropertyChanged += Goal_PropertyChanged;
            WeeklyGoals.Add(goal);
            _ = SaveWeeklyGoalsAsync();
        }

        [RelayCommand]
        private void LinkGoalToMonthly(TaskItem? goalToLink)
        {
            if (goalToLink == null) return;
            if (MonthlyGoals.Any(t => t.LinkedGoalId == goalToLink.Id)) return;

            var goal = new TaskItem(goalToLink.Title) 
            { 
                Tag = goalToLink.Tag, TagColor = goalToLink.TagColor, LinkedGoalId = goalToLink.Id
            };
            goal.PropertyChanged += MonthlyGoal_PropertyChanged;
            MonthlyGoals.Add(goal);
            _ = SaveMonthlyGoalsAsync();
        }

        [RelayCommand]
        private void CopyGoalToMonthly(TaskItem? goalToCopy)
        {
            if (goalToCopy == null) return;

            var goal = new TaskItem(goalToCopy.Title) 
            { 
                Tag = goalToCopy.Tag, TagColor = goalToCopy.TagColor
            };
            goal.PropertyChanged += MonthlyGoal_PropertyChanged;
            MonthlyGoals.Add(goal);
            _ = SaveMonthlyGoalsAsync();
        }

        private static readonly string TagsFilePath = @"C:\Users\willi\Documents\antigravity projects\daily dash 2\data\configuracoes\tags.json";

        private async Task LoadTagsAsync()
        {
            try
            {
                if (System.IO.File.Exists(TagsFilePath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(TagsFilePath);
                    var tags = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<TagModel>>(json);
                    if (tags != null && tags.Count > 0)
                    {
                        if (!tags.Any(t => t.Name == "Sem Tag"))
                            tags.Insert(0, new TagModel { Name="Sem Tag", ColorHex="Transparent" });
                        AvailableTags = new ObservableCollection<TagModel>(tags);
                    }
                }
            }
            catch { }
        }

        private async Task SaveTagsAsync()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(TagsFilePath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir!);
                var json = System.Text.Json.JsonSerializer.Serialize(AvailableTags.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(TagsFilePath, json);
            }
            catch { }
        }

        [RelayCommand]
        private void RemoveTag(TagModel tag)
        {
            if (tag != null && tag.Name != "Sem Tag" && AvailableTags.Contains(tag))
            {
                AvailableTags.Remove(tag);
                _ = SaveTagsAsync();
                if (SelectedTagItem == tag) SelectedTagItem = AvailableTags.FirstOrDefault(t => t.Name == "Sem Tag") ?? AvailableTags[0];
            }
        }

        private string GetColorForTag(string tag)
        {
            var existing = AvailableTags.FirstOrDefault(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing.ColorHex;
            
            return tag.ToLowerInvariant() switch
            {
                "work" or "trabalho" => "#FF3B30", 
                "personal" or "pessoal" => "#34C759", 
                "health" or "saúde" or "saude" => "#007AFF", 
                "study" or "estudo" => "#AF52DE", 
                "finance" or "finanças" or "financas" => "#FF9500", 
                _ => GenerateRandomColor()
            };
        }

        private string GenerateRandomColor()
        {
            string[] niceColors = new[] { "#FF3B30", "#34C759", "#007AFF", "#AF52DE", "#FF9500", "#FF2D55", "#5856D6", "#5AC8FA", "#FFCC00" };
            return niceColors[Random.Shared.Next(niceColors.Length)];
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
        foreach (var t in Tasks) 
        {
            t.PropertyChanged -= Task_PropertyChanged;
            foreach (var st in t.NoteBlocks.OfType<TaskItem>())
            {
                st.PropertyChanged -= DailySubTask_PropertyChanged;
            }
        }
        Tasks.Clear();

        if (state != null)
        {
            DailyFocus = state.DailyFocus ?? "Qual a sua principal meta hoje?";
            ScratchpadText = state.ScratchpadText ?? "";

            foreach (var t in state.Tasks)
            {
                t.PropertyChanged += Task_PropertyChanged;
                foreach (var b in t.NoteBlocks)
                {
                    if (b is TaskItem st) st.PropertyChanged += DailySubTask_PropertyChanged;
                    if (b is TextBlockItem tb) tb.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(tb);
                        _ = SaveDataAsync();
                    };
                }
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
                // Load Heatmap History
                await LoadContributionHistoryAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private bool _isSaving;
        private bool _savePending;

        public async Task SaveDataAsync()
        {
            if (_isLoading) return;
            if (_isSaving)
            {
                _savePending = true;
                return;
            }

            _isSaving = true;
            try
            {
                do
                {
                    _savePending = false;
                    await MarkdownDataManager.SaveDailyStateAsync(SelectedDate, this);
                } while (_savePending);
            }
            finally
            {
                _isSaving = false;
            }
        }

        // ─── Weekly Goals ───────────────────────────────────────────

        private async Task LoadWeeklyGoalsAsync()
        {
            // Unsubscribe old
            foreach (var g in WeeklyGoals)
            {
                g.PropertyChanged -= Goal_PropertyChanged;
                foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                    sub.PropertyChanged -= SubTask_PropertyChanged;
            }
            WeeklyGoals.Clear();

            var goals = await MarkdownDataManager.LoadWeeklyGoalsAsync(SelectedDate);

            if (goals.Count == 0)
            {
                return;
            }

            foreach (var g in goals)
            {
                if (string.IsNullOrEmpty(g.Tag))
                {
                    g.Tag = "Semanal";
                    g.TagColor = "#007AFF";
                }
                g.PropertyChanged += Goal_PropertyChanged;
                foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                    sub.PropertyChanged += SubTask_PropertyChanged;
                WeeklyGoals.Add(g);
            }
        }

        private bool _isSavingWeekly;
        private bool _saveWeeklyPending;

        private async Task SaveWeeklyGoalsAsync()
        {
            if (_isLoading) return;
            if (_isSavingWeekly)
            {
                _saveWeeklyPending = true;
                return;
            }

            _isSavingWeekly = true;
            try
            {
                do
                {
                    _saveWeeklyPending = false;
                    await MarkdownDataManager.SaveWeeklyGoalsAsync(SelectedDate, WeeklyGoals);
                } while (_saveWeeklyPending);
            }
            finally
            {
                _isSavingWeekly = false;
            }
        }

        private void Goal_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem goal)
            {
                if (e.PropertyName == nameof(TaskItem.CompletionPercent))
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
                else if (e.PropertyName == nameof(TaskItem.IsCompleted))
                {
                    UpdateLinkedGoalStatus(goal);
                }
                else if (e.PropertyName == nameof(TaskItem.Deadline))
                {
                    OnPropertyChanged(nameof(AllGoalsWithDeadlines));
                }
            }

            _ = SaveWeeklyGoalsAsync();
        }

        private void SubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem subtask && e.PropertyName == nameof(TaskItem.IsCompleted))
            {
                subtask.ParentTask?.UpdateCompletionPercent();
                UpdateLinkedGoalStatus(subtask);
            }

            _ = SaveWeeklyGoalsAsync();
        }

        private void DailySubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem subtask && (e.PropertyName == nameof(TaskItem.IsCompleted) || e.PropertyName == nameof(TaskItem.Title)))
            {
                subtask.ParentTask?.UpdateCompletionPercent();
                UpdateLinkedGoalStatus(subtask);
                // Note: The UI subtask checking sync back to NotesMarkdown is fully handled 
                // by subtask.ParentTask?.UpdateMarkdownCheckbox(subtask) which fires automatically
                // inside TaskItem when IsCompleted changes. We don't need duplicate regex here.
            }

            _ = SaveDataAsync();
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
                var goal = new TaskItem(NewGoalTitle) { Tag = "Semanal", TagColor = "#007AFF" };
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
                foreach (var sub in goal.NoteBlocks.OfType<TaskItem>())
                    sub.PropertyChanged -= SubTask_PropertyChanged;
                WeeklyGoals.Remove(goal);
                _ = SaveWeeklyGoalsAsync();
            }
        }

        // ─── Sub-tasks for Weekly Goals ─────────────────────────────

        // Automated system: creation and deletion of blocks is handled via triggers and backspace

        [RelayCommand]
        private void HandleSubTaskEnter(TaskItem? subTask)
        {
            if (subTask == null) return;
            
            var parent = subTask.ParentTask;
            if (parent == null) return;

            int index = parent.NoteBlocks.IndexOf(subTask);
            if (index < 0) return;

            if (string.IsNullOrWhiteSpace(subTask.Title))
            {
                // Empty subtask, hitting enter should convert it back to text (default behavior for lists)
                HandleSubTaskBackspace(subTask);
                return;
            }

            var nextSub = new TaskItem("");
            nextSub.ParentTask = parent;
            
            // Wire up based on parent type
            if (Tasks.Contains(parent)) nextSub.PropertyChanged += DailySubTask_PropertyChanged;
            else if (WeeklyGoals.Contains(parent)) nextSub.PropertyChanged += SubTask_PropertyChanged;
            else if (MonthlyGoals.Contains(parent)) nextSub.PropertyChanged += MonthlySubTask_PropertyChanged;

            parent.NoteBlocks.Insert(index + 1, nextSub);
            nextSub.IsEditing = true; // Use the new editing flag!
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void HandleSubTaskBackspace(TaskItem? subTask)
        {
            if (subTask == null || !string.IsNullOrEmpty(subTask.Title)) return;

            var parent = subTask.ParentTask;
            if (parent == null) return;

            int index = parent.NoteBlocks.IndexOf(subTask);
            if (index < 0) return;

            // Remove the subtask block
            parent.NoteBlocks.RemoveAt(index);

            // Merge surrounding text blocks if they exist
            TextBlockItem? prevBlock = index > 0 ? parent.NoteBlocks[index - 1] as TextBlockItem : null;
            TextBlockItem? nextBlock = index < parent.NoteBlocks.Count ? parent.NoteBlocks[index] as TextBlockItem : null;

            if (prevBlock != null && nextBlock != null)
            {
                // Join them
                prevBlock.Text = prevBlock.Text + nextBlock.Text;
                parent.NoteBlocks.Remove(nextBlock);
                prevBlock.IsFocused = true;
            }
            else if (prevBlock != null)
            {
                prevBlock.IsFocused = true;
            }
            else if (nextBlock != null)
            {
                nextBlock.IsFocused = true;
            }
            else
            {
                // If nothing is left, create one empty block
                var newBlock = new TextBlockItem("");
                newBlock.ParentTask = parent;
                newBlock.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(newBlock);
                    _ = SaveDataAsync();
                };
                parent.NoteBlocks.Add(newBlock);
                newBlock.IsFocused = true;
            }

            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void MoveSubTaskUp(NoteBlock? block)
        {
            if (block == null) return;
            
            var coll = FindCollectionForBlock(block);
            if (coll == null) return;

            int index = coll.IndexOf(block);
            if (index > 0)
            {
                var prev = coll[index - 1];
                if (prev is TextBlockItem tb)
                {
                    var lines = Regex.Split(tb.Text, @"\r\n|\n|\r").ToList();
                    if (lines.Count > 1)
                    {
                        string lastLine = lines.Last();
                        lines.RemoveAt(lines.Count - 1);
                        tb.Text = string.Join(Environment.NewLine, lines);
                        
                        var afterText = new TextBlockItem(lastLine) { ParentTask = block.ParentTask };
                        afterText.PropertyChanged += (s, e) => {
                            if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(afterText);
                            _ = SaveDataAsync();
                        };
                        coll.Insert(index + 1, afterText);
                    }
                    else
                    {
                        coll.Move(index, index - 1);
                    }
                }
                else
                {
                    coll.Move(index, index - 1);
                }
                
                // Use dispatcher to ensure the UI has updated before requesting focus
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    if (block is TaskItem task) task.IsEditing = true;
                    block.IsFocused = false; 
                    block.IsFocused = true;
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                MergeAdjacentTextBlocks(coll);
                _ = SaveDataAsync();
            }
        }

        [RelayCommand]
        private void MoveSubTaskDown(NoteBlock? block)
        {
            if (block == null) return;

            var coll = FindCollectionForBlock(block);
            if (coll == null) return;

            int index = coll.IndexOf(block);
            if (index >= 0 && index < coll.Count - 1)
            {
                var next = coll[index + 1];
                if (next is TextBlockItem tb)
                {
                    var lines = Regex.Split(tb.Text, @"\r\n|\n|\r").ToList();
                    if (lines.Count > 1)
                    {
                        string firstLine = lines.First();
                        lines.RemoveAt(0);
                        tb.Text = string.Join(Environment.NewLine, lines);
                        
                        var beforeText = new TextBlockItem(firstLine) { ParentTask = block.ParentTask };
                        beforeText.PropertyChanged += (s, e) => {
                            if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(beforeText);
                            _ = SaveDataAsync();
                        };
                        coll.Insert(index, beforeText);
                    }
                    else
                    {
                        coll.Move(index, index + 1);
                    }
                }
                else
                {
                    coll.Move(index, index + 1);
                }

                // Use dispatcher to ensure the UI has updated before requesting focus
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    if (block is TaskItem task) task.IsEditing = true;
                    block.IsFocused = false;
                    block.IsFocused = true;
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                MergeAdjacentTextBlocks(coll);
                _ = SaveDataAsync();
            }
        }

        private void MergeAdjacentTextBlocks(ObservableCollection<NoteBlock> coll)
        {
            for (int i = 0; i < coll.Count - 1; i++)
            {
                if (coll[i] is TextBlockItem tb1 && coll[i + 1] is TextBlockItem tb2)
                {
                    // If we just split them, don't merge them immediately back if it avoids the split.
                    // But here we want the document to stay unified, so merging is correct as long as they are truly adjacent.
                    tb1.Text = tb1.Text.TrimEnd() + Environment.NewLine + tb2.Text.TrimStart();
                    coll.RemoveAt(i + 1);
                    i--; 
                }
            }
        }

        private ObservableCollection<NoteBlock>? FindCollectionForBlock(NoteBlock block)
        {
            if (block.ParentTask != null) return block.ParentTask.NoteBlocks;
            if (SelectedTaskForDetails != null && SelectedTaskForDetails.NoteBlocks.Contains(block))
                return SelectedTaskForDetails.NoteBlocks;

            foreach (var t in Tasks) if (t.NoteBlocks.Contains(block)) return t.NoteBlocks;
            foreach (var g in WeeklyGoals) if (g.NoteBlocks.Contains(block)) return g.NoteBlocks;
            foreach (var m in MonthlyGoals) if (m.NoteBlocks.Contains(block)) return m.NoteBlocks;

            return null;
        }

        [RelayCommand]
        private void AddSubTask(TaskItem? parentGoal)
        {
            if (parentGoal == null) return;

            // Use a temporary prompt title—user can edit inline
            var sub = new TaskItem("Nova sub-tarefa");
            
            // Wire up the correct handler based on which collection owns the parent
            if (Tasks.Contains(parentGoal))
            {
                // Add to Markdown notes so it's persisted. 
                // The ParseMarkdownSubtasks will automatically create the NoteBlock.
                if (string.IsNullOrWhiteSpace(parentGoal.NotesMarkdown)) 
                    parentGoal.NotesMarkdown = "* [ ] Nova sub-tarefa";
                else if (!parentGoal.NotesMarkdown.EndsWith(Environment.NewLine) && !parentGoal.NotesMarkdown.EndsWith("\n"))
                    parentGoal.NotesMarkdown += Environment.NewLine + Environment.NewLine + "* [ ] Nova sub-tarefa";
                else
                    parentGoal.NotesMarkdown += Environment.NewLine + "* [ ] Nova sub-tarefa";

                _ = SaveDataAsync();
            }
            else if (WeeklyGoals.Contains(parentGoal))
            {
                sub.PropertyChanged += SubTask_PropertyChanged;
                parentGoal.NoteBlocks.Add(sub);
                _ = SaveWeeklyGoalsAsync();
            }
            else if (MonthlyGoals.Contains(parentGoal))
            {
                sub.PropertyChanged += MonthlySubTask_PropertyChanged;
                parentGoal.NoteBlocks.Add(sub);
                _ = SaveMonthlyGoalsAsync();
            }
            else
            {
                // Fallback: try daily
                // Add to Markdown notes so it's persisted
                if (string.IsNullOrWhiteSpace(parentGoal.NotesMarkdown)) 
                    parentGoal.NotesMarkdown = "* [ ] Nova sub-tarefa";
                else if (!parentGoal.NotesMarkdown.EndsWith(Environment.NewLine) && !parentGoal.NotesMarkdown.EndsWith("\n"))
                    parentGoal.NotesMarkdown += Environment.NewLine + Environment.NewLine + "* [ ] Nova sub-tarefa";
                else
                    parentGoal.NotesMarkdown += Environment.NewLine + "* [ ] Nova sub-tarefa";

                _ = SaveDataAsync();
            }
        }

        // Text block shortcuts and lifecycle

        [RelayCommand]
        private void HandleTextBlockShortcut(TextBlockItem? block)
        {
            if (block == null || string.IsNullOrEmpty(block.Text)) return;

            string[] lines = block.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("[] ") || trimmedLine.StartsWith("- [ ] "))
                {
                    // Found a trigger!
                    string title = trimmedLine.StartsWith("[] ") ? trimmedLine.Substring(3) : trimmedLine.Substring(6);
                    
                    var parent = block.ParentTask;
                    if (parent == null) return;

                    int index = parent.NoteBlocks.IndexOf(block);
                    if (index < 0) return;

                    // Text before the trigger
                    string textBefore = string.Join(Environment.NewLine, lines.Take(i)).TrimEnd();
                    // Text after the trigger
                    string textAfter = string.Join(Environment.NewLine, lines.Skip(i + 1)).TrimStart();

                    // Remove original block
                    parent.NoteBlocks.RemoveAt(index);
                    int currentInsertIndex = index;

                    // Insert text before if not empty
                    if (!string.IsNullOrEmpty(textBefore))
                    {
                        var beforeBlock = new TextBlockItem(textBefore);
                        beforeBlock.ParentTask = parent;
                        beforeBlock.PropertyChanged += (s, e) => {
                            if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(beforeBlock);
                            _ = SaveDataAsync();
                        };
                        parent.NoteBlocks.Insert(currentInsertIndex++, beforeBlock);
                    }

                    // Insert subtask
                    var sub = new TaskItem(title);
                    sub.ParentTask = parent;
                    if (Tasks.Contains(parent)) sub.PropertyChanged += DailySubTask_PropertyChanged;
                    else if (WeeklyGoals.Contains(parent)) sub.PropertyChanged += SubTask_PropertyChanged;
                    else if (MonthlyGoals.Contains(parent)) sub.PropertyChanged += MonthlySubTask_PropertyChanged;
                    
                    parent.NoteBlocks.Insert(currentInsertIndex++, sub);
                    sub.IsEditing = true;

                    // Insert text after if not empty
                    if (!string.IsNullOrEmpty(textAfter))
                    {
                        var afterBlock = new TextBlockItem(textAfter);
                        afterBlock.ParentTask = parent;
                        afterBlock.PropertyChanged += (s, e) => {
                            if (e.PropertyName == nameof(TextBlockItem.Text)) HandleTextBlockShortcut(afterBlock);
                            _ = SaveDataAsync();
                        };
                        parent.NoteBlocks.Insert(currentInsertIndex++, afterBlock);
                    }

                    _ = SaveDataAsync();
                    return;
                }
            }
        }
        
        private void AddSubTaskAtPosition(TaskItem? parent, int index)
        {
            if (parent == null) return;
            var sub = new TaskItem("");
            // Logic to wire up...
            if (Tasks.Contains(parent)) sub.PropertyChanged += DailySubTask_PropertyChanged;
            else if (WeeklyGoals.Contains(parent)) sub.PropertyChanged += SubTask_PropertyChanged;
            parent.NoteBlocks.Insert(index, sub);
            sub.IsFocused = true;
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void StartEditingBlock(NoteBlock? block)
        {
            if (block == null) return;
            // Clear editing from all other blocks
            if (SelectedTaskForDetails != null)
            {
                foreach (var b in SelectedTaskForDetails.NoteBlocks)
                {
                    b.IsEditing = false;
                }
            }
            block.IsEditing = true;
        }

        [RelayCommand]
        private void StopEditingBlock(NoteBlock? block)
        {
            if (block == null) return;
            block.IsEditing = false;
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void RemoveNoteBlock(NoteBlock? block)
        {
            if (block == null || SelectedTaskForDetails == null) return;

            SelectedTaskForDetails.NoteBlocks.Remove(block);
            SelectedTaskForDetails.UpdateCompletionPercent();
            _ = SaveDataAsync();
        }

        [RelayCommand]
        private void RemoveSubTask(TaskItem? subTask)
        {
            if (subTask == null) return;

            // Find parent goal in daily tasks
            foreach (var task in Tasks)
            {
                if (task.NoteBlocks.Contains(subTask))
                {
                    subTask.PropertyChanged -= DailySubTask_PropertyChanged;
                    
                    // Remove from Markdown notes
                    if (!string.IsNullOrWhiteSpace(task.NotesMarkdown)) 
                    {
                        string target = subTask.Title;
                        string pattern = $@"^(\s*)[\-\*](?:\s*\[[\sXx]\]|\[\])\s*{System.Text.RegularExpressions.Regex.Escape(target)}\r?\n?";
                        task.NotesMarkdown = System.Text.RegularExpressions.Regex.Replace(task.NotesMarkdown, pattern, "", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }

                    task.NoteBlocks.Remove(subTask);
                    _ = SaveDataAsync();
                    return;
                }
            }

            // Find parent goal in weekly goals
            foreach (var goal in WeeklyGoals)
            {
                if (goal.NoteBlocks.Contains(subTask))
                {
                    subTask.PropertyChanged -= SubTask_PropertyChanged;
                    goal.NoteBlocks.Remove(subTask);
                    _ = SaveWeeklyGoalsAsync();
                    return;
                }
            }

            // Find parent goal in monthly goals
            foreach (var goal in MonthlyGoals)
            {
                if (goal.NoteBlocks.Contains(subTask))
                {
                    subTask.PropertyChanged -= MonthlySubTask_PropertyChanged;
                    goal.NoteBlocks.Remove(subTask);
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
                foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                    sub.PropertyChanged -= MonthlySubTask_PropertyChanged;
            }
            MonthlyGoals.Clear();

            var goals = await MarkdownDataManager.LoadMonthlyGoalsAsync(SelectedDate);

            foreach (var g in goals)
            {
                if (string.IsNullOrEmpty(g.Tag))
                {
                    g.Tag = "Mensal";
                    g.TagColor = "#FFD700";
                }
                g.PropertyChanged += MonthlyGoal_PropertyChanged;
                foreach (var sub in g.NoteBlocks.OfType<TaskItem>())
                    sub.PropertyChanged += MonthlySubTask_PropertyChanged;
                MonthlyGoals.Add(g);
            }
        }

        private bool _isSavingMonthly;
        private bool _saveMonthlyPending;

        private async Task SaveMonthlyGoalsAsync()
        {
            if (_isLoading) return;
            if (_isSavingMonthly)
            {
                _saveMonthlyPending = true;
                return;
            }

            _isSavingMonthly = true;
            try
            {
                do
                {
                    _saveMonthlyPending = false;
                    await MarkdownDataManager.SaveMonthlyGoalsAsync(SelectedDate, MonthlyGoals);
                } while (_saveMonthlyPending);
            }
            finally
            {
                _isSavingMonthly = false;
            }
        }

        private void MonthlyGoal_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem goal)
            {
                if (e.PropertyName == nameof(TaskItem.CompletionPercent))
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
                else if (e.PropertyName == nameof(TaskItem.IsCompleted))
                {
                    UpdateLinkedGoalStatus(goal);
                }
                else if (e.PropertyName == nameof(TaskItem.Deadline))
                {
                    OnPropertyChanged(nameof(AllGoalsWithDeadlines));
                }
            }

            _ = SaveMonthlyGoalsAsync();
        }

        private void MonthlySubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is TaskItem subtask && e.PropertyName == nameof(TaskItem.IsCompleted))
            {
                subtask.ParentTask?.UpdateCompletionPercent();
                UpdateLinkedGoalStatus(subtask);
            }

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
                var goal = new TaskItem(NewMonthlyGoalTitle) { Tag = "Mensal", TagColor = "#FFD700" };
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
                foreach (var sub in goal.NoteBlocks.OfType<TaskItem>())
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
            parentGoal.NoteBlocks.Add(sub);
            _ = SaveMonthlyGoalsAsync();
        }

        // --- Contribution Heatmap -----------------------------------

        [ObservableProperty]
        private Dictionary<DateTime, int> contributionHistory = new();

        private async Task LoadContributionHistoryAsync()
        {
            var history = await MarkdownDataManager.LoadContributionHistoryAsync();
            ContributionHistory = history;
        }
    }
}
