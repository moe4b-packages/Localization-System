using System;
using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Newtonsoft.Json;

using System.Threading.Tasks;
using System.IO.Pipes;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Text;
using System.Threading;

namespace MB.LocalizationSystem
{
    partial class Narrative
    {
#if UNITY_EDITOR
        public static class Parser
        {
            public const string PipeName = "MB Localization Parser";

            static bool IsRunning = false;
            public static async Task<Structure> Process()
            {
                if (IsRunning) throw new InvalidOperationException("Narrative Parsing Already in Progress");
                IsRunning = true;

                var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                Debug.Log("Pipe Server Started");

                try
                {
                    using var parser = Executable.Start();

                    var cancellation = new CancellationTokenSource();

                    parser.Exited += InvokeCancellation;
                    void InvokeCancellation(object sender, EventArgs args)
                    {
                        Debug.LogError($"Narrative System Parsing Cancelled, Process Halted Early");
                        cancellation.Cancel();
                    }

                    await pipe.WaitForConnectionAsync(cancellationToken: cancellation.Token);
                    Debug.Log("Pipe Server Connected");

                    var marker = new byte[sizeof(int)];
                    await pipe.ReadAsync(marker, cancellationToken: cancellation.Token);

                    var length = BitConverter.ToInt32(marker);

                    var raw = new byte[length];
                    await pipe.ReadAsync(raw, cancellationToken: cancellation.Token);
                    Debug.Log("Pipe Server Recieved Data");

                    parser.Exited -= InvokeCancellation;

                    await pipe.WriteAsync(new byte[1]);

                    var text = Encoding.UTF8.GetString(raw);

                    var structure = Structure.Parse(text);
                    return structure;
                }
                finally
                {
                    pipe.Close();
                    Debug.Log("Pipe Server Closed");
                    IsRunning = false;
                }
            }

            static string GetSolutionPath()
            {
                var file = System.IO.Path.GetFullPath($"{Application.productName}.sln");

                return file;
            }

            public static class Executable
            {
                public const string RelativePath = "External/Localization Parser/MB.Localization-System.Parser.exe";

                public static Process Start()
                {
                    var target = System.IO.Path.GetFullPath(RelativePath);

                    var solution = GetSolutionPath();
                    var arguments = MUtility.FormatProcessArguments(solution, PipeName);

                    var info = new ProcessStartInfo(target, arguments);
                    info.CreateNoWindow = true;
                    info.UseShellExecute = false;
                    var process = System.Diagnostics.Process.Start(info);

                    process.EnableRaisingEvents = true;

                    return process;
                }
            }

            [JsonObject]
            public class Structure
            {
                [JsonProperty]
                public HashSet<string> Text { get; set; }

                public Structure()
                {

                }

                public static Structure Parse(string json)
                {
                    if (string.IsNullOrEmpty(json))
                        return null;

                    var structure = JsonConvert.DeserializeObject<Structure>(json);
                    return structure;
                }
            }

            public class Extractor : Localization.Extraction.Processor
            {
                public override async Task Modify(Localization.Extraction.Content content)
                {
                    var strucutre = await Process();

                    content.Text.UnionWith(strucutre.Text);
                }
            }
        }
#endif
    }
}