using System;
using System.Windows.Forms;

namespace CommandPrompt
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            textBoxOutput.Clear();

            try
            {
                CommandPrompt commandPrompt = new CommandPrompt(textBoxCommand.Text, textBoxParameters.Text);

                if (checkBoxAsync.Checked)
                {
                    // Run command asynchronously
                    commandPrompt.Exited += commandPrompt_Exited;
                    commandPrompt.OutputDataReceived += commandPrompt_DataReceived;
                    commandPrompt.ErrorDataReceived += commandPrompt_DataReceived;
                    commandPrompt.BeginRun();
                    labelStatus.Text = "Command is running...";
                }
                else
                {
                    // Get timeout value if specified
                    int timeout = CommandPrompt.NoTimeOut;
                    if (!String.IsNullOrEmpty(textBoxTimeout.Text))
                    {
                        Int32.TryParse(textBoxTimeout.Text, out timeout);
                    }

                    // Run command synchronously
                    commandPrompt.Run(timeout);

                    textBoxOutput.Text = commandPrompt.StandardOutput;
                }
            }
            catch (Exception exception)
            {
                textBoxOutput.Text = exception.ToString();
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        void commandPrompt_DataReceived(object sender, DataEventArgs e)
        {
            // Invoke on UI thread since this method will run in a separate thread
            textBoxOutput.Invoke((Action)(() => textBoxOutput.AppendText(e.Data + Environment.NewLine)));
        }

        void commandPrompt_Exited(object sender, EventArgs e)
        {
            // Invoke on UI thread since this method will run in a separate thread
            labelStatus.Invoke((Action)(() => labelStatus.Text = "Command has finished executing."));
        }
    }
}
