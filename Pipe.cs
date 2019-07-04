public class ProcessPipe
{
    private string Id { get; set; }
    private Process MainProcess = new Process();
    private NamedPipeServerStream MainPipe { get; set; }
    public ObservableCollection<string> Output { get; set; } = new ObservableCollection<string>();
    public ObservableCollection<string> Error { get; set; } = new ObservableCollection<string>();
    public bool RestartIfExited { get; set; } = true;
    public ProcessPipe(string filename)
    {
        Id = Guid.NewGuid().ToString();
        var dir = Regex.Split(Directory.GetCurrentDirectory(), @"(?=.+)(\\ThroughPutSolution).+").Aggregate("", (a, c) => a + c) + @"\ThroughPutSolution\";
        var file = Directory.GetFiles(dir, "electron.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (file != null)
            MainProcess.StartInfo = new ProcessStartInfo(file)
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                Arguments = new string[] { dir + filename, Id }.Aggregate("", (a, c) => a += " " + c)
            };
        else
            throw new Exception("Electron was not found, make sure the npm has been installed");
    }
    public void Start()
    {
        MainPipe = new NamedPipeServerStream(Id, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
        Task.Factory.StartNew(() => {
            MainProcess.Start();
            while (!MainProcess.HasExited)
            {
                var str = MainProcess.StandardOutput.ReadLine();
                App.Current.Dispatcher.Invoke(() => Output.Add(str));
            }
            MainPipe.Close();
        });
        MainPipe.WaitForConnection();
    }

    public string SendMessage(string value)
    {
        if (MainProcess.HasExited)
            if (RestartIfExited) Start(); else throw new Exception("The Process has exited");
        var line = "";
        var pipeID = Id + "-" + Guid.NewGuid().ToString();
        using (var tempPipe = new NamedPipeServerStream(pipeID, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
        {
            new StreamWriter(MainPipe) { AutoFlush = true }.Write(pipeID.ToCharArray());// Tell the main nodejs Pipe the Id of our new message pipe//
            tempPipe.WaitForConnection(); // wait for the nodejs message pipe to connect//
            new StreamWriter(tempPipe) { AutoFlush = true }.Write(value.ToCharArray()); // send the message to the temporary message pipe//
            var sr = new StreamReader(tempPipe);
            while (sr.Peek() == -1) { } // wait for the reply //
            while (sr.Peek() != -1) line += (char)sr.Read();
        }// pipe closes here//
        return line;
    }

    public Task<string> SendMessageAsync<string>(string value)
    {
        return Task<string>.Factory.StartNew(new Func<string>(() => SendMessage(value)));
    }
}
