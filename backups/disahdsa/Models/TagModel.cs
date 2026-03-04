using CommunityToolkit.Mvvm.ComponentModel;

namespace DailyDash.Models
{
    public partial class TagModel : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string colorHex = "#8E8E93";

        public override string ToString() => Name;

        public TagModel Clone() => new TagModel { Name = this.Name, ColorHex = this.ColorHex };
    }
}
