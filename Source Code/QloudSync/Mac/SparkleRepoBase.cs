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
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Timers = System.Timers;

namespace QloudSync {

    public enum SyncStatus {
        Idle,
        SyncUp,
        SyncDown,
        Error
    }


    public enum ErrorStatus {
        None,
        HostUnreachable,
        HostIdentityChanged,
        AuthenticationFailed,
        DiskSpaceExcedeed
    }


    public abstract class SparkleRepoBase {


        public abstract string CurrentRevision { get; }
        public abstract double Size { get; }
        public abstract double HistorySize { get; }
        public abstract List<string> ExcludePaths { get; }
        public abstract bool HasUnsyncedChanges { get; set; }
        public abstract bool HasLocalChanges { get; }
        public abstract bool HasRemoteChanges { get; }
        public abstract bool SyncUp ();
        public abstract bool SyncDown ();
        public abstract void RestoreFile (string path, string revision, string target_file_path);
        public event SyncStatusChangedEventHandler SyncStatusChanged = delegate { };
        public delegate void SyncStatusChangedEventHandler (SyncStatus new_status);

        public event ProgressChangedEventHandler ProgressChanged = delegate { };
        public delegate void ProgressChangedEventHandler (double percentage, string speed);

 
        public event Action ConflictResolved = delegate { };
        public event Action ChangesDetected = delegate { };


        public readonly string LocalPath;
        public readonly string Name;
        public readonly Uri RemoteUrl;
        public SyncStatus Status { get; private set; }
        public ErrorStatus Error { get; protected set; }
        public bool IsBuffering { get; private set; }
        public double ProgressPercentage { get; private set; }
        public string ProgressSpeed { get; private set; }


        public virtual string [] UnsyncedFilePaths {
            get {
                return new string [0];
            }
        }


        protected Settings local_config;
        private TimeSpan poll_interval            = PollInterval.Short;
        private DateTime last_poll                = DateTime.Now;
        private DateTime progress_last_change     = DateTime.Now;
        private TimeSpan progress_change_interval = new TimeSpan (0, 0, 0, 1);
        private Timers.Timer remote_timer         = new Timers.Timer () { Interval = 5000 };

        private bool is_syncing {
            get {
                return (Status == SyncStatus.SyncUp || Status == SyncStatus.SyncDown || IsBuffering);
            }
        }

        private static class PollInterval {
            public static readonly TimeSpan Short = new TimeSpan (0, 0, 5, 0);
            public static readonly TimeSpan Long  = new TimeSpan (0, 0, 15, 0);
        }


        public SparkleRepoBase (string path, Settings config)
        {

            Status            = SyncStatus.Idle;
            Error             = ErrorStatus.None;
            this.local_config = config;
            LocalPath         = path;
            Name              = Path.GetFileName (LocalPath);
            RemoteUrl         = new Uri ("https://s.greenqloud.com/");
            IsBuffering       = false;
        
            SyncStatusChanged += delegate (SyncStatus status) {
                Status = status;
            };

            this.remote_timer.Elapsed += delegate {
                if (this.is_syncing || IsBuffering)
                    return;

                int time_comparison = DateTime.Compare (this.last_poll, DateTime.Now.Subtract (this.poll_interval));
                bool time_to_poll =  (time_comparison < 0);

                if (time_to_poll && !is_syncing) {
                    this.last_poll = DateTime.Now;

                    if (HasRemoteChanges) {
                        this.poll_interval = PollInterval.Long;
                        SyncDownBase ();
                    }
                }

                // In the unlikely case that we haven't synced up our
                // changes or the server was down, sync up again
                if (HasUnsyncedChanges && !is_syncing && Error == ErrorStatus.None)
                    SyncUpBase ();
            };
        }


        public void Initialize ()
        {
            // Sync up everything that changed
            // since we've been offline
            new Thread (() => {
                if (!this.is_syncing && (HasLocalChanges || HasUnsyncedChanges)) {
                    SyncUpBase ();

                    while (HasLocalChanges && !this.is_syncing)
                        SyncUpBase ();
                }

                this.remote_timer.Start ();
            
            }).Start ();
        }


        private Object buffer_lock = new Object ();

        public void OnFileActivity (FileSystemEventArgs args)
        {
            if (IsBuffering)
                return;

            if (args != null) {
                foreach (string exclude_path in ExcludePaths) {
                    if (args.FullPath.Contains (exclude_path))
                        return;
                }
            }

            lock (this.buffer_lock) {
                if (IsBuffering || !HasLocalChanges)
                    return;

                IsBuffering = true;
            }

            ChangesDetected ();


            Logger.LogInfo ("Local", Name + " | Activity detected, waiting for it to settle...");

            List<double> size_buffer = new List<double> ();

            do {
                if (size_buffer.Count >= 4)
                    size_buffer.RemoveAt (0);

                DirectoryInfo info = new DirectoryInfo (LocalPath);
                size_buffer.Add (CalculateSize (info));

                if (size_buffer.Count >= 4 &&
                    size_buffer [0].Equals (size_buffer [1]) &&
                    size_buffer [1].Equals (size_buffer [2]) &&
                    size_buffer [2].Equals (size_buffer [3])) {

                    Logger.LogInfo ("Local", Name + " | Activity has settled");
                    IsBuffering = false;

                    if (HasLocalChanges) {
                        do {
                            SyncUpBase ();

                        } while (HasLocalChanges);

                    } else {
                        SyncStatusChanged (SyncStatus.Idle);
                    }

                } else {
                    Thread.Sleep (500);
                }

            } while (IsBuffering);
        }


