using System;
using System.Collections.Generic;
using System.Linq;
using  SQ.Synchrony;
using SQ.Repository;


namespace  SQ.IO
{
    public class OSXFileWatcher
    {
        
        List<System.IO.FileSystemWatcher> watchers = new List<System.IO.FileSystemWatcher>();
        List <string> triggers = new List<string>();
        List<string> eventshandled = new List<string>(); 
        public DateTime LastTimeChanges {
            get;
            set;
        }
        
        DateTime LastTimeCatch;
        
        public OSXFileWatcher (string repo_address)
        {
            LocalRepo.PendingChanges = new List<Change> ();
            RemoteRepo.FilesChanged = new List<File> ();
            
            
            new System.Threading.Thread(ListenEvents).Start();
            CreateWatcher (repo_address);
        }
        
        public void ListenEvents ()
        {
            while (true) {
                if (LastTimeCatch == new DateTime())
                    continue;
                if(Catching)
                    continue;
                if (triggers.Count != 0){
                    string path = triggers [0];
                    if (!eventshandled.Where (eh => eh == triggers[0]).Any ())
                    {
                        if(HandleCreates (path)){
                            eventshandled.Add (triggers[0]);
                            
                            LastTimeChanges = DateTime.Now;
                            triggers.RemoveAt (0);
                        }
                    }
                }
                try{
                    foreach(Change c in LocalRepo.PendingChanges){
                        if (eventshandled.Where(eh => eh == c.File.FullLocalName && (c.Event == System.IO.WatcherChangeTypes.Created || c.Event == System.IO.WatcherChangeTypes.Renamed)).Any())
                            eventshandled.Remove(c.File.FullLocalName);
                    }
                }
                catch{
                    continue;
                }
            }
        }
        
        void CreateWatcher (string folder_path)
        {
            if ( folder_path.Contains (".app/") || folder_path.EndsWith(".app"))
                return;
            Console.WriteLine (DateTime.Now.ToUniversalTime ()+" - Creating a watcher to "+folder_path+"\n");
            System.IO.DirectoryInfo d = new System.IO.DirectoryInfo (folder_path);
            
            System.IO.FileSystemWatcher f = new System.IO.FileSystemWatcher(d.FullName, "*.*");
            f.NotifyFilter = System.IO.NotifyFilters.DirectoryName | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.LastWrite;
            f.Changed += HandleChanges; 
            f.Deleted += HandleChanges;
            f.Created += HandleChanges;
            f.EnableRaisingEvents = true;
            
            foreach (System.IO.DirectoryInfo dir in d.GetDirectories())
                CreateWatcher (dir.FullName);
            
            watchers.Add (f);
        }
        
        #region Events
        void HandleChanges (object sender, System.IO.FileSystemEventArgs e)
        {
            if (e==null)
                return;
            LocalFile localFile = new LocalFile (e.FullPath);
            if (localFile.IsIgnoreFile)
            {   
                return;
            }
            if (BacklogSynchronizer.GetInstance ().ChangesMade.Count > 0) {
                if (BacklogSynchronizer.GetInstance ().ChangesMade.Where (f => f.FullLocalName == e.FullPath).Any ()) {    
                    BacklogSynchronizer.GetInstance ().ChangesMade.RemoveAll (f => f.FullLocalName == e.FullPath);
                    return;
                }
            }
            switch (e.ChangeType) {
            case System.IO.WatcherChangeTypes.Created:
                triggers.Add (e.FullPath);
                LastTimeCatch = DateTime.Now;
                break;
            case System.IO.WatcherChangeTypes.Deleted:
                HandleDelete();
                break;
            case System.IO.WatcherChangeTypes.Changed:
                LocalRepo.PendingChanges.Add 
                    (new Change(new LocalFile(e.FullPath), e.ChangeType));
                break;
            }
        }
        
        bool HandleCreates (string path)
        { 
            if (path == null) {
                //Console.WriteLine ("Debug - HandleCreate received a null value");
                return false;
            }
            if (RemoteRepo.FilesChanged.Count > 0) {
                if (RemoteRepo.FilesChanged.Where (df => df.FullLocalName == path).Any ())
                    return true;
            }
            
            if (System.IO.Directory.Exists (path)) {
                CreateFolder (path);
            }
            else {  
                LocalFile lf = new LocalFile (path); 
                HandleDelete ();
                CreateFile (lf);
            }
            return true;
        }
        
        bool HandleMoveEvent (LocalFile file)
        {
            File deletedFile =  LocalRepo.Files.Where (f => f.Deleted 
                                                       && f.RelativePath != file.RelativePath
                                                       && f.Name == file.Name
                                                       && f.MD5Hash == file.MD5Hash
                                                       && Math.Abs (file.TimeOfLastChange.Subtract (f.TimeOfLastChange).TotalSeconds) <= 3
                                                       ).FirstOrDefault();
            return HandleMove (file, deletedFile);
        }
        
