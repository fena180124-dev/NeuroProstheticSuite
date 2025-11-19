using System.Windows.Controls;

namespace NeuroProstheticSuite
{
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView() : this("Placeholder") { }

        public PlaceholderView(string title)
        {
            InitializeComponent();
            TitleText.Text = title;
            BodyText.Text = $"'{title}' belum diimplementasikan.";
        }
    }
}