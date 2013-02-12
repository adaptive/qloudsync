using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using GreenQloud.Synchrony;
using GreenQloud.Util;


 namespace GreenQloud.Repository
{
    public class LocalRepo
    {
		private static List<File> files = null;

        private LocalRepo ()
        {
        }
       

        public static List<File> Files {
			set {
				files = value;
			}
			get {
				if(files==null)
					files = GetFiles();
				return files;
			}
		}

		public static List<File> GetFiles ()
		{
			try {
                System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo (RuntimeSettings.HomePath);
				List<File> list = LocalFile.Get (dir.GetFiles ("*", System.IO.SearchOption.AllDirectories).ToList ());
				list.AddRange (Folder.Get (dir.GetDirectories ("*", System.IO.SearchOption.AllDirectories).ToList ()));

				return list;
			} catch (System.ArgumentNullException) {
				Logger.LogInfo("Error", "Set a LocalFolder variable");
				return null;
			}
		}

        public static List<Folder> EmptyFolders {
            get {
                List<DirectoryInfo> emptyDirectories = new DirectoryInfo (RuntimeSettings.HomePath)
                .GetDirectories ("*", SearchOption.AllDirectories)
                    .Where (d => !Directory.EnumerateFileSystemEntries (d.FullName).Any ()).ToList ();
                List<Folder> emptyFolders = new List<Folder> ();
                foreach (DirectoryInfo dir in emptyDirectories) 
                    emptyFolders.Add (new Folder (dir.FullName + Constant.DELIMITER));
                return emptyFolders;
            }
        }
       
        public static string ResolveDecodingProblem (string path)
        {
            bool haveProblem = false;
            string old = char.ConvertFromUtf32(97)+""+char.ConvertFromUtf32(769);
            string _new = char.ConvertFromUtf32(225).ToString();
            if (!path.Contains (old)) return path;
            char[] chars = path.ToCharArray ();
            for (int  c = 0; c < chars.Count()-1; c++) {
                if ((int)chars [c] == 97 && (int)chars [c + 1] == 769) {    
                    haveProblem = true;
                    break;
                }
            }
            
            if (haveProblem) {
                path = path.Replace (old,_new);
            }
            return path;
        }
    }
}