        bool HandleRenameEvent (LocalFile file)
        {
            File deletedFile =  LocalRepo.Files.Where (f => f.Deleted 
                                                       && f.RelativePath == file.RelativePath
                                                       && f.Name != file.Name
                                                       && f.MD5Hash == file.MD5Hash
                                                       && Math.Abs (file.TimeOfLastChange.Subtract (f.TimeOfLastChange).TotalSeconds) <= 3
                                                       ).FirstOrDefault();
            
            return HandleMove (file, deletedFile);
        }
        
        bool HandleMove (SQ.Repository.File newVersion, SQ.Repository.File oldVersion)
        {
            if (oldVersion == null)
                return false;
            List<Change> oldcreates = LocalRepo.PendingChanges.Where (c=> c.File.FullLocalName == oldVersion.FullLocalName && c.Event == System.IO.WatcherChangeTypes.Created).ToList<Change>();
            List<Change> oldrenames = LocalRepo.PendingChanges.Where (c=> c.File.FullLocalName == oldVersion.FullLocalName && c.Event == System.IO.WatcherChangeTypes.Renamed).ToList<Change>();
            if(oldrenames.Count > 0){
                //apagar o rename antigo
                Change renameChange = oldrenames [0];
                Console.WriteLine (DateTime.Now.ToUniversalTime () + " - Replace rename - " + renameChange.File.FullLocalName +" to "+newVersion.FullLocalName);
                
                // apagar o delete que inicou o rename atual
                Change deletedChange = LocalRepo.PendingChanges.Where (c=> c.File.AbsolutePath == oldVersion.AbsolutePath && c.Event == System.IO.WatcherChangeTypes.Deleted).First();
                LocalRepo.PendingChanges.Remove (deletedChange);
                LocalRepo.Files.Remove (oldVersion);
                
                oldVersion = renameChange.File.OldVersion;
                LocalRepo.PendingChanges.Remove (renameChange);
                LocalRepo.Files.Remove (renameChange.File);
                //create o novo rename
                
                newVersion.OldVersion = oldVersion;
                LocalRepo.PendingChanges.Add (new Change (newVersion, System.IO.WatcherChangeTypes.Renamed));
                LocalRepo.Files.Add (newVersion);
            }
            else
                if (oldcreates.Count>0)
            {
                Console.WriteLine (DateTime.Now.ToUniversalTime () + " - Creating new version - " + oldVersion.FullLocalName +" to "+newVersion.FullLocalName);
                Change deletedChange = LocalRepo.PendingChanges.Where (c=> c.File.AbsolutePath == oldVersion.AbsolutePath && c.Event == System.IO.WatcherChangeTypes.Deleted).First();
                LocalRepo.PendingChanges.Remove (deletedChange);
                LocalRepo.Files.Remove (oldVersion);
                
                Change createOldChange = oldcreates[0];
                LocalRepo.Files.Remove (createOldChange.File);
                LocalRepo.PendingChanges.Remove (createOldChange);
                
                if (oldVersion.TimeOfLastChange.Subtract(Repo.LastSyncTime).TotalSeconds >0){
                    newVersion.OldVersion = createOldChange.File;
                    LocalRepo.PendingChanges.Add (new Change(newVersion, System.IO.WatcherChangeTypes.Renamed));
                }
                else{
                    LocalRepo.PendingChanges.Add (new Change(newVersion, System.IO.WatcherChangeTypes.Created));
                }
                LocalRepo.Files.Add (newVersion);
                
                
            }
            else
            {
                newVersion.OldVersion = oldVersion;
                Change deletedChange = LocalRepo.PendingChanges.Where (c=> c.File.AbsolutePath == oldVersion.AbsolutePath && c.Event == System.IO.WatcherChangeTypes.Deleted).First();
                LocalRepo.PendingChanges.Remove ( deletedChange);
                LocalRepo.PendingChanges.Add (new Change (newVersion, System.IO.WatcherChangeTypes.Renamed));
                LocalRepo.Files.Remove (oldVersion);
                LocalRepo.Files.Add (newVersion);
            }
            return true;
        }
        
