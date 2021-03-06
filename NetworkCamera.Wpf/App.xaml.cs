﻿/*
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

using NetworkCamera.Main;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using Serilog;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;

/// <summary>
/// Network Camera application
/// Icon converter: https://convertico.com/
/// </summary>
namespace NetworkCamera.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const long _maxLogFileSize = 100000;
        private const uint _esContinous = 0x80000000;
        private const uint _esSystemRequired = 0x00000001;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private const uint _esDisplayRequired = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState([In] uint esFlags);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set working directory
            string appData = AboutViewModel.GetAppDataFolder();
            Directory.SetCurrentDirectory(appData);

            // Set logger
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
                .WriteTo.Debug()
#endif
                .WriteTo.File(
                    Path.Combine(appData, AboutModel.AssemblyProduct + ".log"),
                    rollingInterval: RollingInterval.Infinite,
                    fileSizeLimitBytes: _maxLogFileSize)
                .CreateLogger();

            Log.Information($"Startup \"{AboutModel.AssemblyProduct}\"");

            // Exception Handling Wiring
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

            NetworkCamera.Wpf.Properties.Settings.Default.Reload();
            EnsureBrowserEmulationEnabled("NetworkCamera.exe");

            // Prevent going to sleep mode
            _ = SetThreadExecutionState(_esContinous | _esSystemRequired);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Enable sleep mode
            _ = SetThreadExecutionState(_esContinous);

            ViewModelLocator.MainViewModel.SaveAll();
            NetworkCamera.Wpf.Properties.Settings.Default.Save();

            Log.Information($"Exit \"{AboutModel.AssemblyProduct}\"");

            base.OnExit(e);
        }

        private void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                var message = nameof(UnobservedTaskExceptionHandler);
                e?.SetObserved(); // Prevents the Program from terminating.

                if (e.Exception != null && e.Exception is Exception tuex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, tuex.Message);
                    Log.Error(tuex, message);
                }
                else if (sender is Exception ex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                    Log.Error(ex, message);
                }
            }
            catch { } // Swallow exception
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var message = nameof(UnhandledExceptionHandler);
                if (e.ExceptionObject != null && e.ExceptionObject is Exception uex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, uex.Message);
                    Log.Error(uex, message);
                }
                else if (sender is Exception ex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                    Log.Error(ex, message);
                }
            }
            catch { } // Swallow exception
        }

        private void DispatcherUnhandledExceptionHandler(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var message = nameof(DispatcherUnhandledExceptionHandler);
                if (e.Exception != null && e.Exception is Exception uex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, uex.Message);
                    Log.Error(uex, message);
                }
                else if (sender is Exception ex)
                {
                    message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                    Log.Error(ex, message);
                }
            }
            catch { } // Swallow exception
            e.Handled = true; // Continue processing
        }

        /// <summary>
        /// WebBrowser Internet Explorer 11 emulation
        /// </summary>
        public static void EnsureBrowserEmulationEnabled(string exename, bool uninstall = false)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);
                if (rk == null) return;
                if (uninstall)
                {
                    rk.DeleteValue(exename);
                }
                else
                {
                    dynamic value = rk.GetValue(exename);
                    if (value == null)
                        rk.SetValue(exename, (uint)11001, RegistryValueKind.DWord);
                }
            }
            finally
            {
            }
        }
    }
}
