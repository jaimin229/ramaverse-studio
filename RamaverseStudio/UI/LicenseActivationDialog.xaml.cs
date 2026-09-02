using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using RamaverseStudio.Services.Licensing;

namespace RamaverseStudio.UI
{
    public partial class LicenseActivationDialog : Window
    {
        public const string CheckoutUrl = "https://jaimin229.gumroad.com/l/ramaverse-studio-pro";

        public LicenseActivationDialog()
        {
            InitializeComponent();
            RefreshState();
        }

        private void RefreshState()
        {
            var lic = LicenseManager.Instance;
            TxtMachineId.Text = lic.MachineId;

            if (lic.Tier == LicenseTier.Pro || lic.Tier == LicenseTier.Commercial)
            {
                TxtTierLabel.Text = $"{lic.Tier.ToString().ToUpper()} ACTIVE";
                BadgeTier.Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129));
                BadgeTier.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtTierLabel.Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183));
                TxtLicenseKey.Text = lic.CurrentKey;
                TxtLicenseKey.IsEnabled = false;
                BtnActivate.Content = "Deactivate";
                BtnStartTrial.Visibility = Visibility.Collapsed;
            }
            else if (lic.IsActiveTrial)
            {
                TxtTierLabel.Text = $"PRO TRIAL ({lic.TrialDaysRemaining} DAYS LEFT)";
                BadgeTier.Background = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                BadgeTier.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                TxtTierLabel.Foreground = new SolidColorBrush(Color.FromRgb(252, 211, 77));
                BtnStartTrial.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtTierLabel.Text = "STARTER FREE EDITION";
                BadgeTier.Background = new SolidColorBrush(Color.FromArgb(50, 124, 58, 237));
                BadgeTier.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237));
                TxtTierLabel.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            }
        }

        private async void OnActivateClicked(object sender, RoutedEventArgs e)
        {
            var lic = LicenseManager.Instance;
            if (lic.IsPro && lic.Tier != LicenseTier.Trial)
            {
                lic.Deactivate();
                TxtLicenseKey.IsEnabled = true;
                TxtLicenseKey.Text = "";
                BtnActivate.Content = "Activate Key";
                TxtStatusMessage.Text = "License deactivated. Switched to Free Edition.";
                TxtStatusMessage.Foreground = Brushes.LightGray;
                TxtStatusMessage.Visibility = Visibility.Visible;
                RefreshState();
                return;
            }

            string inputKey = TxtLicenseKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                TxtStatusMessage.Text = "Please enter a valid license key.";
                TxtStatusMessage.Foreground = Brushes.Crimson;
                TxtStatusMessage.Visibility = Visibility.Visible;
                return;
            }

            BtnActivate.IsEnabled = false;
            TxtStatusMessage.Text = "Verifying license key with Gumroad...";
            TxtStatusMessage.Foreground = Brushes.LightSkyBlue;
            TxtStatusMessage.Visibility = Visibility.Visible;

            bool success = await lic.ValidateAndActivateAsync(inputKey);
            BtnActivate.IsEnabled = true;

            if (success)
            {
                TxtStatusMessage.Text = "Pro Edition successfully activated! All features unlocked.";
                TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtStatusMessage.Visibility = Visibility.Visible;
                RefreshState();
            }
            else
            {
                TxtStatusMessage.Text = "Invalid license key. Please verify your Gumroad purchase receipt.";
                TxtStatusMessage.Foreground = Brushes.Crimson;
                TxtStatusMessage.Visibility = Visibility.Visible;
            }
        }

        private void OnStartTrialClicked(object sender, RoutedEventArgs e)
        {
            LicenseManager.Instance.ActivateTrial(7);
            TxtStatusMessage.Text = "7-Day Full Pro Trial activated! Enjoy all creator features.";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            TxtStatusMessage.Visibility = Visibility.Visible;
            RefreshState();
        }

        private void OnBuyLicenseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CheckoutUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                TxtStatusMessage.Text = $"Please visit: {CheckoutUrl}";
                TxtStatusMessage.Visibility = Visibility.Visible;
            }
        }
    }
}
