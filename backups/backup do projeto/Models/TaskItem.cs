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
            if (e.PropertyName == nameof(IsCompleted))
            {
                // This informs the parent if this item itself is a sub-task
                ParentTask?.UpdateCompletionPercent();
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

