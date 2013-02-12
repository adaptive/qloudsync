using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
using GreenQloud.Net.S3;
using GreenQloud.Util;

namespace GreenQloud.Repository
{
    public class RemoteRepo
    {
        private ConnectionManager connection; 

        public RemoteRepo ()
        {
            this.connection =  new ConnectionManager (DefaultBucketName);
        }

        public ConnectionManager Connection {
            get {
                return connection;
            }
        }

		#region Files
        public List<RemoteFile> Files{ 
            get {
                return AllFiles.Where(sobj => !IsTrashFile (sobj) && sobj.Name != Constant.CLOCK_TIME ).ToList();
            } 
        }

        public List<RemoteFile> AllFiles{ 
            get {
                return GetRemoteFiles (connection.GetFiles());
            } 
        }

		protected List<RemoteFile> GetRemoteFiles (List<S3Object> files)
		{
			List <RemoteFile> remoteFiles = new List <RemoteFile> ();
			
			foreach (S3Object file in files) {
                remoteFiles.Add (new RemoteFile (file));
			}
			return remoteFiles;
		}

        public List<RemoteFile> TrashFiles {
            get{
                return AllFiles.Where(f => IsTrashFile(f)).ToList();
            } 
        }


		#endregion

		#region Auxiliar

        public bool HasChanges{ set; get; }

        public  bool ExistsTrashFolder {
            get {
                return  TrashFiles.Any(rm => IsTrashFile(rm));
            }
        }

        public TimeSpan DiffClocks 
        {
            get{
                return connection.CalculateDiffClocks();
            }
        }

        public bool Connected {
            get {
                return connection.Reconnect () != null;
            }
        }

        public static string DefaultBucketName {
            get 
            {
                return string.Concat(Credential.Username, GlobalSettings.SuffixNameBucket);
            }
        }

		#endregion

        public bool InitBucket ()
        {
            if (!connection.ExistsBucket)
                return connection.CreateBucket();
            
            else return true; 
        }

        public bool InitTrashFolder ()
        {
            if (!ExistsTrashFolder) {
                return connection.CreateTrashFolder ();
            }
            else return true;
        }

        public void Download (RemoteFile remoteFile)
        {
            //TODO mover isso para a chamada do metodo
            //if (!IsTrashFile (remoteFile) && !LocalRepo.PendingChanges.Where (c => c.File.FullLocalName == remoteFile.FullLocalName && c.Event == System.IO.WatcherChangeTypes.Deleted).Any())
                connection.Download (remoteFile);
        }


        public void Upload (File file)
        {
            if (!System.IO.File.Exists(file.FullLocalName))
            {
                Logger.LogInfo ("Upload", string.Format("Could not upload {0} because it does not exist in local repository.",file.AbsolutePath));
                return;
            }
            
            connection.GenericUpload ( file.RelativePathInBucket,  file.Name,  file.FullLocalName);
        }

        public void CreateFolder (Folder folder)
        {
            connection.CreateFolder (folder);
        }

        public void Copy (File source, File destination)
        {
            if (source.InTrash)
                connection.GenericCopy (DefaultBucketName, source.TrashAbsolutePath, destination.RelativePathInBucket, destination.Name);
            else
                connection.GenericCopy (DefaultBucketName, source.AbsolutePath, destination.RelativePathInBucket, destination.Name);
        }

        public void Move (File source, File destination)
        {
            string destinationName;
            if (source.IsAFolder)
                destinationName = source.Name;
            else
                destinationName = source.Name+"(0)";
            
            connection.GenericCopy (DefaultBucketName, source.AbsolutePath, source.TrashRelativePath, destinationName);
            if (Files.Any (rf => rf.Name == source.Name))
                connection.GenericDelete (source.AbsolutePath);
        }
        
        public void MoveToTrash (File  file)
        {

            string destinationName;
            if (file.IsAFolder)
                destinationName = file.Name;
            else
                destinationName = file.Name + "(1)";
           
            UpdateTrashFolder (file);
            connection.GenericCopy (DefaultBucketName, file.AbsolutePath, file.TrashRelativePath, destinationName);           
            connection.GenericDelete (file.AbsolutePath);
        }

        public bool FolderExistsInBucket (Folder folder)
        {
            return Files.Where (rf => rf.AbsolutePath.Contains (folder.AbsolutePath)).Any ();
        }

        public bool ExistsInBucket (File file)
        {
            return Files.Where (rf => rf.AbsolutePath == file.AbsolutePath).Any ();
        }
        
        public bool IsTrashFile (RemoteFile file)
        {
            return file.InTrash;
        }

        public bool SendToTrash (LocalFile file)
        {
            if (!System.IO.File.Exists(file.FullLocalName))
                return false;
            
            string key;
            if ( file.IsAFolder) {
                key =  file.Name;
            } else {
                key =  file.Name + "(0)";
            }
            Logger.LogInfo ("Connection","Uploading the file "+key+" to trash.");
            connection.GenericUpload ( file.TrashRelativePath, key,  file.FullLocalName);
            Logger.LogInfo ("Connection","File "+file.Name+" was sent to trash folder.");
            UpdateTrashFolder (file);

            return true;
        }
        
        public bool SendToTrash (RemoteFile file)
        {
            string destinationName;
            if (file.IsAFolder)
                destinationName = file.Name;
            else
                destinationName = file.Name+"(0)";
            
            connection.GenericCopy (DefaultBucketName, file.AbsolutePath, file.TrashRelativePath, destinationName);
            bool copySucessfull =  TrashFiles.Any (rm => file.AbsolutePath+"(0)" == rm.AbsolutePath);
            if (copySucessfull)
                UpdateTrashFolder (file);
            
            return copySucessfull;
        }
        
        public void UpdateTrashFolder (File  file)
        {
            if (file.IsAFolder)
                return;
            List<RemoteFile> versions = TrashFiles.Where (tf => tf.AbsolutePath.Substring(0, tf.AbsolutePath.Length-3)== file.AbsolutePath).OrderByDescending(t => t.AbsolutePath).ToList<RemoteFile> ();

            int overload = versions.Count-2;
            for (int i=0; i<overload; i++) {
                RemoteFile v = versions[i];
                connection.GenericDelete(v.TrashAbsolutePath);
                versions.Remove(v);
            }

            foreach (RemoteFile version in versions) {
                if(version.AbsolutePath == string.Empty)
                    continue;
                int lastOpenParenthesis = version.AbsolutePath.LastIndexOf ("(")+1;
                int lastCloseParenthesis = version.AbsolutePath.LastIndexOf (")");
                int versionNumber = int.Parse (version.AbsolutePath.Substring (lastOpenParenthesis, lastCloseParenthesis - lastOpenParenthesis));
                versionNumber++;
                string newName = string.Format ("{0}{1})", version.AbsolutePath.Substring (0, lastOpenParenthesis), versionNumber);
                RemoteFile newVersion = new RemoteFile (newName);
                connection.GenericCopy (DefaultBucketName, version.TrashAbsolutePath, newVersion.TrashRelativePath, newVersion.Name);
                connection.GenericDelete (version.TrashAbsolutePath);
            }

        }

		public void Delete (RemoteFile file)
		{
			connection.GenericDelete (file.AbsolutePath);
		}
        
        private List<RemoteFile> GetVersionsOrderByLastModified (File  file)  {
            return  TrashFiles.Where (ft => ft.AbsolutePath.Contains (file.AbsolutePath)).OrderBy(ft => ft.AsS3Object.LastModified).ToList<RemoteFile>();
        }

        public void DeleteAllFilesInBucket(){
			connection.DeleteAllFilesInBucket();
		}       
    }
}
