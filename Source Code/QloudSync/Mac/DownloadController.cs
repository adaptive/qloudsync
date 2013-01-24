using System;
using System.Threading;
using QloudSync.Repository;
using System.IO;

namespace QloudSync
{
    public class DownloadController
    {
        private DownloadController ()
        {
        }

        private static DownloadController instance;
        private bool finished = false;
        private double downSize = 0;

        #region Events

        public event Action Started = delegate { };
        public event Action Failed = delegate { };


        public event ProgressChangedEventHandler ProgressChanged = delegate { };
        public delegate void ProgressChangedEventHandler (double percentage);

        public event FinishedEventHandler Finished = delegate { };
        public delegate void FinishedEventHandler ();



        #endregion

        public static DownloadController GetInstance ()
        {
            if (instance == null)
                instance = new DownloadController();
            return instance;
        }

        public void FirstLoad()
        {
            try
            {
                Thread downThread = new Thread(DownThreadMethod);
                double percentage = 0;
                downThread.Start ();
                
                while (percentage < 100)
                {
                    if (finished)
                        break;
                    
                    double repoSize = LocalRepo.Size;
                    
                    if (downSize != 0)
                        percentage = (repoSize / downSize)*100;
                    ProgressChanged (percentage);
                    Thread.Sleep (1000);
                }
            }
            catch (Exception e)
            {
                Logger.LogInfo("First Load", e);
            }
        }

        void DownThreadMethod ()
        {
            finished = false;
            if (RemoteRepo.InitBucket ()) {
                if(RemoteRepo.InitTrashFolder ()){
                    System.Collections.Generic.List<RemoteFile> remoteFiles = RemoteRepo.Files;
                    foreach (RemoteFile remoteFile in remoteFiles) {
                        if (!remoteFile.IsIgnoreFile)
                            downSize += remoteFile.AsS3Object.Size;
                    }
                    foreach (RemoteFile remoteFile in remoteFiles) {
                        if(remoteFile.IsAFolder)
                            Directory.CreateDirectory (remoteFile.FullLocalName);
                        else
                        {
                            if (!remoteFile.IsIgnoreFile)
                                RemoteRepo.Download (remoteFile);
                        }
                    }
                    //BacklogSynchronizer.GetInstance().Write();
                }
            }
            finished = true;
        }



    }
}
