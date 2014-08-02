using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;

namespace CommandPrompt
{
    /// <summary>
    /// Class used to store string data.
    /// </summary>
    public class DataEventArgs : EventArgs
    {
        public string Data { get; private set; }

        public DataEventArgs(string data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Class that allows running commands and receiving output.
    /// </summary>
    internal class CommandPrompt
    {
        // Process object used to run command.
        private Process _process;

        // Process start info.
        private ProcessStartInfo _startInfo;

        // Stores the contents of standard output.
        private StringBuilder _standardOutput;

        // Stores the contents of standard error.
        private StringBuilder _standardError;

        /// <summary>
        /// No timeout.
        /// </summary>
        public const int NoTimeOut = 0;

        /// <summary>
        /// Value that indicates whether process is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Value that indicates whether process has exited.
        /// </summary>
        public bool HasExited { get; private set; }

        /// <summary>
        /// Process ID of the running command.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        /// Exit code of process. Only set if HasExited is True.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Standard output of command.
        /// </summary>
        public string StandardOutput
        {
            get
            {
                return _standardOutput.ToString();
            }
        }

        /// <summary>
        /// Standard error of cmomand.
        /// </summary>
        public string StandardError
        {
            get
            {
                return _standardError.ToString();
            }
        }

        /// <summary>
        /// Raised when standard output receives data.
        /// </summary>
        public event EventHandler<DataEventArgs> OutputDataReceived = (sender, args) => { };

        /// <summary>
        /// Raised when standard error receievs data.
        /// </summary>
        public event EventHandler<DataEventArgs> ErrorDataReceived = (sender, args) => { };

        /// <summary>
        /// Raised when process has exited.
        /// </summary>
        public event EventHandler Exited = (sender, args) => { };

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="exe">Command to run.</param>
        /// <param name="arguments">Arguments to pass to exe.</param>
        /// <param name="workingDirectory">Working directory to run command in.</param>
        public CommandPrompt(string exe, string arguments = "", string workingDirectory = "")
        {
            _standardOutput = new StringBuilder();
            _standardError = new StringBuilder();

            _startInfo = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,                // This is required to redirect stdin, stdout and stderr
                CreateNoWindow = true,                  // Don't create a window
                RedirectStandardOutput = true,          // Capture standard output
                RedirectStandardError = true,           // Capture standard error
                RedirectStandardInput = true,           // Enable sending commands to standard input
            };

            _process = new Process()
            {
                StartInfo = _startInfo,
                EnableRaisingEvents = true,
            };
            _process.OutputDataReceived += _process_OutputDataReceived;
            _process.ErrorDataReceived += _process_ErrorDataReceived;
            _process.Exited += _process_Exited;
        }

        /// <summary>
        /// Run command synchronously.
        /// </summary>
        /// <param name="timeOutInMilliseconds">Timeout value in milliseconds (default is infinite timeout).</param>
        public void Run(int timeOutInMilliseconds = NoTimeOut)
        {
            if (!IsRunning && !HasExited)
            {
                BeginRun();

                if (timeOutInMilliseconds == NoTimeOut)
                {
                    _process.WaitForExit();
                }
                else
                {
                    _process.WaitForExit(timeOutInMilliseconds);
                }
            }
        }

        /// <summary>
        /// Run command asynchronously.
        /// </summary>
        public void BeginRun()
        {
            if (!IsRunning && !HasExited)
            {
                if (_process.Start())
                {
                    IsRunning = true;
                    ProcessId = _process.Id;

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }
            }
        }

        /// <summary>
        /// Write command to standard input.
        /// </summary>
        /// <param name="command">Command to write.</param>
        public void WriteToStandardInput(string command)
        {
            if (IsRunning && !HasExited)
            {
                _process.StandardInput.Write(command);
            }
        }

        /// <summary>
        /// Kill process.
        /// </summary>
        /// <param name="killChildProcesses">Kill child processes that were spawned from this process.</param>
        public void Kill(bool killChildProcesses = false)
        {
            if (killChildProcesses && ProcessId != 0)
            {
                // Kill this process and all child processes
                KillChildProcesses(ProcessId);
            }
            else if (IsRunning && !HasExited)
            {
                // Only kill this process
                _process.Kill();
            }
        }

        // Recursively kill child and grand-child processes of parent process
        private void KillChildProcesses(int parentPid)
        {
            // Get list of child processes of the parent process
            using (var searcher = new ManagementObjectSearcher("select ProcessId from Win32_Process where ParentProcessId=" + parentPid))
            using (ManagementObjectCollection objCollection = searcher.Get())
            {
                // Kill child processes recursively
                foreach (ManagementObject obj in objCollection)
                {
                    int pid = Convert.ToInt32(obj["ProcessID"]);
                    KillChildProcesses(pid);
                }
            }

            try
            {
                // Kill parent process
                Process.GetProcessById(parentPid).Kill();
            }
            catch (ArgumentException)
            {
                // Ignore; this exception is thrown if the process with the given ID has already exited
            }
        }

        // Handler for OutputDataReceived event of process.
        private void _process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            _standardOutput.AppendLine(e.Data);

            OutputDataReceived(this, new DataEventArgs(e.Data));
        }

        // Handler for ErrorDataReceived event of process.
        private void _process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            _standardError.AppendLine(e.Data);

            ErrorDataReceived(this, new DataEventArgs(e.Data));
        }

        // Handler for Exited event of process.
        private void _process_Exited(object sender, EventArgs e)
        {
            HasExited = true;
            IsRunning = false;
            ExitCode = _process.ExitCode;
            Exited(this, e);
        }
    }
}
