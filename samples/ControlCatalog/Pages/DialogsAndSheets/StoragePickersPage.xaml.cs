using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Dialogs;
using Avalonia.Platform.Storage;

namespace ControlCatalog.Pages
{
    public partial class StoragePickersPage : UserControl
    {
        public StoragePickersPage()
        {
            InitializeComponent();

            IStorageFolder? lastSelectedDirectory = null;
            IStorageItem? lastSelectedItem = null;
            bool ignoreTextChanged = false;
            bool pickerOperationInProgress = false;
            var startLocationVersion = 0;

            var results = PickerLastResults;
            var resultsVisible = PickerLastResultsVisible;
            var bookmarkContainer = BookmarkContainer;
            var openedFileContent = OpenedFileContent;
            var openMultiple = OpenMultiple;
            var currentFolderBox = CurrentFolderBox;
            var useSuggestedFilter = UseSuggestedFilter;
            var suggestedFilterSelector = SuggestedFilterSelector;

            async Task UpdateStartLocationAsync()
            {
                if (ignoreTextChanged)
                    return;

                var version = ++startLocationVersion;
                var text = currentFolderBox.Text;
                await Task.Delay(180);
                if (version != startLocationVersion)
                    return;

                try
                {
                    IStorageFolder? resolvedFolder = null;
                    if (Enum.TryParse<WellKnownFolder>(text, true, out var folderEnum))
                    {
                        resolvedFolder = await GetStorageProvider().TryGetWellKnownFolderAsync(folderEnum);
                    }
                    else if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (!Uri.TryCreate(text, UriKind.Absolute, out var folderLink))
                        {
                            Uri.TryCreate("file://" + text, UriKind.Absolute, out folderLink);
                        }

                        if (folderLink is not null)
                            resolvedFolder = await GetStorageProvider().TryGetFolderFromPathAsync(folderLink);
                    }

                    if (version == startLocationVersion)
                        lastSelectedDirectory = resolvedFolder;
                }
                catch (Exception exception)
                {
                    if (version == startLocationVersion)
                        openedFileContent.Text = $"Start location is unavailable: {exception.Message}";
                }
            }

            async Task RunPickerOperationAsync(Func<Task> operation)
            {
                if (pickerOperationInProgress)
                {
                    openedFileContent.Text = "A picker operation is already in progress.";
                    return;
                }

                pickerOperationInProgress = true;
                try
                {
                    await operation();
                }
                catch (OperationCanceledException)
                {
                    openedFileContent.Text = "The picker operation was canceled.";
                }
                catch (Exception exception)
                {
                    openedFileContent.Text = $"The picker operation failed: {exception.Message}";
                }
                finally
                {
                    pickerOperationInProgress = false;
                }
            }

            async Task RunLauncherOperationAsync(Func<Task> operation)
            {
                try
                {
                    await operation();
                }
                catch (OperationCanceledException)
                {
                    LaunchStatus.Text = "The launch request was canceled.";
                }
                catch (Exception exception)
                {
                    LaunchStatus.Text = $"The launch request failed: {exception.Message}";
                }
            }

            currentFolderBox.TextChanged += (_, _) => _ = UpdateStartLocationAsync();

            List<FilePickerFileType>? BuildFileTypes()
            {
                var selectedItem = (FilterSelector.SelectedItem as ComboBoxItem)?.Content
                    ?? "None";

                var binLogType = new FilePickerFileType("Binary Log")
                {
                    Patterns = new[] { "*.binlog", "*.buildlog" },
                    MimeTypes = new[] { "application/binlog", "application/buildlog" },
                    AppleUniformTypeIdentifiers = new[] { "public.data" }
                };

                return selectedItem switch
                {
                    "All + TXT + BinLog" => new List<FilePickerFileType>
                    {
                        FilePickerFileTypes.All, FilePickerFileTypes.TextPlain, binLogType
                    },
                    "Binlog" => new List<FilePickerFileType> { binLogType },
                    "TXT extension only" => new List<FilePickerFileType>
                    {
                        new("TXT") { Patterns = FilePickerFileTypes.TextPlain.Patterns }
                    },
                    "TXT mime only" => new List<FilePickerFileType>
                    {
                        new("TXT") { MimeTypes = FilePickerFileTypes.TextPlain.MimeTypes }
                    },
                    "TXT apple type id only" => new List<FilePickerFileType>
                    {
                        new("TXT")
                        {
                            AppleUniformTypeIdentifiers =
                                FilePickerFileTypes.TextPlain.AppleUniformTypeIdentifiers
                        }
                    },
                    _ => null
                };
            }

