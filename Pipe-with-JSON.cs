public class ProcessPipe
{
    private string Id { get; set; }
    private Process MainProcess = new Process();
    private NamedPipeServerStream MainPipe { get; set; }
    public ObservableCollection<string> Output { get; set; } = new ObservableCollection<string>();
    public ObservableCollection<string> Error { get; set; } = new ObservableCollection<string>();
    public bool RestartIfExited { get; set; } = true;
    public ProcessPipe(string filename, string nodeAppDirectory)
    {
        Id = Guid.NewGuid().ToString();
        var dir = nodeAppDirectory;
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


    public JSONObject SendMessage(object value)
    {
        if (MainProcess.HasExited)
            if (RestartIfExited) Start(); else throw new Exception("The Process has exited");
        var msg = JSON.ToJSONString(value);
        var line = "";
        var pipeID = Id + "-" + Guid.NewGuid().ToString();
        using (var tempPipe = new NamedPipeServerStream(pipeID, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
        {
            new StreamWriter(MainPipe) { AutoFlush = true }.Write(pipeID.ToCharArray());// Tell the main nodejs Pipe the Id of our new message pipe//
            tempPipe.WaitForConnection(); // wait for the nodejs message pipe to connect//
            new StreamWriter(tempPipe) { AutoFlush = true }.Write(msg.ToCharArray()); // send the message to the temporary message pipe//
            var sr = new StreamReader(tempPipe);
            while (sr.Peek() == -1) { } // wait for the reply //
            while (sr.Peek() != -1) line += (char)sr.Read();
        }// pipe closes here//
        return JSON.Parse(line);
    }

    public T SendMessage<T>(T value)
    {
        try { return JSONConverter.ConvertFromJSONType<T>(SendMessage(value)); }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return default;
        }
    }
    public IAsyncResult BeginSendMessage(object value, AsyncCallback asyncCallback)
    {
        var function = ((Func<object, JSONObject>)SendMessage);
        return function.BeginInvoke(value, asyncCallback, function);
    }
    public IAsyncResult BeginSendMessage<T>(T value, AsyncCallback asyncCallback)
    {
        var function = ((Func<T, T>)SendMessage);
        return function.BeginInvoke(value,asyncCallback,function); 
    }
    public JSONObject EndSendMessage(IAsyncResult result)
    {
        while (!result.IsCompleted) Thread.Sleep(100);
        return ((Func<object, JSONObject>)result.AsyncState).EndInvoke(result);
    }
    public T EndSendMessage<T>(IAsyncResult result)
    {
        while (!result.IsCompleted)Thread.Sleep(100);
        var function = (Func<T, T>)result.AsyncState;
        return function.EndInvoke(result);
    }

    public Task<JSONObject> SendMessageRawAsync(object value)
    {
        return Task<JSONObject>.Factory.StartNew(new Func<JSONObject>(() => SendMessage(value)));
    }

    public Task<T> SendMessageAsync<T>(T value)
    {
        return Task<T>.Factory.StartNew(new Func<T>(() => SendMessage(value)));
    }
}
