﻿// ======================================================================
// DIGIMON MASTERS ONLINE ADVANCED LAUNCHER
// Copyright (C) 2015 Ilya Egorov (goldrenard@gmail.com)

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.
// ======================================================================

using System;
using System.AddIn.Contract;
using System.AddIn.Pipeline;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Media;
using AdvancedLauncher.SDK.Management;
using AdvancedLauncher.SDK.Model;
using AdvancedLauncher.SDK.Model.Config;
using AdvancedLauncher.SDK.Model.Events;
using AdvancedLauncher.SDK.UI;
using AdvancedLauncher.Tools.Interop;
using AdvancedLauncher.UI.Pages;
using AdvancedLauncher.UI.Windows;
using MahApps.Metro.Controls;
using Ninject;

namespace AdvancedLauncher.Management {

    public class WindowManager : CrossDomainObject, IWindowManager {
        private bool IsMenuSeparatorInserted = false;

        private int InternalMenuCount = 0;

        private delegate void UpdateTransitionDelegate(object Control, TransitionType TransitionType);

        private bool IsStarted = false;

        private bool IsContract = false;

        private bool IsAirspaceDecoration = false;

        private TransitionType DefaultTransition = TransitionType.Left;

        private ConcurrentStack<IRemoteControl> WindowStack {
            get;
            set;
        } = new ConcurrentStack<IRemoteControl>();

        private IRemoteControl CurrentWindow {
            get;
            set;
        }

        public ObservableCollection<SDK.Model.MenuItem> MenuItems {
            [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
            get;
            private set;
        } = new ObservableCollection<SDK.Model.MenuItem>();

        public ObservableCollection<PageItem> PageItems {
            [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
            get;
            private set;
        } = new ObservableCollection<PageItem>();

        #region Base Controls

        private MainWindow MainWindow {
            get;
            set;
        }

        private PageItem MainPage {
            get;
            set;
        }

        private PageItem Gallery {
            get;
            set;
        }

        private PageItem Community {
            get;
            set;
        }

        private PageItem Personalization {
            get;
            set;
        }

        private List<NamedItem> OwnedItems = new List<NamedItem>();

        #endregion Base Controls

        #region Injection

        [Inject]
        public IEnvironmentManager EnvironmentManager {
            get;
            set;
        }

        [Inject]
        public IConfigurationManager ConfigurationManager {
            get;
            set;
        }

        [Inject]
        public ILanguageManager LanguageManager {
            get;
            set;
        }

        [Inject]
        public IProfileManager ProfileManager {
            get; set;
        }

        #endregion Injection

        public void Initialize() {
            EnvironmentManager.FileSystemLocked += OnFileSystemLocked;
            ProfileManager.ProfileChanged += OnProfileChanged;
        }

        public void Start() {
            if (IsStarted) {
                return;
            }
            this.MainWindow = App.Kernel.Get<MainWindow>(); // do not inject it directly, we should not export it as public property
            Application.Current.MainWindow = MainWindow;
            DefaultTransition = MainWindow.transitionLayer.Transition;
            MainWindow.Loaded += (s, e) => {
                BuildMenu();
                ShowWindow(new PagesWindow().Container);
            };
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject())) {
                App.Kernel.Get<Splashscreen>().Close();
                MainWindow.Show();
                OnProfileChanged(null, null); // force update tab availability
            }
            IsStarted = true;
        }

        private void BuildMenu() {
            var settingsItem = new SDK.Model.MenuItem("Settings", "appbar_settings", new SDK.Tools.Thickness(5, 5, 5, 5), true);
            settingsItem.Click += (s, e) => {
                MainWindow.SettingsFlyout.Width = MainWindow.ProfileSwitcher.ActualWidth + MainWindow.FLYOUT_WIDTH_MIN;
                if (!MainWindow.SettingsFlyout.IsOpen) {
                    MainWindow.SettingsFlyout.Visibility = Visibility.Hidden;
                }
                MainWindow.SettingsFlyout.IsOpen = true;
            };

            var consoleItem = new SDK.Model.MenuItem("Console", "appbar_app", new SDK.Tools.Thickness(5, 7, 5, 7), true);
            consoleItem.Click += (s, e) => {
                ShowWindow(App.Kernel.Get<Logger>().Container);
                MainWindow.MenuFlyout.IsOpen = false;
            };

            var aboutItem = new SDK.Model.MenuItem("About", "appbar_information", new SDK.Tools.Thickness(9, 4, 9, 4), true);
            aboutItem.Click += (s, e) => {
                ShowWindow(App.Kernel.Get<About>().Container);
                MainWindow.MenuFlyout.IsOpen = false;
            };

            List<MenuItem> extraMenuItems = new List<MenuItem>(MenuItems);
            MenuItems.Clear();
            MenuItems.Add(settingsItem);
            MenuItems.Add(consoleItem);
            MenuItems.Add(aboutItem);
            InternalMenuCount = MenuItems.Count;
            OwnedItems.AddRange(MenuItems);
            foreach (MenuItem item in extraMenuItems) {
                MenuItems.Add(item);
            }

            MainPage = new PageItem("MainWindow_NewsTab", new MainPage().Container, true);
            Gallery = new PageItem("MainWindow_GalleryTab", new Gallery().Container, true);
            Community = new PageItem("MainWindow_CommunityTab", new Community().Container, true);
            Personalization = new PageItem("MainWindow_PersonalizationTab", new Personalization().Container, true);

            List<PageItem> extraPages = new List<PageItem>(PageItems);
            PageItems.Clear();
            PageItems.Add(MainPage);
            PageItems.Add(Gallery);
            PageItems.Add(Community);
            PageItems.Add(Personalization);
            OwnedItems.AddRange(PageItems);
            foreach (PageItem item in extraPages) {
                PageItems.Add(item);
            }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public void ShowWindow(IRemoteControl window) {
            if (window == null) {
                throw new ArgumentException("Window argument cannot be null");
            }

            bool IsContract = false;
            bool IsAirspaceDecoration = false;
            object Control = null;
            try {
                Control = window.GetControl();
            } catch (SerializationException) {
                INativeHandleContract contract = window.GetControl(true) as INativeHandleContract;
                if (contract != null) {
                    Control = FrameworkElementAdapters.ContractToViewAdapter(contract);
                    IsContract = true;
                    IsAirspaceDecoration = window.EnableAirspaceFix;
                }
            }
            if (Control != null) {
                if (CurrentWindow != null) {
                    WindowStack.Push(CurrentWindow);
                }

                if (IsContract && IsAirspaceDecoration) {
                    Control = new AirspaceDecorator() {
                        AirspaceMode = AirspaceMode.Redirect,
                        IsInputRedirectionEnabled = true,
                        IsOutputRedirectionEnabled = true,
                        Background = Brushes.White,
                        Content = Control
                    };
                }

                CurrentWindow = window;
                CurrentWindow.OnShow();
                UpdateTransition(Control, ((IsContract && !IsAirspaceDecoration) || (this.IsContract && !this.IsAirspaceDecoration))
                    ? TransitionType.Normal
                    : DefaultTransition);
                this.IsContract = IsContract;
                this.IsAirspaceDecoration = IsAirspaceDecoration;
            }
        }

        private void UpdateTransition(object Control, TransitionType TransitionType) {
            if (!MainWindow.Dispatcher.CheckAccess()) {
                MainWindow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new UpdateTransitionDelegate((c, t) => {
                    UpdateTransition(c, t);
                }), Control, TransitionType);
                return;
            }
            MainWindow.transitionLayer.Transition = TransitionType;
            MainWindow.transitionLayer.Content = Control;
        }

