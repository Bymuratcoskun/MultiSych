using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MultiSych.Desktop.Views
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog()
        {
            InitializeComponent();
            
            CancelButton.Click += CancelButton_Click;
            ConfirmButton.Click += ConfirmButton_Click;
        }

        public Task<bool> ShowAsync(Window parent, string message)
        {
            MessageText.Text = message;
            return ShowDialog<bool>(parent);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }
    }
}