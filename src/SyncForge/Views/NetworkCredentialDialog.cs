using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncForge.Core.Network;
using SyncForge.Helpers;
using SyncForge.Services;

namespace SyncForge.Views;

/// <summary>
/// Anmeldedaten für eine Netzwerkfreigabe: werden vor dem Speichern getestet und in der
/// Windows-Anmeldeinformationsverwaltung abgelegt (gilt für alle Profile mit dieser Freigabe).
/// </summary>
public static class NetworkCredentialDialog
{
    public static async Task ShowAsync(FrameworkElement owner, string path)
    {
        if (NetworkShares.GetShareRoot(path) is not { } root) return;
        var existing = NetworkCredentials.GetUserName(root);

        var user = new TextBox
        {
            Header = Localization.Get("Credentials_User"),
            PlaceholderText = Localization.Get("Credentials_UserPlaceholder"),
            Text = existing ?? string.Empty
        };
        var password = new PasswordBox { Header = Localization.Get("Credentials_Password") };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false, IsOpen = false };
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var content = new StackPanel { Spacing = 12, MinWidth = 380 };
        content.Children.Add(new TextBlock
        {
            Text = Format.Text("Credentials_Text", root), TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        content.Children.Add(user);
        content.Children.Add(password);
        content.Children.Add(progress);
        content.Children.Add(error);

        var dialog = new ContentDialog
        {
            XamlRoot = owner.XamlRoot,
            Title = Localization.Get("Credentials_Title"),
            Content = content,
            PrimaryButtonText = Localization.Get("Credentials_Save"),
            SecondaryButtonText = existing is null ? string.Empty : Localization.Get("Credentials_Remove"),
            CloseButtonText = Localization.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = owner.ActualTheme
        };

        var busy = false;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            // Läuft schon ein Test: zweiten Klick ignorieren (Button nicht deaktivieren, sonst verliert er die Akzentfarbe).
            if (busy) { args.Cancel = true; return; }
            busy = true;
            var deferral = args.GetDeferral();
            try
            {
                var name = user.Text.Trim();
                var secret = password.Password;
                if (name.Length == 0 || secret.Length == 0)
                {
                    ShowError(Localization.Get("Credentials_Missing"));
                    args.Cancel = true;
                    return;
                }

                progress.Visibility = Visibility.Visible;
                error.IsOpen = false;
                // Verbindung testen, bevor gespeichert wird (Netzwerk kann einige Sekunden brauchen).
                var outcome = await Task.Run(() => NetworkShares.Connect(root, name, secret));
                progress.Visibility = Visibility.Collapsed;

                // Konflikt: Windows ist schon mit dem Server verbunden. Ist die Freigabe dabei erreichbar, passt es trotzdem.
                var ok = outcome.Success || (outcome.Result == NetworkAccessResult.CredentialConflict && Directory.Exists(root));
                if (!ok)
                {
                    ShowError(outcome.Result switch
                    {
                        NetworkAccessResult.LogonFailure => Localization.Get("Credentials_ErrorLogon"),
                        NetworkAccessResult.NotFound => Format.Text("Credentials_ErrorNotFound", root),
                        NetworkAccessResult.CredentialConflict => Localization.Get("Credentials_ErrorConflict"),
                        _ => outcome.Message ?? Localization.Get("Credentials_ErrorLogon")
                    });
                    args.Cancel = true;
                    return;
                }
                NetworkCredentials.Save(root, name, secret);
            }
            finally
            {
                busy = false;
                deferral.Complete();
            }
        };

        void ShowError(string message)
        {
            error.Message = message;
            error.IsOpen = true;
        }

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary) NetworkCredentials.Delete(root);
    }
}