            List<FilePickerFileType>? GetFileTypes()
            {
                var types = BuildFileTypes();
                UpdateSuggestedFilterSelector(types);
                return types;
            }

            void UpdateSuggestedFilterSelector(IReadOnlyList<FilePickerFileType>? types)
            {
                var previouslySelected = (suggestedFilterSelector.SelectedItem as ComboBoxItem)?.Tag as FilePickerFileType;
                suggestedFilterSelector.Items.Clear();
                suggestedFilterSelector.Items.Add(new ComboBoxItem { Content = "First filter", Tag = null });

                var desiredIndex = 0;
                if (types is { Count: > 0 })
                {
                    for (var i = 0; i < types.Count; i++)
                    {
                        var type = types[i];
                        var item = new ComboBoxItem { Content = type.Name, Tag = type };
                        suggestedFilterSelector.Items.Add(item);

                        if (previouslySelected is not null && ReferenceEquals(previouslySelected, type))
                        {
                            desiredIndex = i + 1;
                        }
                    }
                }

                suggestedFilterSelector.SelectedIndex = desiredIndex;
            }

            FilePickerFileType? GetSuggestedFileType(IReadOnlyList<FilePickerFileType>? types)
            {
                if (useSuggestedFilter.IsChecked == true && types is { Count: > 0 })
                {
                    if (suggestedFilterSelector.SelectedItem is ComboBoxItem { Tag: FilePickerFileType selectedType }
                        && types.Any(t => ReferenceEquals(t, selectedType)))
                    {
                        return selectedType;
                    }

                    return types.FirstOrDefault();
                }

                return null;
            }

            void UpdateSuggestedFilterSelectorState() =>
                suggestedFilterSelector.IsEnabled = useSuggestedFilter.IsChecked == true;

            useSuggestedFilter.IsCheckedChanged += (_, _) => UpdateSuggestedFilterSelectorState();
            UpdateSuggestedFilterSelectorState();

            FilterSelector.SelectionChanged += (_, _) => UpdateSuggestedFilterSelector(BuildFileTypes());
            UpdateSuggestedFilterSelector(BuildFileTypes());

