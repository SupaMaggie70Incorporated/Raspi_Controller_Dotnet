using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.IO;
using System.IO.Compression;

namespace Raspi_Controller_Dotnet
{
    public class FileManager
    {
        public FileManager() { 

        }
        public void UploadFile(string filename, Stream input)
        {
            FileStream fs = File.OpenWrite(filename);
            input.CopyTo(fs);
            fs.Flush();
            fs.Dispose();
        }
        public void ReadFileTo(string filename, Stream output)
        {
            FileStream fs = File.OpenRead(filename);
            fs.CopyTo(output);
            fs.Dispose();
            output.Flush();
        }
        public bool IsProtected(string filename)
        {
            FileAttributes fb = File.GetAttributes(filename);
            return (fb & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        public void ZipFileTo(string directory, Stream output, bool includeSystemFiles)
        {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, false))
            {
                foreach(string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (!includeSystemFiles && IsProtected(file)) continue;
                    ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(file));
                    using (Stream s = entry.Open())
                    using (FileStream fs = File.OpenRead(file))
                    {
                        fs.CopyTo(s);
                        s.Flush();
                    }
                }
            }
        }
        public bool IsDirectory(string filename)
        {
            return (File.GetAttributes(filename) & FileAttributes.Directory) == FileAttributes.Directory;
        }
    }
}
