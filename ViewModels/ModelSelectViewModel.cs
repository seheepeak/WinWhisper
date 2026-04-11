using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;
using Whisper.net.Ggml;
using WinWhisper.Common.Extensions;
using WinWhisper.Services.Abstractions;

namespace WinWhisper.ViewModels;

/// <summary>
/// ViewModel for ModelSelect window
/// </summary>
public class ModelSelectViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ModelItem> Models { get; set; }

    public event EventHandler<GgmlType>? SelectedModelChanged;

    private readonly IModelService _modelService;

    public ModelSelectViewModel(IModelService modelService)
    {
        _modelService = modelService;
        Models =
        [
            // Sorted by size/capability roughly
            new ModelItem(this, _modelService, "Whisper Large V3", "2.9 GB", GgmlType.LargeV3),
            new ModelItem(this, _modelService, "Whisper Large V3 Turbo", "1.5 GB", GgmlType.LargeV3Turbo),
            new ModelItem(this, _modelService, "Whisper Large V2", "2.9 GB", GgmlType.LargeV2),
            new ModelItem(this, _modelService, "Whisper Large V1", "2.9 GB", GgmlType.LargeV1),

            new ModelItem(this, _modelService, "Whisper Medium", "1.5 GB", GgmlType.Medium),
            new ModelItem(this, _modelService, "Whisper Medium (English)", "1.5 GB", GgmlType.MediumEn),

            new ModelItem(this, _modelService, "Whisper Small", "466 MB", GgmlType.Small),
            new ModelItem(this, _modelService, "Whisper Small (English)", "466 MB", GgmlType.SmallEn),

            new ModelItem(this, _modelService, "Whisper Base", "142 MB", GgmlType.Base),
            new ModelItem(this, _modelService, "Whisper Base (English)", "142 MB", GgmlType.BaseEn),

            new ModelItem(this, _modelService, "Whisper Tiny", "75 MB", GgmlType.Tiny),
            new ModelItem(this, _modelService, "Whisper Tiny (English)", "75 MB", GgmlType.TinyEn),
        ];

    }

    public void SelectModelByGgmlType(GgmlType ggmlType)
    {
        var model = Models.FirstOrDefault(m => m.GgmlType == ggmlType);
        if (model != null)
        {
            SelectModel(model);
        }
    }

    public void SelectModel(ModelItem model)
    {
        // Radio button logic: If already selected, do nothing.
        if (model.IsSelected)
        {
            return;
        }

        if (!model.IsDownloaded)
        {
            return;
        }

        // Deselect all other models
        foreach (var m in Models)
        {
            if (m != model)
            {
                m.IsSelected = false;
            }
        }
        // Select this model
        model.IsSelected = true;
        SelectedModelChanged?.Invoke(this, model.GgmlType);
    }

    public void CancelAllDownloads()
    {
        foreach (var model in Models)
        {
            if (model.IsDownloading)
            {
                model.CancelDownloadCommand.Execute(null);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Model class representing a single model item
/// </summary>
public class ModelItem : INotifyPropertyChanged
{
    private readonly ModelSelectViewModel _viewModel;
    private readonly IModelService _modelService;
    private bool _isSelected;
    private bool _isDownloaded;
    private bool _isDownloading;
    private double _downloadProgress;
    private CancellationTokenSource? _cts;

    public string Name { get; set; }
    public string Size { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDownloadedButNotSelected));
            }
        }
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (_isDownloaded != value)
            {
                _isDownloaded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDownloadedButNotSelected));
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (Math.Abs(_downloadProgress - value) > 0.01)
            {
                _downloadProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
    }

    public bool IsDownloadedButNotSelected => IsDownloaded && !IsSelected;

    public string DownloadProgressText => $"Downloading... {DownloadProgress:F0}%";

    public GgmlType GgmlType { get; }

    public ICommand DownloadCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand CancelDownloadCommand { get; }

    public ModelItem(ModelSelectViewModel viewModel, IModelService modelService, string name, string size, GgmlType ggmlType)
    {
        _viewModel = viewModel;
        _modelService = modelService;
        Name = name;
        Size = size;
        GgmlType = ggmlType;

        IsDownloading = false;
        DownloadProgress = 0;
        IsDownloaded = _modelService.IsModelInstalled(GgmlType);

        DownloadCommand = new RelayCommand(async () => await DownloadAsync());
        SelectCommand = new RelayCommand(async () => await Task.Run(() => Select()));
        RemoveCommand = new RelayCommand(async () => await RemoveAsync());
        CancelDownloadCommand = new RelayCommand(async () => await Task.Run(() => CancelDownload()));
    }

    private void Select()
    {
        if (!IsDownloaded) return;
        // ViewModel now handles the toggle logic
        _viewModel.SelectModel(this);
    }

    private void CancelDownload()
    {
        _cts?.Cancel();
    }

    private async Task DownloadAsync()
    {
        if (IsDownloading || IsDownloaded) return;

        IsDownloading = true;
        DownloadProgress = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                DownloadProgress = p.ProgressPercentage;
            });

            await _modelService.DownloadModelAsync(GgmlType, progress, _cts.Token);
            IsDownloaded = true;
        }
        catch (OperationCanceledException)
        {
            // Download cancelled
            DownloadProgress = 0;
        }
        catch (Exception ex)
        {
            App.ShowTrayNotification("Model download failed", ex.Message);
        }
        finally
        {
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RemoveAsync()
    {
        var result = MessageBox.Show($"Do you want to delete {Name}?", "Delete Model", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (IsSelected)
            {
                IsSelected = false; // Deselect before deleting
            }

            await _modelService.DeleteModelAsync(GgmlType);
            IsDownloaded = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
