using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Markup.Xaml;
using Avalonia.Input;

namespace MKVCleaver2
{
    public partial class MainWindow : Window
    {
        private List<String> _extractCommands = new List<String>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Setup drag and drop
            var filesBorder = this.FindControl<Border>("filesBorder");
            if (filesBorder != null)
            {
                DragDrop.SetAllowDrop(filesBorder, true);
                filesBorder.AddHandler(DragDrop.DragOverEvent, Border_DragOver);
                filesBorder.AddHandler(DragDrop.DropEvent, Border_Drop);
                filesBorder.AddHandler(DragDrop.DragEnterEvent, DragEnter);
                filesBorder.AddHandler(DragDrop.DragLeaveEvent, DragLeave);
            }
            
            SettingsHelper.Init();
            if (String.IsNullOrEmpty(SettingsHelper.GetToolnixPath()))
            {
                // TODO: Show message about MKVToolnix not found
            }
        }

        #region Non-Control Methods

        private void ToggleButtonState()
        {
            var tvFiles = this.FindControl<TreeView>("tvFiles");
            var btnRemoveFile = this.FindControl<Button>("btnRemoveFile");
            var btnExtract = this.FindControl<Button>("btnExtract");

            if (tvFiles?.ItemCount > 0)
            {
                btnRemoveFile!.IsEnabled = true;
                btnExtract!.IsEnabled = true;
            }
            else
            {
                btnRemoveFile!.IsEnabled = false;
                btnExtract!.IsEnabled = false;
            }
        }

        #endregion

