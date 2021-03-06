/*
 * Copyright 2019 Capnode AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using NetworkCamera.Device;
using NetworkCamera.Service.Inference;
using NetworkCamera.Setting;
using Serilog;

namespace NetworkCamera.Main
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private bool _isBusy;
        private string _statusMessage;
        private bool _overviewTabSelected;
        private bool _devicesTabSelected;
        private readonly InferenceServer _inferenceServer;
        private readonly Task _startupTask;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(
            SettingsViewModel settingsViewModel,
            DevicesViewModel devicesViewModel,
            InferenceServer inferenceServer)
        {
            if (settingsViewModel == null) throw new ArgumentNullException(nameof(settingsViewModel));
            if (devicesViewModel == null) throw new ArgumentNullException(nameof(devicesViewModel));
            if (inferenceServer == null) throw new ArgumentNullException(nameof(inferenceServer));
            SettingsViewModel = settingsViewModel;
            DevicesViewModel = devicesViewModel;
            _inferenceServer = inferenceServer;

            SaveCommand = new RelayCommand(() => SaveAll(), () => !IsBusy);
            ShowDeviceCommand = new RelayCommand(() => OnShowDevice(), () => !IsBusy);
            Messenger.Default.Register<NotificationMessage>(this, OnStatusMessage);
            Messenger.Default.Register<DeviceMessage>(this, OnDeviceMessage);

            _startupTask = StartupAsync();
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand ShowDeviceCommand { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public DevicesViewModel DevicesViewModel { get; }

        public ObservableCollection<DeviceViewModel> OnlineCameras { get; } = new ObservableCollection<DeviceViewModel>();

        public static string Title => AboutModel.AssemblyProduct;

        /// <summary>
        /// Mark ongoing operation
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public bool OverviewTabSelected
        {
            get => _overviewTabSelected;
            set => Set(ref _overviewTabSelected, value);
        }

        public bool DevicesTabSelected
        {
            get => _devicesTabSelected;
            set => Set(ref _devicesTabSelected, value);
        }

        public void SaveAll()
        {
            string appData = AboutViewModel.GetAppDataFolder();
            SaveConfig(appData);
        }

        private void OnStatusMessage(NotificationMessage message)
        {
            StatusMessage = message.Notification;
        }

        private void OnDeviceMessage(DeviceMessage message)
        {
            // Add active cameras
            OnlineCameras.Clear();
            foreach (DeviceViewModel device in DevicesViewModel.Devices)
            {
                if (device.Active)
                {
                    OnlineCameras.Add(device);
                }
            }
        }

        private void OnShowDevice()
        {
            DevicesTabSelected = true;
        }

        private async Task StartupAsync()
        {
            try
            {
                // Read configuration
                await ReadConfig().ConfigureAwait(false);
                Messenger.Default.Send(new NotificationMessage("Ready"));
            }
            catch (Exception ex)
            {
                string message = $"{ex.Message} ({ex.GetType()})";
                Messenger.Default.Send(new NotificationMessage(message));
                Log.Error(ex, message);
            }
        }

        private async Task ReadConfig()
        {
            try
            {
                IsBusy = true;
                string appData = AboutViewModel.GetAppDataFolder();
                string program = AboutViewModel.GetProgramFolder();
                CopyDirectory(Path.Combine(program, "AppData"), appData, false);

                SettingsViewModel.Read(Path.Combine(appData, "Settings.json"));
                await StartServices().ConfigureAwait(true);
                DevicesViewModel.Read(Path.Combine(appData, "Devices.json"));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveConfig(string appData)
        {
            try
            {
                IsBusy = true;
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }

                SettingsViewModel.Save(Path.Combine(appData, "Settings.json"));
                DevicesViewModel.Save(Path.Combine(appData, "Devices.json"));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task StartServices()
        {
            // Start services
            if (!float.TryParse(
                SettingsViewModel.Model.InferenceLimit,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float limit))
            {
                limit = 0f;
            }

            await _inferenceServer.Start(
                SettingsViewModel.Model.InferenceServer,
                SettingsViewModel.Model.InferenceModel,
                SettingsViewModel.Model.InferenceLabels,
                limit)
                .ConfigureAwait(false);
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool overwiteFiles)
        {
            if (!Directory.Exists(sourceDir))
            {
                string message = $"Source directory {sourceDir} does not exist";
                Messenger.Default.Send(new NotificationMessage(message));
                Log.Error(message);
                return;
            }

            // Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir, StringComparison.OrdinalIgnoreCase));

            // Copy all the files & Replaces any files with the same name
            foreach (string source in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string dest = source.Replace(sourceDir, destDir, StringComparison.OrdinalIgnoreCase);
                if (!File.Exists(dest) || overwiteFiles)
                {
                    File.Copy(source, dest, true);
                }
            }
        }
    }
}