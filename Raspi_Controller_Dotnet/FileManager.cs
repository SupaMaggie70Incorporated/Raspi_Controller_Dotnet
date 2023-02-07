using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.IO;

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
    }
}
