using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http; // For HttpClient
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleUIExtension
{
    public class SimpleUI
    {
        readonly string environmentVariablePath = "VS_SETTINGS_PATH";
        readonly string simplifiedSettingsFilePath = "Resources\\simplified.settings";
        readonly string settingsCommand = "Tools.ImportandExportSettings";
        readonly string importSettingsArgs = "/import:\"{0}\"";
        readonly string resetSettingsArgs = "/reset";
        readonly string exportSettingsArgs = "/export:\"{0}\"";
        readonly string previousSettingsFile = "Resources\\previous.settings";
        readonly string settingsFileUri = "https://aka.ms/AAw4md1";
        readonly string simplifiedSettingsFromWebFilePath = "Resources\\simplified_web.settings";

        private readonly AsyncPackage package;
        private DTE dte;
        private bool isSimpleSettingsApplied = false;
        private string vsixInstallationPath;
        
        public SimpleUI(AsyncPackage Package, DTE Dte, string InstallationPath)
        {
            package = Package;
            dte = Dte;
            vsixInstallationPath = InstallationPath;
        }

        public async Task ToggleSimpleAsync()
        {
            if (isSimpleSettingsApplied)
            {
                var pathToPreviousSettingsFile = Path.Combine(vsixInstallationPath, previousSettingsFile);
                if (File.Exists(pathToPreviousSettingsFile))
                {
                    await ApplySettingsFileAsync(pathToPreviousSettingsFile);
                }
                else
                {
                    await ResetSettingsAsync();
                }
                isSimpleSettingsApplied = false;
            }
            else
            {
                var pathToPreviousSettingsFile = Path.Combine(vsixInstallationPath, previousSettingsFile);
                if (!File.Exists(pathToPreviousSettingsFile))
                {
                    await ExportSettingsFileAsync(pathToPreviousSettingsFile);
                }

                var pathToSettingsFile = Environment.GetEnvironmentVariable(environmentVariablePath);
                if(string.IsNullOrEmpty(pathToSettingsFile))
                {
                    pathToSettingsFile = Path.Combine(vsixInstallationPath, simplifiedSettingsFromWebFilePath);

                    await DownloadFileAsync(pathToSettingsFile);

                    if (!File.Exists(pathToSettingsFile))
                    {
                        pathToSettingsFile = Path.Combine(vsixInstallationPath, simplifiedSettingsFilePath);
                        if (!File.Exists(pathToSettingsFile))
                        {
                            throw new FileNotFoundException(string.Format("Settings file {0} not found please check the value of the environment variable {1}.", pathToSettingsFile, environmentVariablePath));
                        }
                    }
                }

                await ApplySettingsFileAsync(pathToSettingsFile);
                isSimpleSettingsApplied = true;
            }
        }

        private async Task ExportSettingsFileAsync(string SettingsFilePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var args = string.Format(exportSettingsArgs, SettingsFilePath);
            dte.ExecuteCommand(settingsCommand, args);
        }

        private async Task ApplySettingsFileAsync(string SettingsFilePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var args = string.Format(importSettingsArgs, SettingsFilePath);
            dte.ExecuteCommand(settingsCommand, args);
        }

        private async Task ResetSettingsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            dte.ExecuteCommand(settingsCommand, "");
        }

        public async Task DownloadFileAsync(string pathToSettingsFile)
        {
            // Get the directory of the running assembly
            string assemblyDirectory = Path.GetDirectoryName(typeof(SimpleUI).Assembly.Location);

            // Check if the directory has read permissions
            if (!HasWritePermission(assemblyDirectory))
            {
                throw new UnauthorizedAccessException($"Write permission is denied for the directory: {assemblyDirectory}");
            }

            // Define the file path to save the downloaded file
            string filePath = Path.Combine(assemblyDirectory, pathToSettingsFile);

            // Download the file from example.com
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(settingsFileUri);
                response.EnsureSuccessStatusCode();

                // Save the file to the specified path
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fileStream);
                }
            }
        }

        private bool HasWritePermission(string path)
        {
            try
            {
                // Attempt to create and delete a temporary file in the directory
                string tempFilePath = Path.Combine(path, Path.GetRandomFileName());
                using (FileStream fs = File.Create(tempFilePath, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                // Return false for any other exceptions
                return false;
            }
        }
    }
}
