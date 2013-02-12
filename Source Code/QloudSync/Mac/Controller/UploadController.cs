using System;
using System.Collections.Generic;
using GreenQloud.Repository;
using System.Threading;
using System.IO;
using GreenQloud.Synchrony;
using System.Linq;

namespace GreenQloud
{
    public class UploadController : SynchronizerController
    {
        public ChangesCapturedByWatcher<GreenQloud.Repository.File> PendingChanges;
        OSXFileSystemWatcher watcher = new OSXFileSystemWatcher ();
        int controller = 0;

        protected UploadController ()
        {
            try {

            
                watcher.Changed += delegate(string path) {
                    PendingChanges.Add (new LocalFile (path));
                };
                PendingChanges = new ChangesCapturedByWatcher<GreenQloud.Repository.File> ();
                PendingChanges.OnAdd += HandleOnAdd;
            } catch (Exception e) {
                Logger.LogInfo("uploadcontroller", e);
            }
        }

        void HandleOnAdd (object sender, EventArgs e)
        {
            new Thread (Synchronize).Start ();
        }

        private static UploadController instance;
        public static UploadController GetInstance(){
            if(instance == null)
                instance = new UploadController();
            return instance;
        }

        public override void Synchronize ()
        {
            ++controller;
            if (Status == SyncStatus.Idle && controller == 1) {
                int index = 0;
                while (PendingChanges.Count != 0) {
                    
                    if (index >= PendingChanges.Count)
                        break;
                    GreenQloud.Repository.File pendingFile = PendingChanges[index];
                    if (UploadSynchronizer.GetInstance().Synchronize(pendingFile))
                    {   
                        PendingChanges.Remove (pendingFile); 
                        index = 0;
                    }
                    else 
                        index++;
                }
                controller = 0;
            }            
        }

        public class ChangesCapturedByWatcher <File> : List<GreenQloud.Repository.File>
        {
            public event EventHandler OnAdd;
            
            public new void Add(GreenQloud.Repository.File item) {
                if (null != OnAdd) {
                    OnAdd(this, null);                
                }
                if(item.FullLocalName==RuntimeSettings.HomePath)
                    return;
                if (!this.Any(f=>f.FullLocalName==item.FullLocalName || item.FullLocalName.Contains(f.FullLocalName+".")))
                    base.Add(item);
            }
        }       
    }
}