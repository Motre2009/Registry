using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpectacularThing;

public partial class MainWindow : Window
{
    private const string AppKeyPath = @"Software\SpectacularThing";
    private const string DesktopPath = @"Control Panel\Desktop";
    private const string ColorPath = @"Control Panel\Colors";
    private const string DelayKey = "MenuShowDelay";
    private const string ColorKey = "Menu";

    private const int DefaultDelay = 400;
    private const string DefaultColor = "255 255 255";

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SendMessageTimeout(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private const uint HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    private void LoadSettings()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppKeyPath);
            if (key == null) return;

            var delayStr = key.GetValue(DelayKey)?.ToString() ?? "";
            if (int.TryParse(delayStr, out int delay))
                sliderMenuDelay.Value = Math.Max(0, Math.Min(1000, delay));

            var colorStr = key.GetValue(ColorKey)?.ToString() ?? "";
            if (!string.IsNullOrEmpty(colorStr))
                textBoxMenuColor.Text = colorStr;

            UpdateDelayLabel();
            UpdateColorPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Load error: {ex.Message}");
        }
    }

    private void SliderMenuDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateDelayLabel();
    }

    private void TextBoxMenuColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
    }

    private void ButtonRed_Click(object sender, RoutedEventArgs e)
    {
        textBoxMenuColor.Text = "255 0 0";
        UpdateColorPreview();
    }

    private void ButtonGreen_Click(object sender, RoutedEventArgs e)
    {
        textBoxMenuColor.Text = "0 255 0";
        UpdateColorPreview();
    }

    private void ButtonBlue_Click(object sender, RoutedEventArgs e)
    {
        textBoxMenuColor.Text = "0 0 255";
        UpdateColorPreview();
    }

    private void UpdateDelayLabel()
    {
        if (labelMenuDelay == null)
        {
            Debug.WriteLine("labelMenuDelay is null");
            return;
        }

        labelMenuDelay.Content = $"Menu Show Delay: {(int)sliderMenuDelay.Value} ms";
    }

    private void ButtonApply_Click(object sender, RoutedEventArgs e)
    {
        var delayValue = ((int)sliderMenuDelay.Value).ToString();
        var colorValue = textBoxMenuColor.Text.Trim();

        if (!IsValidRgb(colorValue))
        {
            MessageBox.Show("Invalid RGB format! Use: 255 255 255");
            return;
        }

        try
        {
            using var desktopKey = Registry.CurrentUser.CreateSubKey(DesktopPath);
            desktopKey.SetValue(DelayKey, delayValue, RegistryValueKind.String);

            using var colorsKey = Registry.CurrentUser.CreateSubKey(ColorPath);
            colorsKey.SetValue(ColorKey, colorValue, RegistryValueKind.String);

            SaveToAppKey(delayValue, colorValue);

            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out _);

            SystemParametersInfo(0, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            MessageBox.Show("Settings applied! Press F5 on desktop for color. Re-log for delay.");
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Access denied. Try running as administrator.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private void ButtonReset_Click(object sender, RoutedEventArgs e)
    {
        sliderMenuDelay.Value = DefaultDelay;
        textBoxMenuColor.Text = DefaultColor;
        UpdateDelayLabel();
        UpdateColorPreview();

        try
        {
            using var desktopKey = Registry.CurrentUser.CreateSubKey(DesktopPath);
            desktopKey.SetValue(DelayKey, DefaultDelay.ToString(), RegistryValueKind.String);

            using var colorsKey = Registry.CurrentUser.CreateSubKey(ColorPath);
            colorsKey.SetValue(ColorKey, DefaultColor, RegistryValueKind.String);

            SaveToAppKey(DefaultDelay.ToString(), DefaultColor);

            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out _);

            SystemParametersInfo(0, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            MessageBox.Show("Reset to defaults! Press F5 on desktop or re-login.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reset error: {ex.Message}");
        }
    }

    private bool IsValidRgb(string rgb)
    {
        var parts = rgb.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 &&
               int.TryParse(parts[0], out int r) && r >= 0 && r <= 255 &&
               int.TryParse(parts[1], out int g) && g >= 0 && g <= 255 &&
               int.TryParse(parts[2], out int b) && b >= 0 && b <= 255;
    }

    private void SaveToAppKey(string delay, string color)
    {
        using var key = Registry.CurrentUser.CreateSubKey(AppKeyPath);
        key.SetValue(DelayKey, delay, RegistryValueKind.String);
        key.SetValue(ColorKey, color, RegistryValueKind.String);
    }

    private void UpdateColorPreview()
    {
        if (textBoxMenuColor == null || !IsValidRgb(textBoxMenuColor.Text))
        {
            return;
        }
        if (previewRect == null)
        {
            Debug.WriteLine("previewRect is null");
            return;
        }

        var parts = textBoxMenuColor.Text.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (byte.TryParse(parts[0], out byte r) &&
            byte.TryParse(parts[1], out byte g) &&
            byte.TryParse(parts[2], out byte b))
        {
            previewRect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