        public void GoHome() {
            IRemoteControl homeWindow = CurrentWindow;
            while (WindowStack.Count > 0) {
                homeWindow = PopAvailableWindow();
            }
            this.CurrentWindow = null;
            ShowWindow(homeWindow);
        }

        public void GoBack() {
            if (WindowStack.Count > 0) {
                IRemoteControl previous = PopAvailableWindow();
                this.CurrentWindow = null;
                ShowWindow(previous);
            }
        }

        public void GoBack(IRemoteControl window) {
            if (window == null) {
                throw new ArgumentException("Window argument cannot be null");
            }
            if (window.Equals(CurrentWindow) && WindowStack.Count > 0) {
                window = PopAvailableWindow();
                this.CurrentWindow = null;
                ShowWindow(window);
            }
        }

        private IRemoteControl PopAvailableWindow() {
            bool IsSuccess = false;
            IRemoteControl control = null;
            while (!IsSuccess) {
                try {
                    WindowStack.TryPop(out control);
                    var boolEnabled = control.EnableAirspaceFix;
                    IsSuccess = true;
                } catch (AppDomainUnloadedException) { }
            }
            return control;
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public void AddMenuItem(SDK.Model.MenuItem menuItem) {
            lock (MenuItems) {
                if (!IsMenuSeparatorInserted) {
                    MenuItems.Add(MenuItem.Separator);
                    IsMenuSeparatorInserted = true;
                }
                MenuItems.Add(menuItem);
            }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public bool RemoveMenuItem(SDK.Model.MenuItem menuItem) {
            lock (MenuItems) {
                if (OwnedItems.Contains(menuItem)) {
                    throw new ArgumentException("You are not allowed to remove default MenuItem");
                }
                bool result = MenuItems.Remove(menuItem);
                if (MenuItems.Count == InternalMenuCount + 1) {
                    var obj = MenuItems.Last();
                    if (obj.IsSeparator) {
                        MenuItems.Remove(obj);
                        IsMenuSeparatorInserted = false;
                    }
                }
                return result;
            }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public void AddPageItem(PageItem pageItem) {
            PageItems.Add(pageItem);
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public bool RemovePageItem(PageItem pageItem) {
            if (OwnedItems.Contains(pageItem)) {
                throw new ArgumentException("You are not allowed to remove default PageItem");
            }
            return PageItems.Remove(pageItem);
        }

        #region Event handlers

        private void OnFileSystemLocked(object sender, LockedEventArgs e) {
            if (Personalization == null) {
                return;
            }
            if (e.IsLocked) {
                //Выбираем первую вкладку и отключаем персонализацию (на всякий случай)
                Personalization.IsEnabled = false;
            } else {
                //Включаем персонализации обратно если игра определена
                if (ConfigurationManager.CheckGame(ProfileManager.CurrentProfile.GameModel)) {
                    Personalization.IsEnabled = true;
                }
            }
        }

        private void OnProfileChanged(object sender, BaseEventArgs e) {
            GameModel model = ProfileManager.CurrentProfile.GameModel;
            bool gameAvailable = ConfigurationManager.CheckGame(model);

            if (Community != null) {
                Community.IsEnabled = ConfigurationManager.GetConfiguration(model).IsWebAvailable;
            }
            if (Gallery != null) {
                Gallery.IsEnabled = gameAvailable;
            }
            if (Personalization != null) {
                Personalization.IsEnabled = gameAvailable;
            }
        }

        #endregion Event handlers
    }
}