using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates command and view-model integration for ContentDialog.</summary>
    public partial class ContentDialogCommandsPage : UserControl
    {
        public ContentDialogCommandsPage()
        {
            InitializeComponent();
        }

        private async void OnShowDialog(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var viewModel = new DialogCommandViewModel(
                InitialProjectBox.Text ?? string.Empty,
                message => StatusText.Text = message);
            var projectName = new TextBox { PlaceholderText = "Required project name" };
            var includeReadme = new CheckBox { Content = "Create a README" };
            var summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
            AutomationProperties.SetName(projectName, "Project name");

            projectName.Bind(
                TextBox.TextProperty,
                new Binding(nameof(DialogCommandViewModel.ProjectName))
                {
                    Source = viewModel,
                    Mode = BindingMode.TwoWay
                });
            includeReadme.Bind(
                CheckBox.IsCheckedProperty,
                new Binding(nameof(DialogCommandViewModel.IncludeReadme))
                {
                    Source = viewModel,
                    Mode = BindingMode.TwoWay
                });
            summary.Bind(
                TextBlock.TextProperty,
                new Binding(nameof(DialogCommandViewModel.Summary))
                {
                    Source = viewModel
                });

            var dialog = new ContentDialog
            {
                Title = "Create project",
                Content = new StackPanel
                {
                    Spacing = 10,
                    MaxWidth = 440,
                    Children = { projectName, includeReadme, summary }
                },
                PrimaryButtonContent = "Save",
                SecondaryButtonContent = "Preview",
                CloseButtonContent = "Cancel",
                PrimaryButtonCommand = viewModel.SaveCommand,
                PrimaryButtonCommandParameter = "primary:save",
                SecondaryButtonCommand = viewModel.PreviewCommand,
                SecondaryButtonCommandParameter = "secondary:preview",
                CloseButtonCommand = viewModel.CancelCommand,
                CloseButtonCommandParameter = "close:cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync(owner);
            var resolvedProjectName = string.IsNullOrWhiteSpace(viewModel.ProjectName)
                ? "Untitled"
                : viewModel.ProjectName.Trim();
            var readmeDescription = viewModel.IncludeReadme == true
                ? " with a README"
                : " without a README";
            StatusText.Text = result switch
            {
                ContentDialogResult.Primary => $"Project '{resolvedProjectName}' saved{readmeDescription}.",
                ContentDialogResult.Secondary => $"Preview prepared for project '{resolvedProjectName}'{readmeDescription}.",
                ContentDialogResult.Close => "Project creation canceled.",
                _ => "The dialog was dismissed without running an action."
            };
            StatusDetailText.Text = $"{result} · {viewModel.LastCommand ?? "No command"}";
        }

        private sealed class DialogCommandViewModel : INotifyPropertyChanged
        {
            private readonly Action<string> _report;
            private readonly SampleCommand _saveCommand;
            private string _projectName;
            private bool? _includeReadme = true;
            private string? _lastCommand;

            public DialogCommandViewModel(string projectName, Action<string> report)
            {
                _projectName = projectName;
                _report = report;
                _saveCommand = new SampleCommand(
                    Execute,
                    _ => !string.IsNullOrWhiteSpace(ProjectName));
                PreviewCommand = new SampleCommand(Execute);
                CancelCommand = new SampleCommand(Execute);
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public string ProjectName
            {
                get => _projectName;
                set
                {
                    if (_projectName == value)
                        return;

                    _projectName = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(Summary));
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }

            public bool? IncludeReadme
            {
                get => _includeReadme;
                set
                {
                    if (_includeReadme == value)
                        return;

                    _includeReadme = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(Summary));
                }
            }

            public string Summary => string.IsNullOrWhiteSpace(ProjectName)
                ? "Enter a project name to enable Save."
                : $"Project '{ProjectName.Trim()}' will be created " +
                  (IncludeReadme == true ? "with a README." : "without a README.");

            public string? LastCommand
            {
                get => _lastCommand;
                private set
                {
                    _lastCommand = value;
                    RaisePropertyChanged();
                }
            }

            public ICommand SaveCommand => _saveCommand;

            public ICommand PreviewCommand { get; }

            public ICommand CancelCommand { get; }

            private void Execute(object? parameter)
            {
                LastCommand = parameter?.ToString() ?? "<null>";
                _report($"Command executed: {LastCommand}.");
            }

            private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class SampleCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Predicate<object?> _canExecute;

            public SampleCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute ?? (_ => true);
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => _canExecute(parameter);

            public void Execute(object? parameter) => _execute(parameter);

            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