        protected void OnConflictResolved ()
        {
            ConflictResolved ();
        }


        protected void OnProgressChanged (double progress_percentage, string progress_speed)
        {
            if (progress_percentage < 1)
                return;

            // Only trigger the ProgressChanged event once per second
            if (DateTime.Compare (this.progress_last_change, DateTime.Now.Subtract (this.progress_change_interval)) >= 0)
                return;

            if (progress_percentage == 100.0)
                progress_percentage = 99.0;

            ProgressPercentage        = progress_percentage;
            ProgressSpeed             = progress_speed;
            this.progress_last_change = DateTime.Now;

            ProgressChanged (progress_percentage, progress_speed);
        }


        private void SyncUpBase ()
        {
            Logger.LogInfo ("SyncUp", Name + " | Initiated");
            HasUnsyncedChanges = true;

            SyncStatusChanged (SyncStatus.SyncUp);

            if (SyncUp ()) {
                Logger.LogInfo ("SyncUp", Name + " | Done");
           
                HasUnsyncedChanges = false;
                this.poll_interval = PollInterval.Long;

                SyncStatusChanged (SyncStatus.Idle);
                //this.listener.Announce (new SparkleAnnouncement (Identifier, CurrentRevision));

            } else {
                Logger.LogInfo ("SyncUp", Name + " | Error");
                SyncDownBase ();

                if (Error == ErrorStatus.None && SyncUp ()) {
                    HasUnsyncedChanges = false;

                    SyncStatusChanged (SyncStatus.Idle);
                    //this.listener.Announce (new SparkleAnnouncement (Identifier, CurrentRevision));

                } else {
                    this.poll_interval = PollInterval.Short;
                    SyncStatusChanged (SyncStatus.Error);
                }
            }

            ProgressPercentage = 0.0;
            ProgressSpeed      = "";


        }


        private void SyncDownBase ()
        {
            Logger.LogInfo ("SyncDown", Name + " | Initiated");

            SyncStatusChanged (SyncStatus.SyncDown);
            string pre_sync_revision = CurrentRevision;

            if (SyncDown ()) {
                Logger.LogInfo ("SyncDown", Name + " | Done");
                Error = ErrorStatus.None;

                // There could be changes from a resolved
                // conflict. Tries only once, then lets
                // the timer try again periodically
                if (HasUnsyncedChanges) {
                    SyncStatusChanged (SyncStatus.SyncUp);
                    
                    SyncUp ();
                    HasUnsyncedChanges = false;
                }

                SyncStatusChanged (SyncStatus.Idle);

            } else {
                Logger.LogInfo ("SyncDown", Name + " | Error");

                SyncStatusChanged (SyncStatus.Error);
            }

            ProgressPercentage = 0.0;
            ProgressSpeed      = "";

            SyncStatusChanged (SyncStatus.Idle);
        }


       /* private void CreateListener ()
        {
            this.listener = SparkleListenerFactory.CreateListener (Name, Identifier);

            if (this.listener.IsConnected) {
                this.poll_interval = PollInterval.Long;

                new Thread (() => {
                    if (!this.is_syncing && !HasLocalChanges && HasRemoteChanges)
                        SyncDownBase ();

                }).Start ();
            }

            this.listener.Connected            += ListenerConnectedDelegate;
            this.listener.Disconnected         += ListenerDisconnectedDelegate;
            this.listener.AnnouncementReceived += ListenerAnnouncementReceivedDelegate;

            // Start listening
            if (!this.listener.IsConnected && !this.listener.IsConnecting)
                this.listener.Connect ();
        }


        // Stop polling when the connection to the irc channel is succesful
        private void ListenerConnectedDelegate ()
        {
            this.poll_interval = PollInterval.Long;
            this.last_poll     = DateTime.Now;

            if (!this.is_syncing) {
                // Check for changes manually one more time
                if (HasRemoteChanges)
                    SyncDownBase ();

                // Push changes that were made since the last disconnect
                if (HasUnsyncedChanges)
                    SyncUpBase ();
            }
        }


        // Start polling when the connection to the channel is lost
        private void ListenerDisconnectedDelegate ()
        {
            this.poll_interval = PollInterval.Short;
            SparkleLogger.LogInfo (Name, "Falling back to polling");
        }


        // Fetch changes when there is an announcement
        private void ListenerAnnouncementReceivedDelegate (SparkleAnnouncement announcement)
        {
            string identifier = Identifier;

            if (announcement.FolderIdentifier.Equals (identifier) &&
                !announcement.Message.Equals (CurrentRevision)) {

                while (this.is_syncing)
                    Thread.Sleep (100);

                SparkleLogger.LogInfo (Name, "Syncing due to announcement");
                SyncDownBase ();

            } else {
                if (announcement.FolderIdentifier.Equals (identifier))
                    SparkleLogger.LogInfo (Name, "Not syncing, message is for current revision");
            }
        }*/


        // Recursively gets a folder's size in bytes
        private double CalculateSize (DirectoryInfo parent)
        {
            if (!Directory.Exists (parent.ToString ()))
                return 0;

            double size = 0;

            if (ExcludePaths.Contains (parent.Name))
                return 0;

            try {
                foreach (FileInfo file in parent.GetFiles ()) {
                    if (!file.Exists)
                        return 0;

                    size += file.Length;
                }

                foreach (DirectoryInfo directory in parent.GetDirectories ())
                    size += CalculateSize (directory);

            } catch (Exception) {
                return 0;
            }

            return size;
        }


        public void Dispose ()
        {
            this.remote_timer.Stop ();
            this.remote_timer.Dispose ();
        }
    }
}