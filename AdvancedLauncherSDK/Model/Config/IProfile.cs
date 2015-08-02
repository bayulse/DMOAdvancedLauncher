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

using System.Windows.Media;
using AdvancedLauncher.SDK.Management.Execution;

namespace AdvancedLauncher.SDK.Model.Config {

    public interface IProfile {

        int Id {
            get;
            set;
        }

        string Guid {
            get;
            set;
        }

        string Name {
            get;
            set;
        }

        string LaunchMode {
            get;
            set;
        }

        bool UpdateEngineEnabled {
            get;
            set;
        }

        bool KBLCServiceEnabled {
            get;
            set;
        }

        #region Subcontainers

        IRotationData Rotation {
            get;
            set;
        }

        INewsData News {
            get;
            set;
        }

        ILauncher Launcher {
            get;
            set;
        }

        #endregion Subcontainers

        #region Image

        string ImagePath {
            get;
            set;
        }

        ImageSource Image {
            set;
            get;
        }

        bool HasImage {
            get;
        }

        bool NoImage {
            get;
        }

        #endregion Image

        IGameModel GameModel {
            set;
            get;
        }
    }
}