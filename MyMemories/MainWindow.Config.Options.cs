using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace MyMemories;

public sealed partial class MainWindow
{
    private async Task ShowOptionsDialogAsync()
    {
        if (_configService == null)
            return;

        // Create UI for options
        var stackPanel = new StackPanel { Spacing = 24 };

        // === Performance Section ===
        var performanceHeader = new TextBlock
        {
            Text = "? Performance Settings",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stackPanel.Children.Add(performanceHeader);

        // Zip Compression Level
        var zipCompressionLabel = new TextBlock
        {
            Text = "Zip Compression Level:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(zipCompressionLabel);

        // Description
        var zipDescription = new TextBlock
        {
            Text = "Controls the balance between compression speed and file size when creating zip archives.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stackPanel.Children.Add(zipDescription);

        // Slider container with labels
        var sliderContainer = new Grid
        {
            Margin = new Thickness(0, 0, 0, 8)
        };
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sliderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Level labels (top row)
        var labelPanel = new Grid();
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fastLabel = new TextBlock
        {
            Text = "Fast",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(fastLabel, 0);
        labelPanel.Children.Add(fastLabel);

        var balancedLabel = new TextBlock
        {
            Text = "Balanced",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(balancedLabel, 1);
        labelPanel.Children.Add(balancedLabel);

        var maxLabel = new TextBlock
        {
            Text = "Maximum",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        Grid.SetColumn(maxLabel, 2);
        labelPanel.Children.Add(maxLabel);

        Grid.SetRow(labelPanel, 0);
        sliderContainer.Children.Add(labelPanel);

        // Slider
        var compressionSlider = new Slider
        {
            Minimum = 0,
            Maximum = 9,
            Value = _configService.ZipCompressionLevel,
            StepFrequency = 1,
            TickFrequency = 1,
            SnapsTo = Microsoft.UI.Xaml.Controls.Primitives.SliderSnapsTo.StepValues,
            Margin = new Thickness(0, 4, 0, 4)
        };
        Grid.SetRow(compressionSlider, 1);
        sliderContainer.Children.Add(compressionSlider);

        // Value display
        var valueDisplay = new TextBlock
        {
            Text = $"Level: {_configService.ZipCompressionLevel}",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(valueDisplay, 2);
        sliderContainer.Children.Add(valueDisplay);

        // Update value display when slider changes
        compressionSlider.ValueChanged += (s, args) =>
        {
            var level = (int)args.NewValue;
            valueDisplay.Text = $"Level: {level}";
        };

        stackPanel.Children.Add(sliderContainer);

        // Compression level guide
        var guidePanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 16)
        };

        guidePanel.Children.Add(new TextBlock
        {
            Text = "?? Compression Guide:",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 0-3: Fast compression, larger files - Best for quick backups",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 4-6: Balanced - Good for everyday use",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        guidePanel.Children.Add(new TextBlock
        {
            Text = "• Level 7-9: Maximum compression, slower - Best for archiving",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        });

        stackPanel.Children.Add(guidePanel);

        // Info banner
        var infoBanner = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 0, 120, 215)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE946", // Info icon
                        FontSize = 16,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    },
                    new TextBlock
                    {
                        Text = "Changes will apply to all future zip operations. Existing zip files are not affected.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 12
                    }
                }
            }
        };
        stackPanel.Children.Add(infoBanner);

        // Create dialog
        var dialog = new ContentDialog
        {
            Title = "Options",
            Content = new ScrollViewer
            {
                Content = stackPanel,
                MaxHeight = 600
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var newLevel = (int)compressionSlider.Value;
            _configService.ZipCompressionLevel = newLevel;
            await _configService.SaveConfigurationAsync();

            StatusText.Text = $"Options saved - Zip compression level set to {newLevel}";

            // Log the change
            if (_configService.IsLoggingEnabled())
            {
                await _configService.LogErrorAsync($"Zip compression level changed to {newLevel}");
            }
        }
    }
}