            OpenFilePicker.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var fileTypes = GetFileTypes();
                var result = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = "Open file",
                    FileTypeFilter = fileTypes,
                    SuggestedFileType = GetSuggestedFileType(fileTypes),
                    SuggestedFileName = "FileName",
                    SuggestedStartLocation = lastSelectedDirectory,
                    AllowMultiple = openMultiple.IsChecked == true
                });

                await SetPickerResult(result);
            });
            SaveFilePicker.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var fileTypes = GetFileTypes();
                var suggestedType = GetSuggestedFileType(fileTypes);
                var file = await GetStorageProvider().SaveFilePickerAsync(new FilePickerSaveOptions()
                {
                    Title = "Save file",
                    FileTypeChoices = fileTypes,
                    SuggestedFileType = suggestedType,
                    SuggestedStartLocation = lastSelectedDirectory,
                    SuggestedFileName = "FileName",
                    ShowOverwritePrompt = true
                });

                if (file is not null)
                {
                    try
                    {
                        // Sync disposal of StreamWriter is not supported on WASM
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new System.IO.StreamWriter(stream);
                        await writer.WriteLineAsync(openedFileContent.Text);

                        SetFolder(await file.GetParentAsync());
                    }
                    catch (Exception ex)
                    {
                        openedFileContent.Text = ex.ToString();
                    }
                }

                await SetPickerResult(file is null ? null : new[] { file });
            });
            SaveFilePickerWithResult.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var saveFileTypes = new[] { FilePickerFileTypes.Json, FilePickerFileTypes.Xml };
                var result = await GetStorageProvider().SaveFilePickerWithResultAsync(new FilePickerSaveOptions()
                {
                    Title = "Save file",
                    FileTypeChoices = saveFileTypes,
                    SuggestedFileType = GetSuggestedFileType(saveFileTypes),
                    SuggestedStartLocation = lastSelectedDirectory,
                    SuggestedFileName = "FileName",
                    ShowOverwritePrompt = true
                });

                try
                {
                    if (result.File is { } file)
                    {
                        // Sync disposal of StreamWriter is not supported on WASM
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new System.IO.StreamWriter(stream);
                        if (result.SelectedFileType == FilePickerFileTypes.Xml)
                        {
                            await writer.WriteLineAsync("<sample>Test</sample>");
                        }
                        else
                        {
                            await writer.WriteLineAsync("""{ "sample": "Test" }""");
                        }

                        SetFolder(await result.File.GetParentAsync());
                    }
                }
                catch (Exception ex)
                {
                    openedFileContent.Text = ex.ToString();
                }

                await SetPickerResult(result.File is null ? null : new[] { result.File }, result.SelectedFileType);
            });
            OpenFolderPicker.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var folders = await GetStorageProvider().OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = "Folder file",
                    SuggestedStartLocation = lastSelectedDirectory,
                    SuggestedFileName = "FileName",
                    AllowMultiple = openMultiple.IsChecked == true
                });

                await SetPickerResult(folders);
            });
            OpenFileFromBookmark.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var file = bookmarkContainer.Text is not null
                    ? await GetStorageProvider().OpenFileBookmarkAsync(bookmarkContainer.Text)
                    : null;

                await SetPickerResult(file is null ? null : new[] { file });
            });
            OpenFolderFromBookmark.Click += (_, _) => _ = RunPickerOperationAsync(async () =>
            {
                var folder = bookmarkContainer.Text is not null
                    ? await GetStorageProvider().OpenFolderBookmarkAsync(bookmarkContainer.Text)
                    : null;

                await SetPickerResult(folder is null ? null : new[] { folder });
            });

            LaunchUri.Click += (_, _) => _ = RunLauncherOperationAsync(async () =>
            {
                if (Uri.TryCreate(UriToLaunch.Text, UriKind.Absolute, out var uri))
                {
                    var result = await TopLevel.GetTopLevel(this)!.Launcher.LaunchUriAsync(uri);
                    LaunchStatus.Text = result
                        ? "The URI was launched successfully."
                        : "The platform declined the URI launch request.";
                }
                else
                {
                    LaunchStatus.Text = "Enter a valid absolute URI.";
                }
            });

            LaunchFile.Click += (_, _) => _ = RunLauncherOperationAsync(async () =>
            {
                if (lastSelectedItem is not null)
                {
                    var result = await TopLevel.GetTopLevel(this)!.Launcher.LaunchFileAsync(lastSelectedItem);
                    LaunchStatus.Text = result
                        ? "The selected item was launched successfully."
                        : "The platform declined the file launch request.";
                }
                else
                {
                    LaunchStatus.Text = "Select a file or folder before launching it.";
                }
            });

            void SetFolder(IStorageFolder? folder)
            {
                ignoreTextChanged = true;
                lastSelectedDirectory = folder;
                lastSelectedItem = folder;
                currentFolderBox.Text = folder?.Path is { IsAbsoluteUri: true } abs ? abs.LocalPath : folder?.Path?.ToString();
                ignoreTextChanged = false;
            }
            async Task SetPickerResult(IReadOnlyCollection<IStorageItem>? items, FilePickerFileType? selectedType = null)
            {
                items ??= Array.Empty<IStorageItem>();
                bookmarkContainer.Text = items.FirstOrDefault(f => f.CanBookmark) is { } f
                    ? await f.SaveBookmarkAsync()
                    : "Bookmark unavailable";
                var mappedResults = new List<string>();

                var resultText = items.Count == 0 ? "No item was selected." : string.Empty;
                if (items.FirstOrDefault() is IStorageItem item)
                {
                    resultText += item is IStorageFile ? "File:" : "Folder:";
                    resultText += Environment.NewLine;

                    var props = await item.GetBasicPropertiesAsync();
                    resultText += @$"Size: {props.Size}
            DateCreated: {props.DateCreated}
            DateModified: {props.DateModified}
            CanBookmark: {item.CanBookmark}
            ";
                    if (item is IStorageFile file)
                    {
                        resultText += @$"
            Content:
            ";

                        try
                        {
                            resultText += await ReadTextFromFile(file, 500);
                        }
                        catch (Exception ex)
                        {
                            resultText += ex.ToString();
                        }
                    }

                    if (item is IStorageFolder storageFolder)
                    {
                        SetFolder(storageFolder);
                    }
                    else
                    {
                        var parent = await item.GetParentAsync();
                        SetFolder(parent);
                        if (parent is not null)
                        {
                            mappedResults.Add(FullPathOrName(parent));
                        }
                    }

                    foreach (var selectedItem in items)
                    {
                        mappedResults.Add("+> " + FullPathOrName(selectedItem));
                        if (selectedItem is IStorageFolder folder)
                        {
                            await foreach (var innerItem in folder.GetItemsAsync())
                            {
                                mappedResults.Add("++> " + FullPathOrName(innerItem));
                            }
                        }
                    }
                    lastSelectedItem = item;
                }

                if (selectedType is not null)
                {
                    resultText += Environment.NewLine + "Selected type: " + selectedType.Name;
                }

                openedFileContent.Text = resultText;
                results.ItemsSource = mappedResults;
                resultsVisible.IsVisible = mappedResults.Any();
            }
        }

        internal static async Task<string> ReadTextFromFile(IStorageFile file, int length)
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);

            // 4GB file test, shouldn't load more than 10000 chars into a memory.
            var buffer = System.Buffers.ArrayPool<char>.Shared.Rent(length);
            try
            {
                var charsRead = await reader.ReadAsync(buffer, 0, length);
                return new string(buffer, 0, charsRead);
            }
            finally
            {
                System.Buffers.ArrayPool<char>.Shared.Return(buffer);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var openedFileContent = OpenedFileContent;
            try
            {
                var storageProvider = GetStorageProvider();
                openedFileContent.Text = $@"CanOpen: {storageProvider.CanOpen}
CanSave: {storageProvider.CanSave}
CanPickFolder: {storageProvider.CanPickFolder}";
            }
            catch (Exception ex)
            {
                openedFileContent.Text = "Storage provider is not available: " + ex.Message;
            }
        }

        private IStorageProvider GetStorageProvider()
        {
            var forceManaged = ForceManaged.IsChecked ?? false;
            return forceManaged
                ? new ManagedStorageProvider(GetWindow())
                : GetTopLevel().StorageProvider;
        }

        private static string FullPathOrName(IStorageItem? item)
        {
            if (item is null) return "(null)";
            return item.Path is { IsAbsoluteUri: true } path ? path.ToString() : item.Name;
        }

        Window GetWindow() => TopLevel.GetTopLevel(this) as Window ?? throw new NullReferenceException("Invalid Owner");
        TopLevel GetTopLevel() => TopLevel.GetTopLevel(this) ?? throw new NullReferenceException("Invalid Owner");
    }
}
