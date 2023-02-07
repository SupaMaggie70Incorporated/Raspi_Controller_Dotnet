using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Raspi_Controller_Dotnet
{
    public class TerminalManager
    {
        public List<Terminal> Terminals;


        private string Bash_Path;
        public TerminalManager() {
            if (Program.IsWindows) Bash_Path = "CMD.exe";
            else if (Program.IsLinux) Bash_Path = "/bin/bash";
        }
        public Terminal RunCommand(string command, bool admin = false)
        {
            Process process = new Process();
            process.StartInfo.FileName = Bash_Path;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Arguments = command;
            if (admin) process.StartInfo.Verb = "runas";

            process.Start();
            Terminal t = new Terminal(process, admin);
            Terminals.Add(t);
            return t;
        }
        public void CloseTerminals()
        {
            foreach(Terminal t in Terminals)
                t.Kill();
        }
        public void Update()
        {

        }
    }
    public class Terminal
    {
        public Process Process;
        public bool IsAdmin;
        public Terminal(Process process, bool admin)
        {
            Process = process;
            IsAdmin = admin;
        }

        public void Exit()
        {
            Kill();
            Program.TerminalManager.Terminals.Remove(this);
        }
        public void Kill()
        {
            Program.Log($"Closing program {Process.StartInfo.FileName}");
            Process.Kill();
        }
    }
}
