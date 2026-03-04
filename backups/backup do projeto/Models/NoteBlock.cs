using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace DailyDash.Models
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(TaskItem), typeDiscriminator: "TaskItem")]
    [JsonDerivedType(typeof(TextBlockItem), typeDiscriminator: "TextBlockItem")]
    [JsonDerivedType(typeof(SubTaskBlockItem), typeDiscriminator: "SubTaskBlockItem")]
    public abstract partial class NoteBlock : ObservableObject
    {
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isFocused;

        [ObservableProperty]
        [property: JsonIgnore]
        private bool isEditing;

        [JsonIgnore]
        public TaskItem? ParentTask { get; set; }

        [ObservableProperty]
        private string id = Guid.NewGuid().ToString("N");
    }

    public partial class TextBlockItem : NoteBlock
    {
        [ObservableProperty]
        private string text = string.Empty;

        public TextBlockItem() { }

        public TextBlockItem(string text)
        {
            this.text = text;
        }
    }

    public partial class SubTaskBlockItem : NoteBlock
    {
        [ObservableProperty]
        private bool isCompleted;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private int completionPercent;

        public SubTaskBlockItem() { }
        public SubTaskBlockItem(string title, bool isCompleted, int percent)
        {
            Title = title;
            IsCompleted = isCompleted;
            CompletionPercent = percent;
        }
    }
}
