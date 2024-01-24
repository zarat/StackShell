using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Xml.Linq;
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
            
            List<Type> assignDiskParams = new List<Type>();
            assignDiskParams.Add(typeof(string));  // VM Name
            assignDiskParams.Add(typeof(string)); // Path to disk
            assignDiskParams.Add(typeof(string)); // Controller type
            assignDiskParams.Add(typeof(int)); // Controller Index
            assignDiskParams.Add(typeof(int)); // Slot Index
            routines.Add(new Routine((Type)null, "assignDisk", assignDiskParams, "Weise eine VHDX einer VM zu. vmName, vhdxPath, controllertype, controllerindex, slot.")); // vhdpath, vhdsize
            
            routines.Add(new Routine((Type)null, "removeVM", typeof(string), "Entferne eine VM. vmName.")); // vmName
            
            List<Type> removeDiskParams = new List<Type>();
            removeDiskParams.Add(typeof(string)); // VM Name
            removeDiskParams.Add(typeof(string)); // Controller Type
            removeDiskParams.Add(typeof(int)); // Controller Index
            removeDiskParams.Add(typeof(int)); // Slot Id
            routines.Add(new Routine((Type)null, "removeDisk", removeDiskParams, "Entferne eine VHDX von einer VM. vmName, controllerType, controllerIndex, slotId.")); // vhdpath, vhdsize

            // \todo Add ISO to specific drive
            List<Type> addIsoImageParams = new List<Type>();
            addIsoImageParams.Add(typeof(string)); // VM Name
            addIsoImageParams.Add(typeof(string)); // Image Path
            addIsoImageParams.Add(typeof(int)); // Controller ID
            addIsoImageParams.Add(typeof(int)); // Slot Id
            routines.Add(new Routine((Type)null, "addIsoImage", addIsoImageParams, "Lege ein ISO Image in das DVD Laufwerk einer VM. vmName, isoPath, controllerId, slotId.")); // vhdpath, vhdsize

            routines.Add(new Routine((Type)null, "addDVDDrive", typeof(string), typeof(int), typeof(int), "Erstelle ein leeres DVD Laufwerk. vmName, controllerId, slotId"));
            routines.Add(new Routine((Type)null, "addScsiController", typeof(string), "Erstelle einen neuen SCSI Controller. vmName."));

            // todo Bootreihenfolge
            // bootorder im format "'CD', 'IDE', 'LegacyNetworkAdapter', 'Floppy'"
            routines.Add(new Routine((Type)null, "changeBootOrder", typeof(string), typeof(string), "Änder die Bootreihenfolge einer VM. vmName, bootOrder."));

            exportedRoutines = routines.AsReadOnly();


        }

        // \todo success/error indicator!
        private string ExecutePowershellScript(string script)
        {

            string powerShellScript = script;
            string resultStr;
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
                    resultStr = "PowerShell-Fehler: " + error;
                }

                resultStr = output;
            }

            return resultStr;

        }

        public object Invoke(String strFunctionName, List<object> listParameters)
        {

            if (strFunctionName == "createVM")
            {

                string vmName = (string)listParameters[0];
                string vmPath = (string)listParameters[1];
                string switchName = (string)listParameters[2];
                string memoryBytes = (string)listParameters[3]; // 2GB

                string powerShellScript = $@"
New-VM -Name '{vmName}' -Path '{vmPath}' -MemoryStartupBytes {memoryBytes} -SwitchName '{switchName}'
";

                return ExecutePowershellScript(powerShellScript);

            }

            if (strFunctionName == "createDisk")
            {

                string vhdPath = (string)listParameters[0];
                string vhdSize = (string)listParameters[1];

                string powerShellScript = $@"
New-VHD -Path '{vhdPath}' -SizeBytes {vhdSize} -Dynamic
";

                return ExecutePowershellScript(powerShellScript);

            }

            if (strFunctionName == "assignDisk")
            {

                string vmName = (string)listParameters[0];
                string vhdPath = (string)listParameters[1];
                
                string controllerType = (string)listParameters[2];
                int controllerId = (int)listParameters[3];
                int slotId = (int)listParameters[4];

                string powerShellScript = $@"
Add-VMHardDiskDrive -VMName '{vmName}' -Path '{vhdPath}' -ControllerType '{controllerType}' -ControllerNumber {controllerId} -ControllerLocation {slotId}
";

                return ExecutePowershellScript(powerShellScript);

            }

            if(strFunctionName == "removeVM")
            {

                string vmName = (string)listParameters[0];

                string powerShellScript = $@"
Remove-VM -VMName '{vmName}' -Force
";

                return ExecutePowershellScript(powerShellScript);

            }

            if (strFunctionName == "removeDisk")
            {

                string vmName = (string)listParameters[0];
                string controllerType = (string)listParameters[1];
                int controllerIndex = (int)listParameters[2];
                int diskIndex = (int)listParameters[3];

                string powerShellScript = $@"
Remove-VMHardDiskDrive -VMName '{vmName}' -ControllerType '{controllerType}' -ControllerNumber {controllerIndex} -ControllerLocation {diskIndex}
";

                return ExecutePowershellScript(powerShellScript);

            }

            if (strFunctionName == "addIsoImage")
            {

                string vmName = (string)listParameters[0];
                string isoPath = (string)listParameters[1];
                int controllerId = (int)listParameters[2];
                int slotId = (int)listParameters[3];

                string powerShellScript = $@"
Set-VMDvdDrive -VMName '{vmName}' -Path '{isoPath}' -ControllerNumber {controllerId} -ControllerLocation {slotId}
";

                return ExecutePowershellScript(powerShellScript);

            }

            if(strFunctionName == "addDVDDrive")
            {

                string vmName = (string)listParameters[0];
                int controllerId = (int)listParameters[1];
                int slotId = (int)listParameters[2];

                string powerShellScript = $@"
Add-VMDvdDrive -VMName '{vmName}' -ControllerNumber {controllerId} -ControllerLocation {slotId}
";

                return ExecutePowershellScript(powerShellScript);

            }

            if (strFunctionName == "addScsiController")
            {

                string vmName = (string)listParameters[0];

                string powerShellScript = $@"
Add-VMScsiController -VMName '{vmName}'
";

                return ExecutePowershellScript(powerShellScript);

            }

            if(strFunctionName == "changeBootOrder")
            {

                string vmName = (string)listParameters[0];
                string startupOrder = (string)listParameters[1];

                string[] startupOrderArray = startupOrder.Split(',');
                for (int i = 0; i < startupOrderArray.Length; i++)
                {
                    startupOrderArray[i] = startupOrderArray[i].Trim();
                }

                string powershellScript = $@"
Set-VMBios -VMName '{vmName}' -StartupOrder @({string.Join(", ", startupOrderArray)})
";

                return ExecutePowershellScript(powershellScript);

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
