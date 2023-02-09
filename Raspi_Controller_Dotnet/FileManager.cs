using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.IO;
using System.IO.Compression;
using System.Net;

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
            return (fb & FileAttributes.System) != 0;
        }
        public bool IsHidden(string filename)
        {
            FileAttributes fb = File.GetAttributes(filename);
            return (fb & FileAttributes.Hidden) != 0;
        }
        const long MaxZipSizeTotalBytes = 536870912; // 500MB max
        public long GetFolderSize(string directory)
        {
            long total = 0;
            foreach(string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) 
            {
                total += new FileInfo(file).Length;
                if (total > MaxZipSizeTotalBytes) return MaxZipSizeTotalBytes;
            }
            return total;
        }
        public void ZipFileTo(string directory, HttpListenerContext ctx, bool includeSystemFiles, bool includeHidden)
        {
            using (var archive = new ZipArchive(ctx.Response.OutputStream, ZipArchiveMode.Create, true))
            {
                long downloadedSize = 0;
                foreach(string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (!includeSystemFiles && IsProtected(file)) continue;
                    else if(!includeHidden&& IsHidden(file)) continue;
                    long len = new FileInfo(file).Length;
                    downloadedSize += len;
                    if(downloadedSize > MaxZipSizeTotalBytes || !ctx.Response.OutputStream.CanWrite)
                    {
                        break;
                    }
                    ZipArchiveEntry entry = archive.CreateEntry(Path.GetRelativePath(directory, file),CompressionLevel.SmallestSize);
                    try
                    {
                        using (FileStream fs = File.OpenRead(file))
                        using (Stream s = entry.Open())
                        {
                            fs.CopyTo(s);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            ctx.Response.Close();
        }
        public bool IsDirectory(string filename)
        {
            return (File.GetAttributes(filename) & FileAttributes.Directory) == FileAttributes.Directory;
        }
    }
}
