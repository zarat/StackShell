using System.Collections.ObjectModel;
using System.Diagnostics;
using ScriptStack.Runtime;

namespace ScriptStack
{

    public class HyperV : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines = null;

        public HyperV()
        {

            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();

            List<Type> vmParams = new List<Type>();
            vmParams.Add(typeof(string)); //vmname
            vmParams.Add(typeof(string)); // vmpath
            vmParams.Add(typeof(string)); // switch name
            vmParams.Add(typeof(string)); // memory GB
            routines.Add(new Routine((Type)null, "createVM", vmParams, "Erstelle eine neue VM. vmname, vmpath, switchName, memory."));
            routines.Add(new Routine((Type)null, "createDisk", typeof(string), typeof(string), "Erstelle eine neue VHDX. vhdxPath, vhdxSize.")); // vhdpath, vhdsize
            routines.Add(new Routine((Type)null, "assignDisk", typeof(string), typeof(string), "Weise eine VHDX einer VM zu. vmName, vhdxPath.")); // vhdpath, vhdsize
            routines.Add(new Routine((Type)null, "removeVM", typeof(string), "Entferne eine VM. vmName.")); // vmName
            routines.Add(new Routine((Type)null, "removeDisk", typeof(string), typeof(string), "Entferne eine VHDX von einer VM. vmName, vhdxPath.")); // vhdpath, vhdsize
            routines.Add(new Routine((Type)null, "addIsoImage", typeof(string), typeof(string), "Lege ein ISO Image in das DVD Laufwerk einer VM. vmName, isoPath.")); // vhdpath, vhdsize

            exportedRoutines = routines.AsReadOnly();


        }

        public object Invoke(String strFunctionName, List<object> listParameters)
        {

            if (strFunctionName == "createVM")
            {

                /*
                using (var app = new Process())
                {
                    app.StartInfo.FileName = "powershell.exe";
                    app.StartInfo.Arguments = "New-VM -Name \"Neue VM\" -Path \"D:\\NeueVM\" -MemoryStartupBytes 2GB -BootDevice CD -SwitchName \"Default Switch\"";
                    app.EnableRaisingEvents = true;
                    app.StartInfo.RedirectStandardOutput = true;
                    app.StartInfo.RedirectStandardError = true;
                    // Must not set true to execute PowerShell command
                    app.StartInfo.UseShellExecute = false;
                    app.Start();
                    using (var o = app.StandardOutput)
                    {
                        return o.ReadToEndAsync();
                    }
                }
                */

                string vmName = (string)listParameters[0];
                string vmPath = (string)listParameters[1];
                string switchName = (string)listParameters[2];
                string memoryBytes = (string)listParameters[3]; // 2GB

                bool ok = true;

                string powerShellScript = $@"
New-VM -Name '{vmName}' -Path '{vmPath}' -MemoryStartupBytes {memoryBytes} -SwitchName '{switchName}'
";

                string powerShellExe = @"powershell.exe";

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }
                }

                if(ok)
                {
                    Console.WriteLine("VM '" + vmName + "' erfolgreich erstellt!");
                }

            }

            if (strFunctionName == "createDisk")
            {

                string vhdPath = (string)listParameters[0];
                string vhdSize = (string)listParameters[1];

                bool ok = true;

                string powerShellScript = $@"
New-VHD -Path '{vhdPath}' -SizeBytes {vhdSize} -Dynamic
";

                string powerShellExe = @"powershell.exe";

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }
                    
                }

                Console.WriteLine("VHDX '" + vhdPath + "' erfolgreich erstellt!");

            }

            if (strFunctionName == "assignDisk")
            {

                string vmName = (string)listParameters[0];
                string vhdPath = (string)listParameters[1];

                bool ok = true;

                string powerShellScript = $@"
Add-VMHardDiskDrive -VMName '{vmName}' -Path '{vhdPath}'
";

                string powerShellExe = @"powershell.exe";

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }
                    
                }

                Console.WriteLine("VHDX '" + vhdPath + "' erfolgreich in '" + vmName + "' eingehängt!");

            }

            if(strFunctionName == "removeVM")
            {

                string vmName = (string)listParameters[0];

                string powerShellScript = $@"
Remove-VM -VMName '{vmName}' -Force
";
                bool ok = true;

                string powerShellExe = @"powershell.exe";

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }   

                }

                if (ok)
                {
                    Console.WriteLine("VM \"" + vmName + "\" erfolgreich entfernt!");
                }

            }

            // TODO
            if (strFunctionName == "removeDisk")
            {

                string vmName = (string)listParameters[0];
                string vhdPath = (string)listParameters[1];

                string powerShellScript = $@"
Remove-VMHardDiskDrive -VMName '{vmName}' -ControllerType IDE -ControllerNumber 0 -ControllerLocation 0
";

                string powerShellExe = @"powershell.exe";

                bool ok = true;

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }

                }

                if (ok)
                {
                    Console.WriteLine("VHDX erfolgreich von '" + vmName + "' entfernt!");
                }

            }

            if (strFunctionName == "addIsoImage")
            {

                string vmName = (string)listParameters[0];
                string isoPath = (string)listParameters[1];

                string powerShellScript = $@"
Add-VMDvdDrive -VMName '{vmName}' -Path '{isoPath}'
";

                string powerShellExe = @"powershell.exe";

                bool ok = true;

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = powerShellExe,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();

                    process.StandardInput.WriteLine(powerShellScript);
                    process.StandardInput.Close();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("PowerShell-Fehler: " + error);
                        ok = false;
                    }

                }

                if (ok)
                {
                    Console.WriteLine("ISO Image '" + isoPath + "' erfolgreich in '" + vmName + "' eingelegt!");
                }

            }


            return null;

        }

        public ReadOnlyCollection<Routine> Routines
        {
            get
            {
                return exportedRoutines;
            }
        }

    }

}
