//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Threading;

 

namespace QloudSync {

    public enum IconState {
        Idle,
        SyncingUp,
        SyncingDown,
        Syncing,
        Error
    }


    public class SparkleStatusIconController {

        public event UpdateIconEventHandler UpdateIconEvent = delegate { };
        public delegate void UpdateIconEventHandler (IconState state);

        public event UpdateMenuEventHandler UpdateMenuEvent = delegate { };
        public delegate void UpdateMenuEventHandler (IconState state);

        public event UpdateStatusItemEventHandler UpdateStatusItemEvent = delegate { };
        public delegate void UpdateStatusItemEventHandler (string state_text);

        public event UpdateQuitItemEventHandler UpdateQuitItemEvent = delegate { };
        public delegate void UpdateQuitItemEventHandler (bool quit_item_enabled);

        public event UpdateRecentEventsItemEventHandler UpdateRecentEventsItemEvent = delegate { };
        public delegate void UpdateRecentEventsItemEventHandler (bool recent_events_item_enabled);

        public IconState CurrentState = IconState.Idle;
        public string StateText = "Welcome to QloudSync!";

        public readonly int MenuOverflowThreshold   = 9;
        public readonly int MinSubmenuOverflowCount = 3;

        public string [] Folders;
        public string [] FolderErrors;
        
        public string [] OverflowFolders;
        public string [] OverflowFolderErrors;
        

        public string FolderSize {
            get {
                double size = 0;

                if (size == 0)
                    return "";
                else
                    return "— " + Program.Controller.FormatSize (size);
            }
        }

        public int ProgressPercentage {
            get {
                return (int) Program.Controller.ProgressPercentage;
            }
        }

        public string ProgressSpeed {
            get {
                return Program.Controller.ProgressSpeed;
            }
        }

        public bool QuitItemEnabled {
            get {
                return (CurrentState == IconState.Idle || CurrentState == IconState.Error);
            }
        }


        public SparkleStatusIconController ()
        {
            UpdateFolders ();

            Program.Controller.OnIdle += delegate {
                UpdateFolders ();

                if (CurrentState != IconState.Error) {
                    CurrentState = IconState.Idle;

                    if (System.IO.Directory.GetDirectories(Settings.HomePath).Length == 0 && System.IO.Directory.GetFiles (Settings.HomePath).Length < 2)
                        StateText = "Welcome to QloudSync!";
                    else
                        StateText = "Files up to date " + FolderSize;
                }

                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);
                UpdateIconEvent (CurrentState);
                UpdateMenuEvent (CurrentState);
            };

            Program.Controller.OnSyncing += delegate {
				int repos_syncing_up   = 0;
				int repos_syncing_down = 0;
				
				if (repos_syncing_up > 0 &&
				    repos_syncing_down > 0) {
					
					CurrentState = IconState.Syncing;
                    StateText    = "Syncing changes…";
				
				} else if (repos_syncing_down == 0) {
					CurrentState = IconState.SyncingUp;
                    StateText    = "Sending changes…";
					
				} else {
					CurrentState = IconState.SyncingDown;
                    StateText    = "Receiving changes…";
				}

                if (ProgressPercentage > 0)
                    StateText += " " + ProgressPercentage + "%  " + ProgressSpeed;

                UpdateIconEvent (CurrentState);
                UpdateStatusItemEvent (StateText);
                UpdateQuitItemEvent (QuitItemEnabled);
            };

            Program.Controller.OnError += delegate {
                CurrentState = IconState.Error;
                StateText    = "Failed to send some changes";

                UpdateFolders ();
                
                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);
                UpdateIconEvent (CurrentState);
                UpdateMenuEvent (CurrentState);
            };
        }


        public void SparkleShareClicked ()
        {
            Program.Controller.OpenSparkleShareFolder ();
        }



        public void AddHostedProjectClicked ()
        {
            new Thread (() => Program.Controller.ShowSetupWindow (PageType.Add)).Start ();
        }


        public void RecentEventsClicked ()
        {
            new Thread (() => Program.Controller.ShowEventLogWindow ()).Start ();
        }


        public void AboutClicked ()
        {
            Program.Controller.ShowAboutWindow ();
        }
        
		
        public void QuitClicked ()
        {
            Program.Controller.Quit ();
        }


        private void UpdateFolders ()
        {
        }
    }
}