        private async void btnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open MKV Files",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("MKV Files") { Patterns = new[] { "*.mkv" } } }
            });

            if (files.Count > 0)
            {
                await ProcessMkvFiles(files);
            }
        }

        private void btnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            var tvFiles = this.FindControl<TreeView>("tvFiles");

            if (tvFiles?.SelectedItem != null)
            {
                var items = (ObservableCollection<MkvFile>)tvFiles.ItemsSource!;
                items.Remove((MkvFile)tvFiles.SelectedItem);
            }
            ToggleButtonState();
        }

        private async void btnLocateToolnix_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select MKVToolnix Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var selectedPath = folders[0].Path.LocalPath;
                var mkvInfoPath = Path.Combine(selectedPath, "mkvinfo");
                var mkvExtractPath = Path.Combine(selectedPath, "mkvextract");

                if (File.Exists(mkvInfoPath) && File.Exists(mkvExtractPath))
                {
                    SettingsHelper.SetToolnixPath(selectedPath);
                }
                else
                {
                    // TODO: Show message box about utilities not found
                    Console.WriteLine("MKVToolnix utilities not found in selected directory");
                }
            }
        }

        private void btnExtract_Click(object sender, RoutedEventArgs e)
        {
            var tvFiles = this.FindControl<TreeView>("tvFiles");
            var lbBatchTracksToExtract = this.FindControl<ListBox>("lbBatchTracksToExtract");
            var tboutputDir = this.FindControl<TextBox>("tboutputDir");
            var pbCurrentFile = this.FindControl<ProgressBar>("pbCurrentFile");

            if (tvFiles?.ItemsSource is ObservableCollection<MkvFile> files)
            {
                foreach (var file in files)
                {
                    if (file.IsSelected == true)
                    {
                        if (lbBatchTracksToExtract?.ItemsSource is ObservableCollection<BatchTrack> batchTracks)
                        {
                            var selectedTracks = batchTracks.Where(bt => bt.IsSelected).ToList();

                            if (selectedTracks.Any())
                            {
                                string commandstr = string.Empty;

                                foreach (var track in selectedTracks)
                                {
                                    string outputPath;
                                    string baseFileName = Path.GetFileNameWithoutExtension(file.Name);
                                    string extension = SettingsHelper.GetCodecContainerExtension(track.Track.Codec);

                                    if (string.IsNullOrEmpty(tboutputDir?.Text))
                                    {
                                        string standartPath = Path.GetDirectoryName(file.Path) + "/";
                                        outputPath = $"{standartPath}{baseFileName}_Track{track.Track.Number}_{track.Track.Type}_{track.Track.Language}{extension}";
                                    }
                                    else
                                    {
                                        outputPath = $"{tboutputDir.Text}/{baseFileName}_Track{track.Track.Number}_{track.Track.Type}_{track.Track.Language}{extension}";
                                    }

                                    commandstr += track.Track.Number.ToString() + ":\"" + outputPath + "\" ";
                                }

                                var proc = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = SettingsHelper.GetMkvExtractPath(),
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        CreateNoWindow = true,
                                        Arguments = " tracks \"" + file.Path + "\" " + commandstr,
                                        StandardOutputEncoding = Encoding.UTF8
                                    }
                                };

                                proc.Start();

                                StreamReader sr = proc.StandardOutput;
                                string? buffstr;

                                while ((buffstr = sr.ReadLine()) != null)
                                {
                                    if (buffstr.Contains("Progress: "))
                                    {
                                        if (buffstr.Length >= 12)
                                        {
                                            var progressStr = buffstr.Substring(10);
                                            var percentIndex = progressStr.IndexOf('%');
                                            if (percentIndex > 0)
                                            {
                                                var progressValue = progressStr.Substring(0, percentIndex);
                                                if (int.TryParse(progressValue, out int progress))
                                                {
                                                    pbCurrentFile!.Value = progress;
                                                }
                                            }
                                        }
                                    }
                                }

                                proc.WaitForExit();
                            }
                        }
                    }
                }
            }
        }

        #region Drag and Drop

        private void DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                bool hasMkvFiles = false;
                
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file.Path.LocalPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                        {
                            hasMkvFiles = true;
                            break;
                        }
                    }
                }
                
                if (hasMkvFiles)
                {
                    var filesBorder = this.FindControl<Border>("filesBorder");
                    if (filesBorder != null)
                    {
                        filesBorder.Background = Avalonia.Media.Brushes.LightBlue;
                        filesBorder.Opacity = 0.8;
                    }
                }
            }
        }

        private void DragLeave(object? sender, DragEventArgs e)
        {
            var filesBorder = this.FindControl<Border>("filesBorder");
            if (filesBorder != null)
            {
                filesBorder.Background = Avalonia.Media.Brush.Parse("#00000000");
                filesBorder.Opacity = 1.0;
            }
        }

        private void Border_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                bool hasMkvFiles = false;
                
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (file.Path.LocalPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                        {
                            hasMkvFiles = true;
                            break;
                        }
                    }
                }
                
                if (hasMkvFiles)
                {
                    e.DragEffects = DragDropEffects.Copy;
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                }
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private async void Border_Drop(object? sender, DragEventArgs e)
        {
            var filesBorder = this.FindControl<Border>("filesBorder");
            if (filesBorder != null)
            {
                filesBorder.Background = Avalonia.Media.Brush.Parse("#00000000");
                filesBorder.Opacity = 1.0;
            }
            
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                if (files != null)
                {
                    var mkvFiles = files.Where(f => f.Path.LocalPath.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                                       .Cast<IStorageFile>()
                                       .ToList();
                    
                    if (mkvFiles.Any())
                    {
                        await ProcessMkvFiles(mkvFiles);
                    }
                }
            }
        }

        #endregion

        #region File Processing

        private async Task ProcessMkvFiles(IEnumerable<IStorageFile> files)
        {
            var items = new List<MkvFile>();

            foreach (var file in files)
            {
                string fileName = file.Path.LocalPath;
                
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = SettingsHelper.GetMkvInfoPath(),
                        Arguments = "\"" + fileName + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                proc.Start();

                String output = "";
                while (!proc.StandardOutput.EndOfStream)
                {
                    output += proc.StandardOutput.ReadLine() + "\n";
                }
                proc.WaitForExit();

                var item = new MkvFile();
                item.Path = fileName;
                item.Name = Path.GetFileName(fileName);
                item.Tracks = new EbmlParser().Parse(output);
                item.IsSelected = true;
                
                foreach (var track in item.Tracks)
                {
                    track.Parent = item;
                }
                
                items.Add(item);
            }

            List<Track> commonTracks = new List<Track>();
            
            if (items.Count > 0)
            {
                if (items.Count == 1)
                {
                    commonTracks.AddRange(items[0].Tracks);
                }
                else
                {
                    commonTracks.AddRange(items.First().Tracks);
                    foreach (var mkvFile in items.Skip(1))
                    {
                        commonTracks = commonTracks.Intersect(mkvFile.Tracks, new TrackComparer()).ToList();
                    }
                }
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tvFiles = this.FindControl<TreeView>("tvFiles");
                var lbBatchTracksToExtract = this.FindControl<ListBox>("lbBatchTracksToExtract");

                var existingItems = tvFiles!.ItemsSource as ObservableCollection<MkvFile>;
                if (existingItems == null)
                {
                    existingItems = new ObservableCollection<MkvFile>();
                    tvFiles.ItemsSource = existingItems;
                }

                foreach (var item in items)
                {
                    existingItems.Add(item);
                }

                ObservableCollection<BatchTrack> batchTracks = new ObservableCollection<BatchTrack>();
                foreach (var track in commonTracks)
                {
                    var batchTrack = new BatchTrack
                    {
                        Name = string.Format("{0}, {1} ({2})", track.Type, track.Language, track.Name),
                        Track = track,
                        IsSelected = true
                    };
                    batchTracks.Add(batchTrack);
                }

                lbBatchTracksToExtract!.ItemsSource = batchTracks;
                ToggleButtonState();
            });
        }

        #endregion
    }
}