﻿using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Memory
{
    /// <summary>
    /// Memory.dll class. Full documentation at https://github.com/erfg12/memory.dll/wiki
    /// </summary>
    public class Mem
    {
        #region DllImports
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Int32 bInheritHandle,
            Int32 dwProcessId
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("dbghelp.dll")]
        static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            Int32 ProcessId,
            IntPtr hFile,
            MINIDUMP_TYPE DumpType,
            IntPtr ExceptionParam,
            IntPtr UserStreamParam,
            IntPtr CallackParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            string lpBuffer,
            UIntPtr nSize,
            out IntPtr lpNumberOfBytesWritten
        );

        [DllImport("kernel32.dll")]
        static extern int GetProcessId(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern uint GetPrivateProfileString(
           string lpAppName,
           string lpKeyName,
           string lpDefault,
           StringBuilder lpReturnedString,
           uint nSize,
           string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint dwFreeType
            );

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, [Out] byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint flAllocationType,
            uint flProtect
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern UIntPtr GetProcAddress(
            IntPtr hModule,
            string procName
        );

        [DllImport("kernel32.dll", EntryPoint = "CloseHandle")]
        private static extern bool _CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(
        IntPtr hObject
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(
            string lpModuleName
        );

        [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
        internal static extern Int32 WaitForSingleObject(
            IntPtr handle,
            Int32 milliseconds
        );

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32")]
        public static extern IntPtr CreateRemoteThread(
          IntPtr hProcess,
          IntPtr lpThreadAttributes,
          uint dwStackSize,
          UIntPtr lpStartAddress, // raw Pointer into remote process  
          IntPtr lpParameter,
          uint dwCreationFlags,
          out IntPtr lpThreadId
        );

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        // privileges
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;

        // used for memory allocation
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 4;
        #endregion

        /// <summary>
        /// The process handle that was opened. (Use OpenProcess function to populate this variable)
        /// </summary>
        public IntPtr pHandle;

        public Process procs = null;
        public int procID = 0;

        internal enum MINIDUMP_TYPE
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000
        }

        /// <summary>
        /// Open the PC game process with all security and access rights.
        /// </summary>
        /// <param name="id">You can use the getProcIDFromName function to get this.</param>
        /// <returns></returns>
        public bool OpenProcess(int id)
        {
            if (isAdmin() == false)
            {
                Debug.Write("WARNING: You are NOT running this program as admin!! Visit https://github.com/erfg12/memory.dll/wiki/Administrative-Privileges");
                MessageBox.Show("WARNING: You are NOT running this program as admin!!");
            }

            try
            {
                Process.EnterDebugMode();
                if (id != 0)
                { //getProcIDFromName returns 0 if there was a problem
                    procs = Process.GetProcessById(id);
                    procID = id;
                }
                else
                    return false;

                if (procs.Responding == false)
                    return false;

                pHandle = OpenProcess(0x1F0FFF, 1, id);

                if (pHandle == IntPtr.Zero)
                {
                    var eCode = Marshal.GetLastWin32Error();
                }

                mainModule = procs.MainModule;
                getModules();
                return true;
            } catch { return false; }
        }

        /// <summary>
        /// Check if program is running with administrative privileges. Read about it here: https://github.com/erfg12/memory.dll/wiki/Administrative-Privileges
        /// </summary>
        /// <returns></returns>
        public bool isAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Builds the process modules dictionary (names with addresses).
        /// </summary>
        public void getModules()
        {
            if (procs == null)
                return;

            modules.Clear();
            foreach (ProcessModule Module in procs.Modules)
            {
                if (Module.ModuleName != "" && Module.ModuleName != null && !modules.ContainsKey(Module.ModuleName))
                    modules.Add(Module.ModuleName, Module.BaseAddress);
            }
        }

        public void setFocus()
        {
            //int style = GetWindowLong(procs.MainWindowHandle, -16);
            //if ((style & 0x20000000) == 0x20000000) //minimized
            //    SendMessage(procs.Handle, 0x0112, (IntPtr)0xF120, IntPtr.Zero);
            SetForegroundWindow(procs.MainWindowHandle);
        }

        /// <summary>
        /// Get the process ID number by process name.
        /// </summary>
        /// <param name="name">Example: "eqgame". Use task manager to find the name. Do not include .exe</param>
        /// <returns></returns>
        public int getProcIDFromName(string name) //new 1.0.2 function
        {
            Process[] processlist = Process.GetProcesses();

            foreach (Process theprocess in processlist)
            {
                if (theprocess.ProcessName == name) //find (name).exe in the process list (use task manager to find the name)
                    return theprocess.Id;
            }

            return 0; //if we fail to find it
        }

        /// <summary>
        /// Get code from ini file.
        /// </summary>
        /// <param name="name">label for address or code</param>
        /// <param name="file">path and name of ini file</param>
        /// <returns></returns>
        public string LoadCode(string name, string file/*, bool isString*/) //version 1.0.4 added isString
        {
            StringBuilder returnCode = new StringBuilder(1024);
            uint read_ini_result;
            if (file != "")
                read_ini_result = GetPrivateProfileString("codes", name, "", returnCode, (uint)file.Length, file);
            else
                returnCode.Append(name);
            return returnCode.ToString();
        }

        private Int32 LoadIntCode(string name, string path)
        {
            int intValue = Convert.ToInt32(LoadCode(name, path), 16);
            if (intValue >= 0)
                return intValue;
            else
                return 0;
        }

        public Dictionary<string, IntPtr> modules = new Dictionary<string, IntPtr>();

        /// <summary>
        /// Make a named pipe (if not already made) and call to a remote function.
        /// </summary>
        /// <param name="func">remote function to call</param>
        /// <param name="name">name of the thread</param>
        public void ThreadStartClient(string func, string name)
        {
            //ManualResetEvent SyncClientServer = (ManualResetEvent)obj;
            using (NamedPipeClientStream pipeStream = new NamedPipeClientStream(name))
            {
                if (!pipeStream.IsConnected)
                    pipeStream.Connect();

                //MessageBox.Show("[Client] Pipe connection established");
                using (StreamWriter sw = new StreamWriter(pipeStream))
                {
                    if (sw.AutoFlush == false)
                        sw.AutoFlush = true;
                    sw.WriteLine(func);
                }
            }
        }

        private ProcessModule mainModule;

        private UIntPtr LoadUIntPtrCode(string name, string path = "")
        {
            string theCode = LoadCode(name, path);
            UIntPtr uintValue;

            string newOffset = theCode.Substring(theCode.IndexOf('+') + 1);

            if (String.IsNullOrEmpty(newOffset))
                return (UIntPtr)0;

            int intToUint = 0;

            if (Convert.ToInt32(newOffset, 16) > 0)
                intToUint = Convert.ToInt32(newOffset, 16);

            if (theCode.Contains("base") || theCode.Contains("main"))
                uintValue = (UIntPtr)((int)procs.MainModule.BaseAddress + intToUint);
            else if (!theCode.Contains("base") && !theCode.Contains("main") && theCode.Contains("+"))
            {
                string[] moduleName = theCode.Split('+');

                if (modules.Count == 0 || !modules.ContainsKey(moduleName[0]))
                    getModules();

                if (modules.ContainsKey(moduleName[0]))
                {
                    IntPtr altModule = modules[moduleName[0]];
                    uintValue = (UIntPtr)((int)altModule + intToUint);
                }
                else
                {
                    Debug.WriteLine("ERROR! Module " + moduleName[0] + " not found! Visit https://github.com/erfg12/memory.dll/wiki/List-Modules");
                    MessageBox.Show("ERROR! Module " + moduleName[0] + " not found!");
                    return (UIntPtr)0;
                }
            }
            else
                uintValue = (UIntPtr)intToUint;

            return (UIntPtr)uintValue;
        }

        /// <summary>
        /// Cut a string that goes on for too long or one that is possibly merged with another string.
        /// </summary>
        /// <param name="str">The string you want to cut.</param>
        /// <returns></returns>
        public string CutString(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (c >= ' ' && c <= '~')
                    sb.Append(c);
                else
                    break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clean up a string that has bad characters in it.
        /// </summary>
        /// <param name="str">The string you want to sanitize.</param>
        /// <returns></returns>
        public string sanitizeString(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (c >= ' ' && c <= '~')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        #region readMemory
        /// <summary>
        /// Read a float value from an address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public float readFloat(string code, string file = "")
        {
            byte[] memory = new byte[4];

            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memory, (UIntPtr)4, IntPtr.Zero))
            {
                float address = BitConverter.ToSingle(memory, 0);
                float returnValue = (float)Math.Round(address, 2);
                if (returnValue < -99999 || returnValue > 99999)
                    return 0;
                else
                    return returnValue;
            }
            else
                return 0;
        }

        /// <summary>
        /// Read a string value from an address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public string readString(string code, string file = "")
        {
            byte[] memoryNormal = new byte[32];
            UIntPtr theCode;
            theCode = getCode(code, file);
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memoryNormal, (UIntPtr)32, IntPtr.Zero))
                return System.Text.Encoding.UTF8.GetString(memoryNormal);
            else
                return "";
        }

        public int readUIntPtr(UIntPtr code)
        {
            byte[] memory = new byte[4];
            if (ReadProcessMemory(pHandle, code, memory, (UIntPtr)4, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Read an integer from an address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public int readInt(string code, string file = "")
        {
            byte[] memory = new byte[4];
            UIntPtr theCode;

            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memory, (UIntPtr)4, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Read a long value from an address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public long readLong(string code, string file = "")
        {
            byte[] memory = new byte[16];
            UIntPtr theCode;

            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memory, (UIntPtr)16, IntPtr.Zero))
                return BitConverter.ToInt64(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Read a UInt value from address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public uint readUInt(string code, string file = "")
        {
            byte[] memory = new byte[4];
            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memory, (UIntPtr)4, IntPtr.Zero))
                return BitConverter.ToUInt32(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Reads a 2 byte value from an address and moves the address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="moveQty">Quantity to move.</param>
        /// <param name="file">path and name of ini file (OPTIONAL)</param>
        /// <returns></returns>
        public int read2ByteMove(string code, int moveQty, string file = "")
        {
            byte[] memory = new byte[4];
            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            UIntPtr newCode = UIntPtr.Add(theCode, moveQty);

            if (ReadProcessMemory(pHandle, newCode, memory, (UIntPtr)2, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Reads an integer value from address and moves the address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="moveQty">Quantity to move.</param>
        /// <param name="file">path and name of ini file (OPTIONAL)</param>
        /// <returns></returns>
        public int readIntMove(string code, int moveQty, string file = "")
        {
            byte[] memory = new byte[4];
            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            UIntPtr newCode = UIntPtr.Add(theCode, moveQty);

            if (ReadProcessMemory(pHandle, newCode, memory, (UIntPtr)4, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Get UInt and move to another address by moveQty. Use in a for loop.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="moveQty">Quantity to move.</param>
        /// <param name="file">path and name of ini file (OPTIONAL)</param>
        /// <returns></returns>
        public ulong readUIntMove(string code, int moveQty, string file = "")
        {
            byte[] memory = new byte[8];
            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file, 8);

            UIntPtr newCode = UIntPtr.Add(theCode, moveQty);

            if (ReadProcessMemory(pHandle, newCode, memory, (UIntPtr)8, IntPtr.Zero))
                return BitConverter.ToUInt64(memory, 0);
            else
                return 0;
        }

        /// <summary>
        /// Read a 2 byte value from an address. Returns an integer.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and file name to ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public int read2Byte(string code, string file = "")
        {
            byte[] memoryTiny = new byte[4];

            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memoryTiny, (UIntPtr)2, IntPtr.Zero))
                return BitConverter.ToInt32(memoryTiny, 0);
            else
                return 0;
        }

        /// <summary>
        /// Read 1 byte from address.
        /// </summary>
        /// <param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        /// <param name="file">path and file name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public int readByte(string code, string file = "")
        {
            byte[] memoryTiny = new byte[4];

            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (ReadProcessMemory(pHandle, theCode, memoryTiny, (UIntPtr)1, IntPtr.Zero))
                return BitConverter.ToInt32(memoryTiny, 0);
            else
                return 0;
        }

        public int readPByte(UIntPtr address, string code, string file = "")
        {
            byte[] memory = new byte[4];
            if (ReadProcessMemory(pHandle, address + LoadIntCode(code, file), memory, (UIntPtr)1, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        public float readPFloat(UIntPtr address, string code, string file = "")
        {
            byte[] memory = new byte[4];
            if (ReadProcessMemory(pHandle, address + LoadIntCode(code, file), memory, (UIntPtr)4, IntPtr.Zero))
            {
                float spawn = BitConverter.ToSingle(memory, 0);
                return (float)Math.Round(spawn, 2);
            }
            else
                return 0;
        }

        public int readPInt(UIntPtr address, string code, string file = "")
        {
            byte[] memory = new byte[4];
            if (ReadProcessMemory(pHandle, address + LoadIntCode(code, file), memory, (UIntPtr)4, IntPtr.Zero))
                return BitConverter.ToInt32(memory, 0);
            else
                return 0;
        }

        public string readPString(UIntPtr address, string code, string file = "")
        {
            byte[] memoryNormal = new byte[32];
            if (ReadProcessMemory(pHandle, address + LoadIntCode(code, file), memoryNormal, (UIntPtr)32, IntPtr.Zero))
                return CutString(System.Text.Encoding.ASCII.GetString(memoryNormal));
            else
                return "";
        }
        #endregion

        #region writeMemory
        ///<summary>
        ///Write to memory address. See https://github.com/erfg12/memory.dll/wiki/writeMemory() for more information.
        ///</summary>
        ///<param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        ///<param name="type">byte, bytes, float, int, string or long.</param>
        ///<param name="write">value to write to address.</param>
        ///<param name="file">path and name of .ini file (OPTIONAL)</param>
        public bool writeMemory(string code, string type, string write, string file = "")
        {
            byte[] memory = new byte[4];
            int size = 4;

            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (type == "float")
            {
                memory = BitConverter.GetBytes(Convert.ToSingle(write));
                size = 4;
            }
            else if (type == "int")
            {
                memory = BitConverter.GetBytes(Convert.ToInt32(write));
                size = 4;
            }
            else if (type == "byte")
            {
                memory = new byte[1];
                memory = BitConverter.GetBytes(Convert.ToInt32(write));
                size = 1;
            }
            else if (type == "bytes")
            {
                string[] script_instruction_value_split = write.Split(' ');
                int num_bytes = script_instruction_value_split.Length;
                byte[] write_bytes = new byte[num_bytes];
                long script_instruction_value_int = 0;
                byte[] script_instruction_value_bytes;
                for (int i = 0; i < num_bytes; i++)
                {
                    script_instruction_value_int = Int32.Parse(script_instruction_value_split[i], NumberStyles.AllowHexSpecifier);
                    script_instruction_value_bytes = BitConverter.GetBytes(script_instruction_value_int);
                    write_bytes[i] = script_instruction_value_bytes[0];
                }
                writeBytes(theCode.ToString(), write_bytes);
                return true;
            }
            else if (type == "long")
            {
                memory = BitConverter.GetBytes(Convert.ToInt64(write));
                size = 16;
            }
            else if (type == "string")
            {
                memory = new byte[write.Length];
                memory = System.Text.Encoding.UTF8.GetBytes(write);
                size = write.Length;
            }

            if (WriteProcessMemory(pHandle, theCode, memory, (UIntPtr)size, IntPtr.Zero))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Write to address and move by moveQty. Good for byte arrays. See https://github.com/erfg12/memory.dll/wiki/Writing-a-Byte-Array for more information.
        /// </summary>
        ///<param name="code">address, module + pointer + offset, module + offset OR label in .ini file.</param>
        ///<param name="type">byte, bytes, float, int, string or long.</param>
        /// <param name="write">byte to write</param>
        /// <param name="moveQty">quantity to move</param>
        /// <param name="file">path and name of .ini file (OPTIONAL)</param>
        /// <returns></returns>
        public bool writeMove(string code, string type, string write, int moveQty, string file = "") //version v1.0.3
        {
            byte[] memory = new byte[4];
            int size = 4;

            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);

            if (type == "float")
            {
                memory = BitConverter.GetBytes(Convert.ToSingle(write));
                size = 4;
            }
            else if (type == "int")
            {
                memory = BitConverter.GetBytes(Convert.ToInt32(write));
                size = 4;
            }
            else if (type == "byte")
            {
                memory = new byte[1];
                memory = BitConverter.GetBytes(Convert.ToInt32(write));
                size = 1;
            }
            else if (type == "string")
            {
                memory = new byte[write.Length];
                memory = System.Text.Encoding.UTF8.GetBytes(write);
                size = write.Length;
            }

            UIntPtr newCode = UIntPtr.Add(theCode, moveQty);

            if (WriteProcessMemory(pHandle, newCode, memory, (UIntPtr)size, IntPtr.Zero))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Write byte array to addresses.
        /// </summary>
        /// <param name="code">address to write to</param>
        /// <param name="write">byte array to write</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        public void writeBytes(string code, byte[] write, string file = "")
        {
            UIntPtr theCode;
            if (!LoadCode(code, file).Contains(","))
                theCode = LoadUIntPtrCode(code, file);
            else
                theCode = getCode(code, file);
            WriteProcessMemory(pHandle, theCode, write, (UIntPtr)write.Length, IntPtr.Zero);
        }
        #endregion

        /// <summary>
        /// Get code from ini file.
        /// </summary>
        /// <param name="name">label in ini file</param>
        /// <param name="path">path to ini file</param>
        /// <param name="size">size of address (default is 4)</param>
        /// <returns></returns>
        private UIntPtr getCode(string name, string path, int size = 4)
        {
            string theCode = LoadCode(name, path);
            if (theCode == "")
                return UIntPtr.Zero;
            string newOffsets = theCode;
            if (theCode.Contains("+"))
                newOffsets = theCode.Substring(theCode.IndexOf('+') + 1);

            byte[] memoryAddress = new byte[size];

            if (newOffsets.Contains(','))
            {
                List<int> offsetsList = new List<int>();

                string[] newerOffsets = newOffsets.Split(',');
                foreach (string oldOffsets in newerOffsets)
                {
                    offsetsList.Add(Convert.ToInt32(oldOffsets, 16));
                }
                int[] offsets = offsetsList.ToArray();

                if (theCode.Contains("base") || theCode.Contains("main"))
                    ReadProcessMemory(pHandle, (UIntPtr)((int)mainModule.BaseAddress + offsets[0]), memoryAddress, (UIntPtr)size, IntPtr.Zero);
                else if (!theCode.Contains("base") && !theCode.Contains("main") && theCode.Contains("+"))
                {
                    string[] moduleName = theCode.Split('+');
                    IntPtr altModule = modules[moduleName[0]];
                    ReadProcessMemory(pHandle, (UIntPtr)((int)altModule + offsets[0]), memoryAddress, (UIntPtr)size, IntPtr.Zero);
                }
                else
                    ReadProcessMemory(pHandle, (UIntPtr)(offsets[0]), memoryAddress, (UIntPtr)size, IntPtr.Zero);

                uint num1 = BitConverter.ToUInt32(memoryAddress, 0);

                UIntPtr base1 = (UIntPtr)0;

                for (int i = 1; i < offsets.Length; i++)
                {
                    base1 = new UIntPtr(num1 + Convert.ToUInt32(offsets[i]));
                    ReadProcessMemory(pHandle, base1, memoryAddress, (UIntPtr)size, IntPtr.Zero);
                    num1 = BitConverter.ToUInt32(memoryAddress, 0);
                }
                return base1;
            }
            else
            {
                int trueCode = Convert.ToInt32(newOffsets, 16);

                if (theCode.Contains("base") || theCode.Contains("main"))
                    ReadProcessMemory(pHandle, (UIntPtr)((int)mainModule.BaseAddress + trueCode), memoryAddress, (UIntPtr)size, IntPtr.Zero);
                else if (!theCode.Contains("base") && !theCode.Contains("main") && theCode.Contains("+"))
                {
                    string[] moduleName = theCode.Split('+');
                    IntPtr altModule = modules[moduleName[0]];
                    ReadProcessMemory(pHandle, (UIntPtr)((int)altModule + trueCode), memoryAddress, (UIntPtr)size, IntPtr.Zero);
                }
                else
                    ReadProcessMemory(pHandle, (UIntPtr)(trueCode), memoryAddress, (UIntPtr)size, IntPtr.Zero);

                uint num1 = BitConverter.ToUInt32(memoryAddress, 0);
                
                UIntPtr base1 = new UIntPtr(num1);
                num1 = BitConverter.ToUInt32(memoryAddress, 0);
                return base1;
            }
        }

        /// <summary>
        /// Close the process when finished.
        /// </summary>
        public void closeProcess()
        {
            CloseHandle(pHandle);
        }

        /// <summary>
        /// Inject a DLL file.
        /// </summary>
        /// <param name="strDLLName">path and name of DLL file.</param>
        public void InjectDLL(String strDLLName)
        {
            IntPtr bytesout;

            foreach (ProcessModule pm in procs.Modules)
            {
                if (pm.ModuleName.StartsWith("inject", StringComparison.InvariantCultureIgnoreCase))
                    return;
            }

            if (procs.Responding == false)
                return;

            Int32 LenWrite = strDLLName.Length + 1;
            IntPtr AllocMem = (IntPtr)VirtualAllocEx(pHandle, (IntPtr)null, (uint)LenWrite, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

            WriteProcessMemory(pHandle, AllocMem, strDLLName, (UIntPtr)LenWrite, out bytesout);
            UIntPtr Injector = (UIntPtr)GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (Injector == null)
                return;

            IntPtr hThread = (IntPtr)CreateRemoteThread(pHandle, (IntPtr)null, 0, Injector, AllocMem, 0, out bytesout);
            if (hThread == null)
                return;

            int Result = WaitForSingleObject(hThread, 10 * 1000);
            if (Result == 0x00000080L || Result == 0x00000102L)
            {
                if (hThread != null)
                    CloseHandle(hThread);
                return;
            }
            VirtualFreeEx(pHandle, AllocMem, (UIntPtr)0, 0x8000);

            if (hThread != null)
                CloseHandle(hThread);

            return;
        }

        byte[] dumpBytes;

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        private static void SuspendProcess(int pid)
        {
            var process = Process.GetProcessById(pid);

            if (process.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
                if (pOpenThread == IntPtr.Zero)
                    continue;

                SuspendThread(pOpenThread);
                CloseHandle(pOpenThread);
            }
        }

        public static void ResumeProcess(int pid)
        {
            var process = Process.GetProcessById(pid);
            if (process.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);
                if (pOpenThread == IntPtr.Zero)
                    continue;

                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);
                CloseHandle(pOpenThread);
            }
        }

        /// <summary>
        /// Array of Bytes scan to find address. Returns IntPtr address. See https://github.com/erfg12/memory.dll/wiki/sigScan-(AoB-Scanning) for more information.
        /// </summary>
        /// <param name="search">array of bytes to look for. Can be full masks or partial masks. This can also be a ini file label.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public IntPtr AoBScan(string search, string file = "")
        {
            string[] stringByteArray = LoadCode(search, file).Split(' ');
            byte[] myPattern = new byte[stringByteArray.Length];
            string mask = "";
            int i = 0;
            foreach (string ba in stringByteArray)
            {
                if (ba == "??")
                {
                    myPattern[i] = 0xFF;
                    mask += "?";
                }
                else if (Char.IsLetterOrDigit(ba[0]) && ba[1] == '?') //partial match
                {
                    myPattern[i] = Encoding.ASCII.GetBytes("0x" + ba[0] + "F")[0];
                    mask += "?"; //show it's still a wildcard of some kind
                }
                else if (Char.IsLetterOrDigit(ba[1]) && ba[0] == '?') //partial match
                {
                    myPattern[i] = Encoding.ASCII.GetBytes("0xF" + ba[1])[0];
                    mask += "?"; //show it's still a wildcard of some kind
                }
                else
                {
                    myPattern[i] = Byte.Parse(ba, NumberStyles.HexNumber);
                    mask += "x";
                }
                i++;
            }

            //DumpMemory(0);
            byte[] test = new byte[procs.VirtualMemorySize64];
            SuspendProcess(procID);
            UIntPtr procMod = (UIntPtr)((int)procs.MainModule.BaseAddress);
            ReadProcessMemory(pHandle, procMod, test, (UIntPtr)procs.PrivateMemorySize64, IntPtr.Zero);
            //Debug.Write("[DEBUG] ReadProcessMemory Length=" + test.Length.ToString() + " Dump Length=" + dumpBytes.Length + " (2)");
            //File.WriteAllBytes("test.txt", test);
            return (IntPtr)FindPattern(test, myPattern, mask);
        }

        async Task PutTaskDelay()
        {
            await Task.Delay(5000);
        }

        /// <summary>
        /// Quickly scans only a section of memory, instead of the entire thing like AoBScan does.
        /// </summary>
        /// <param name="start">starting address</param>
        /// <param name="length">length to scan</param>
        /// <param name="search">array of bytes to look for. Can include partial masks. This can also be a ini file label.</param>
        /// <param name="file">path and name of ini file. (OPTIONAL)</param>
        /// <returns></returns>
        public IntPtr AoBScanFast(int start, int length, string search, string file = "")
        {
            string[] stringByteArray = LoadCode(search, file).Split(' ');
            byte[] myPattern = new byte[stringByteArray.Length];
            string mask = "";
            int i = 0;
            foreach (string ba in stringByteArray)
            {
                if (ba == "??")
                {
                    myPattern[i] = 0xFF;
                    mask += "?";
                }
                else if (Char.IsLetterOrDigit(ba[0]) && ba[1] == '?') //partial match
                {
                    myPattern[i] = Encoding.ASCII.GetBytes("0x" + ba[0] + "F")[0];
                    mask += "?"; //show it's still a wildcard of some kind
                }
                else if (Char.IsLetterOrDigit(ba[1]) && ba[0] == '?') //partial match
                {
                    myPattern[i] = Encoding.ASCII.GetBytes("0xF" + ba[1])[0];
                    mask += "?"; //show it's still a wildcard of some kind
                }
                else
                {
                    myPattern[i] = Byte.Parse(ba, NumberStyles.HexNumber);
                    mask += "x";
                }
                i++;
            }

            byte[] test = new byte[procs.VirtualMemorySize64];
            SuspendProcess(procID);
            UIntPtr procMod = (UIntPtr)((int)procs.MainModule.BaseAddress);
            Debug.Write("[DEBUG] Main module address at " + procMod.ToString() + Environment.NewLine);
            Debug.Write("[DEBUG] VirtualMemorySize64 = " + procs.VirtualMemorySize64 + Environment.NewLine);
            ReadProcessMemory(pHandle, procMod, test, (UIntPtr)test.Length, IntPtr.Zero);
            //File.WriteAllBytes("test.dmp", test);
            return (IntPtr)FindPattern(test, myPattern, mask, start, length);
        }

        private bool MaskCheck(int nOffset, byte[] btPattern, string strMask, byte[] dumpRegion)
        {
            // Loop the pattern and compare to the mask and dump.
            for (int x = 0; x < btPattern.Length; x++)
            {
                // If the mask char is a wildcard.
                if (strMask[x] == '?')
                {
                    if (btPattern[x] == 0xFF) //100% wildcard
                        continue;
                    else
                    { //50% wildcard
                        if (dumpRegion[nOffset + x].ToString("X").Length == 2) //byte must be 2 characters long
                        {
                            if (btPattern[x].ToString("X")[0] == '?') { //ex: ?5
                                if (dumpRegion[nOffset + x].ToString("X")[1] != btPattern[x].ToString("X")[1])
                                    return false;
                            }
                            else if (btPattern[x].ToString("X")[1] == '?') //ex: 5?
                            {
                                if (dumpRegion[nOffset + x].ToString("X")[0] != btPattern[x].ToString("X")[0])
                                    return false;
                            }
                        }
                    }
                }
                //Debug.Write("[DEBUG] AoB scan comparing " + String.Format("0x{0:x2}", Convert.ToUInt32(btPattern[x].ToString())) + " and " + String.Format("0x{0:x2}", Convert.ToUInt32(dumpRegion[nOffset + x].ToString())) + " at " + String.Format("0x{0:x8}", Convert.ToUInt32((nOffset + x).ToString())) + " nOffset:" + nOffset.ToString() + " x:" + x.ToString() + Environment.NewLine);

                if ((strMask[x] == 'x') && (btPattern[x] != dumpRegion[nOffset + x]))
                    return false;
            }

            // The loop was successful so we found 1 pattern match.
            return true;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                if (b == 255)
                    hex.Append("?? ");
                else
                    hex.AppendFormat("{0:x2} ", b);
            }
            return hex.ToString();
        }

        /// <summary>
        /// Convert a byte array to hex values in a string.
        /// </summary>
        /// <param name="ba">your byte array to convert</param>
        /// <returns></returns>
        public static string ByteArrayToHexString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            int i = 1;
            foreach (byte b in ba)
            {
                if (i == 16)
                {
                    hex.AppendFormat("{0:x2}{1}", b, Environment.NewLine);
                    i = 0;
                }
                else
                    hex.AppendFormat("{0:x2} ", b);
                i++;
            }
            return hex.ToString();
        }

        public int FindPattern(byte[] haystack, byte[] needle, string strMask, int start = 0, int length = 0)
        {
            if (length == 0)
                length = (haystack.Length - start);

            Debug.Write("[DEBUG] starting AoB scan at " + start + " and going " + length + " length." + Environment.NewLine);
            Debug.Write("[DEBUG] AoB mask is " + strMask + Environment.NewLine);
            Debug.Write("[DEBUG] AoB pattern is " + ByteArrayToString(needle) + Environment.NewLine);
            Debug.Write("[DEBUG] memory dump (" + haystack.Length + ")");
            for (int x = start; x < (start + length); x++)
            {
                if (MaskCheck(x, needle, strMask, haystack))
                {
                    //string total = (x + diff).ToString("x8");
                    //Debug.Write("[DEBUG] base address is " + procs.MainModule.BaseAddress.ToString("x8") + " and resulting offset is " + x.ToString("x8") + " min address is " + getMinAddress().ToString("x8") + Environment.NewLine);
                    ResumeProcess(procID);
                    return (x + (int)mainModule.BaseAddress);
                }
            }
            ResumeProcess(procID);
            return 0;
        }

        public struct SYSTEM_INFO
{
    public ushort processorArchitecture;
    ushort reserved;
    public uint pageSize;
    public IntPtr minimumApplicationAddress;  // minimum address
    public IntPtr maximumApplicationAddress;  // maximum address
    public IntPtr activeProcessorMask;
    public uint numberOfProcessors;
    public uint processorType;
    public uint allocationGranularity;
    public ushort processorLevel;
    public ushort processorRevision;
}

        public struct MEMORY_BASIC_INFORMATION
        {
            public ulong BaseAddress;
            public ulong AllocationBase;
            public int AllocationProtect;
            public ulong RegionSize;
            public int State;
            public ulong Protect;
            public ulong Type;
        }

        public ulong getMinAddress()
        {
            SYSTEM_INFO SI;
            GetSystemInfo(out SI);
            return (ulong)SI.minimumApplicationAddress;
        }

        int diff = 0;

        public static void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void DumpMemory2()
        {
            SYSTEM_INFO sys_info = new SYSTEM_INFO();
            GetSystemInfo(out sys_info);

            IntPtr proc_min_address = sys_info.minimumApplicationAddress;
            IntPtr proc_max_address = sys_info.maximumApplicationAddress;

            // saving the values as long ints so I won't have to do a lot of casts later
            long proc_min_address_l = (long)procs.MainModule.BaseAddress;
            long proc_max_address_l = (long)procs.VirtualMemorySize64;

            // notepad better be runnin'
            //Process process = Process.GetProcessesByName("notepad")[0];

            // opening the process with desired access level
            //IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | 0x0010, false, process.Id);

            StreamWriter sw = new StreamWriter("dump.dmp");

            // this will store any information we get from VirtualQueryEx()
            MEMORY_BASIC_INFORMATION mem_basic_info = new MEMORY_BASIC_INFORMATION();
            while (proc_min_address_l < proc_max_address_l)
            {
                // 28 = sizeof(MEMORY_BASIC_INFORMATION)
                VirtualQueryEx(pHandle, procs.MainModule.BaseAddress, out mem_basic_info, 28);

                // if this memory chunk is accessible
                //if (mem_basic_info.Protect == PAGE_READWRITE && mem_basic_info.State == MEM_COMMIT)
                //{
                byte[] buffer = new byte[mem_basic_info.RegionSize];
                UIntPtr test = (UIntPtr)mem_basic_info.RegionSize;
                UIntPtr test2 = (UIntPtr)((int)procs.MainModule.BaseAddress);
                    // read everything in the buffer above
                    ReadProcessMemory(pHandle, test2, buffer, test, IntPtr.Zero);
                    AppendAllBytes(@"test.txt", buffer);
                    // then output this in the file
                    /*for (int i = 0; i < (int)mem_basic_info.RegionSize; i++)
                    {
                        sw.WriteLine("{0}", ((char)buffer[i]);
                        //Debug.Write(((int)mem_basic_info.BaseAddress + i).ToString("X") + buffer[i].ToString());
                    }*/
                //}

                // move to the next memory chunk
                proc_min_address_l += (int)mem_basic_info.RegionSize;
                proc_min_address = new IntPtr(proc_min_address_l);
            }
            sw.Close();
        }

        /// <summary>
        /// dumps process memory to file
        /// </summary>
        /// <param name="type">0 = dump to variable dumpBytes, 1 = raw dump file, 2 = hex in text file, 3 = both raw dump and hex in text</param>
        public void DumpMemory(int type = 0)
        {
            int procID = GetProcessId(pHandle);
            var str = "";
            string rawFile = Process.GetProcessById(procID).ProcessName + ".DMP";
            string txtFile = Process.GetProcessById(procID).ProcessName + ".TXT";
            //Debug.Write("[DEBUG] Now dumping process ID:" + procID.ToString() + " (" + Process.GetProcessById(procID).ProcessName + ")" + Environment.NewLine);

            FileStream fsToDump = null;
            if (File.Exists(rawFile))
                fsToDump = File.Open(rawFile, FileMode.Append);
            else
                fsToDump = File.Create(rawFile);

            //Debug.Write("[DEBUG] writing raw values to dump file.." + Environment.NewLine);
            Process thisProcess = Process.GetCurrentProcess();
            MiniDumpWriteDump(pHandle, procID, fsToDump.SafeFileHandle.DangerousGetHandle(),
                /*MINIDUMP_TYPE.MiniDumpNormal |*/ MINIDUMP_TYPE.MiniDumpWithDataSegs | MINIDUMP_TYPE.MiniDumpWithFullMemory | MINIDUMP_TYPE.MiniDumpWithHandleData | MINIDUMP_TYPE.MiniDumpFilterMemory
            | MINIDUMP_TYPE.MiniDumpScanMemory | MINIDUMP_TYPE.MiniDumpWithUnloadedModules | MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory | MINIDUMP_TYPE.MiniDumpFilterModulePaths
            | MINIDUMP_TYPE.MiniDumpWithProcessThreadData | MINIDUMP_TYPE.MiniDumpWithPrivateReadWriteMemory /*| MINIDUMP_TYPE.MiniDumpWithoutOptionalData*/ | MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo
            | MINIDUMP_TYPE.MiniDumpWithThreadInfo | MINIDUMP_TYPE.MiniDumpWithCodeSegs, 
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            fsToDump.Close();

            if (type == 0)
            {
                dumpBytes = File.ReadAllBytes(rawFile);
                diff = Convert.ToInt32(procs.WorkingSet) - dumpBytes.Length;
                //MessageBox.Show(diff.ToString() + " 0x" + diff.ToString("x8"));
            }

            if (type == 1)
            {
                //Debug.Write("[DEBUG] converting raw values to hex string values, this can take a while..." + Environment.NewLine);
                byte[] bytes = File.ReadAllBytes(rawFile);
                str = ByteArrayToHexString(bytes);
            }

            if (type < 2)
                File.Delete(rawFile);

            if (type == 1)
            {
                //Debug.Write("[DEBUG] deleting raw dump file and writing hex string values to text file..." + Environment.NewLine);
                File.WriteAllText(txtFile, str);
            }
        }
    }
}
