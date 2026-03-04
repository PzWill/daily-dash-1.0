using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Collections.Generic;

namespace DailyDash.Models
{
    public partial class TaskItem : NoteBlock
    {
        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string linkedGoalId = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private string notesMarkdown = string.Empty;

        partial void OnNotesMarkdownChanged(string value)
        {
            ParseMarkdownSubtasks(value);
        }

        private bool _isParsingMarkdown;

        private void ParseMarkdownSubtasks(string markdown)
        {
            if (_isParsingMarkdown) return;
            _isParsingMarkdown = true;
            try
            {
                var newChecklists = new List<TaskItem>();
                if (!string.IsNullOrEmpty(markdown))
                {
                    // We no longer coerce * [] to - [] to avoid infinite loops with Milkdown's markdown text sync
                    var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimStart();
                        bool isCheckbox = false;
                        bool isCompleted = false;
                        string title = string.Empty;

                        if (trimmed.StartsWith("* [ ]", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("- [ ]", StringComparison.OrdinalIgnoreCase))
                        {
                            isCheckbox = true; title = trimmed.Substring(5).Trim();
                        }
                        else if (trimmed.StartsWith("* [x]", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
                        {
                            isCheckbox = true; isCompleted = true; title = trimmed.Substring(5).Trim();
                        }
                        else if (trimmed.StartsWith("*[]") || trimmed.StartsWith("-[]"))
                        {
                            isCheckbox = true; title = trimmed.Substring(3).Trim();
                        }
                        else if (trimmed.StartsWith("*[x]", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("-[x]", StringComparison.OrdinalIgnoreCase))
                        {
                            isCheckbox = true; isCompleted = true; title = trimmed.Substring(4).Trim();
                        }

                        if (isCheckbox && !string.IsNullOrWhiteSpace(title))
                        {
                            newChecklists.Add(new TaskItem(title) { IsCompleted = isCompleted, ParentTask = this });
                        }
                    }
                }

                // Smart sync to avoid destroying existing UI objects and losing focus
                var existingTasks = NoteBlocks.OfType<TaskItem>().ToList();
                bool collectionChanged = false;

                // Sync existing
                for (int i = 0; i < existingTasks.Count; i++)
                {
                    if (i < newChecklists.Count)
                    {
                        var existingTask = existingTasks[i];
                        var newData = newChecklists[i];
                        
                        if (existingTask.Title != newData.Title)
                            existingTask.Title = newData.Title;
                            
                        if (existingTask.IsCompleted != newData.IsCompleted)
                        {
                            existingTask.IsCompleted = newData.IsCompleted;
                        }
                    }
                    else
                    {
                        NoteBlocks.Remove(existingTasks[i]);
                        collectionChanged = true;
                    }
                }

                // Add new
                for (int i = existingTasks.Count; i < newChecklists.Count; i++)
                {
                    NoteBlocks.Add(newChecklists[i]);
                    collectionChanged = true;
                }

                if (collectionChanged)
                {
                    UpdateCompletionPercent();
                }
            }
            finally
            {
                _isParsingMarkdown = false;
            }
        }

        [ObservableProperty]
        private ObservableCollection<NoteBlock> noteBlocks = new();

        [ObservableProperty]
        private ObservableCollection<TaskItem> inlineTasks = new();

        /// <summary>
        /// Optional description for the task/goal.
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private bool isCompleted;

        [ObservableProperty]
        private string tag = string.Empty;

        [ObservableProperty]
        private string tagColor = "Transparent";

        /// <summary>
        /// Completion percentage (0–100). Used for weekly/monthly goals with slider.
        /// </summary>
        [ObservableProperty]
        private int completionPercent;

        [ObservableProperty]
        private DateTime? deadline;

        public TaskItem(string title) : this()
        {
            Title = title;
        }

        public TaskItem() 
        { 
            NoteBlocks.CollectionChanged += NoteBlocks_CollectionChanged;
            AttachSubTaskListeners(NoteBlocks);
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(NoteBlocks))
            {
                if (NoteBlocks != null)
                {
                    NoteBlocks.CollectionChanged -= NoteBlocks_CollectionChanged;
                    NoteBlocks.CollectionChanged += NoteBlocks_CollectionChanged;
                    AttachSubTaskListeners(NoteBlocks);
                }
                UpdateCompletionPercent();
            }
            if (e.PropertyName == nameof(IsCompleted) || e.PropertyName == nameof(Title))
            {
                // This informs the parent if this item itself is a sub-task
                if (e.PropertyName == nameof(IsCompleted))
                {
                    ParentTask?.UpdateCompletionPercent();
                }
                ParentTask?.SyncSubtaskToMarkdown(this);
            }
        }

        public void SyncSubtaskToMarkdown(TaskItem subtask)
        {
            if (_isParsingMarkdown || string.IsNullOrWhiteSpace(subtask.Title) || string.IsNullOrEmpty(NotesMarkdown)) return;

            var subtasks = NoteBlocks.OfType<TaskItem>().ToList();
            int subtaskIndex = subtasks.IndexOf(subtask);
            if (subtaskIndex < 0) return;

            var lines = NotesMarkdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            int currentCheckboxIndex = 0;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^(\s*)([\-\*])\s*\[([ xX])\]\s*(.*)$");
                if (match.Success)
                {
                    if (currentCheckboxIndex == subtaskIndex)
                    {
                        char currentCheck = match.Groups[3].Value[0];
                        bool isCurrentlyCompleted = char.ToLower(currentCheck) == 'x';
                        string oldTitle = match.Groups[4].Value.Trim();

                        bool needsUpdate = false;
                        string newCheck = isCurrentlyCompleted ? "x" : " ";

                        if (isCurrentlyCompleted != subtask.IsCompleted)
                        {
                            newCheck = subtask.IsCompleted ? "x" : " ";
                            needsUpdate = true;
                        }
                        
                        if (oldTitle != subtask.Title)
                        {
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            lines[i] = $"{match.Groups[1].Value}{match.Groups[2].Value} [{newCheck}] {subtask.Title}";
                            changed = true;
                        }
                        break;
                    }
                    currentCheckboxIndex++;
                }
            }

            if (changed)
            {
                _isParsingMarkdown = true;
                try
                {
                    NotesMarkdown = string.Join("\n", lines);
                }
                finally
                {
                    _isParsingMarkdown = false;
                }
            }
        }

        private void NoteBlocks_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                AttachSubTaskListeners(e.NewItems.Cast<NoteBlock>());
            }
            if (e.OldItems != null)
            {
                DetachSubTaskListeners(e.OldItems.Cast<NoteBlock>());
            }
            UpdateCompletionPercent();
        }

        private void AttachSubTaskListeners(IEnumerable<NoteBlock> items)
        {
            foreach (var item in items)
            {
                item.ParentTask = this;
            }
        }

        private void DetachSubTaskListeners(IEnumerable<NoteBlock> items)
        {
            foreach (var item in items)
            {
                if (item.ParentTask == this)
                {
                    item.ParentTask = null;
                }
            }
        }

        public void UpdateCompletionPercent()
        {
            int totalSub = 0;
            int completedSub = 0;

            if (NoteBlocks != null)
            {
                var subBlocks = NoteBlocks.OfType<TaskItem>().ToList();
                if (subBlocks.Count > 0)
                {
                    totalSub += subBlocks.Count;
                    completedSub += subBlocks.Count(s => s.IsCompleted);
                }
            }

            if (totalSub == 0) 
            {
                CompletionPercent = 0;
                return;
            }

            CompletionPercent = (int)Math.Round((double)completedSub / totalSub * 100);
        }
    }
}