        void HandleDelete ()
        {
            try {
                foreach (SQ.Repository.File lf in LocalRepo.Files) {
                    
                    if (RemoteRepo.FilesChanged.Where (f => f.AbsolutePath == lf.AbsolutePath).Any ())
                        continue;
                    
                    if (!lf.ExistsInLocalRepo && !lf.Deleted) {
                        //se tiver algum pendingchange antes do delete,apaga
                        List<Change> deprecatedChanges = LocalRepo.PendingChanges.Where (c => c.File.AbsolutePath == lf.AbsolutePath && c.Event != System.IO.WatcherChangeTypes.Deleted).ToList<Change> ();
                        foreach (Change ch in deprecatedChanges)
                            LocalRepo.PendingChanges.Remove (ch);
                        lf.Deleted = true;
                        lf.TimeOfLastChange = DateTime.Now;
                        LocalRepo.PendingChanges.Add 
                            (new Change (lf, System.IO.WatcherChangeTypes.Deleted));
                    }
                }
                ClearDeletes ();
                UpdateWatchers ();
            } catch (InvalidOperationException) {
                SQ.Util.Logger.LogInfo ("Debug", "Collection was modified; HandleDelete");
                HandleDelete();
            }
        }
        
#endregion
        
        void CreateFolder (string folder_path)
        {
            if (folder_path.Contains (".app/") || folder_path.EndsWith (".app"))
                return;
            CreateWatcher (folder_path);
            LocalFile folder = new LocalFile (folder_path);
            if (System.IO.Directory.GetFiles (folder_path).Count () == 0 && System.IO.Directory.GetDirectories (folder_path).Count () == 0)
                LocalRepo.PendingChanges.Add 
                    (new Change (folder, System.IO.WatcherChangeTypes.Created));
            else {
                foreach (string fileName in System.IO.Directory.GetFiles(folder_path)) {
                    LocalFile lf = new LocalFile (fileName);
                    if (!lf.IsIgnoreFile)
                        CreateFile (lf);
                }
                foreach (string folderName in System.IO.Directory.GetDirectories(folder_path)) {
                    CreateFolder (folderName);
                }            
            }
            
            LocalRepo.Files.Add (folder); 
        }
        
        void CreateFile (LocalFile file)
        {
            
            bool createDeprecated = HandleMoveEvent (file);
            createDeprecated |= HandleRenameEvent (file);
            if (!createDeprecated) {
                Console.WriteLine (DateTime.Now.ToUniversalTime ()+" - Create File: "+file.FullLocalName);
                file.TimeOfLastChange = DateTime.Now;
                LocalRepo.PendingChanges.Add 
                    (new Change (file, System.IO.WatcherChangeTypes.Created));
            } 
            
            if (!LocalRepo.Files.Where (f => f.FullLocalName == file.FullLocalName && file.MD5Hash == f.MD5Hash).Any ())
                LocalRepo.Files.Add (file);
            
            
        }
        
        void UpdateWatchers ()
        {
            int c = 0;
            while (c < watchers.Count)
            {
                System.IO.FileSystemWatcher w = watchers[c];
                if(!new System.IO.DirectoryInfo(w.Path).Exists){
                    Console.WriteLine (DateTime.Now.ToUniversalTime ()+" - Remove watcher: "+w.Path);
                    w.Dispose();
                    watchers.Remove (w);
                    w = null;
                    LocalRepo.PendingChanges.Add(new Change(new Folder(w.Path), System.IO.WatcherChangeTypes.Deleted));
                }
                else c++;
            }
            Console.WriteLine ();
        }
        /*
         * retira da lista de apagados os arquivos que foram excluidos ha mais de uma hora
         * ou retira outras versoes do arquivo deletado da lista
         */
        void ClearDeletes ()
        {
            List<SQ.Repository.File> filesMustBeDeleted = new List<File> ();
            List<SQ.Repository.File> oldDeleteFiles = LocalRepo.Files.Where (odf => odf.Deleted && DateTime.Now.Subtract (odf.TimeOfLastChange).TotalSeconds > 60).ToList<File> ();
            foreach (LocalFile deletedFile in oldDeleteFiles)
                filesMustBeDeleted.Add (deletedFile);
            lock (LocalRepo.Files) {
                oldDeleteFiles = LocalRepo.Files.Where (odf => odf.Deleted).ToList<SQ.Repository.File> ();
                foreach (SQ.Repository.File f in oldDeleteFiles) {
                    while (LocalRepo.Files.Count(df => df.FullLocalName == f.FullLocalName && df.Deleted) > 1) {
                        SQ.Repository.File deletedversion = LocalRepo.Files.Where (df => df.FullLocalName == f.FullLocalName && df.Deleted).OrderBy (df => df.TimeOfLastChange).First ();
                        filesMustBeDeleted.Add (deletedversion);
                    }
                }
            }
            foreach (File fmbd in filesMustBeDeleted)
                LocalRepo.Files.Remove(fmbd);
        }
        
        
        public bool Catching {
            get {
                return DateTime.Now.Subtract (LastTimeCatch).TotalSeconds < 1;
            }
        }
        
        public bool IsIdle {
            get {
                return DateTime.Now.Subtract (LastTimeChanges).TotalSeconds > 1 && !Catching;
            }
        }
    }
}

