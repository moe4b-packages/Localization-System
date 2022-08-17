using System;
using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
#endif

using Newtonsoft.Json;

using System.Threading.Tasks;
using System.IO.Pipes;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Text;
using System.Threading;

using UnityEngine.SceneManagement;
using System.Linq;
using System.Reflection;
using UnityEngine.Windows;

namespace MB.LocalizationSystem
{
    partial class Localization
    {
#if UNITY_EDITOR
        public static class Parser
        {
            public static class Roslyn
            {
                public const string PipeName = "MB Localization Parser";

                static bool IsRunning = false;
                public static async Task<Structure> Process()
                {
                    if (IsRunning) throw new InvalidOperationException("Narrative Parsing Already in Progress");
                    IsRunning = true;

                    var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    Debug.Log("Localization Parser Pipe Server Started");

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
                        Debug.Log("Localization Parser Pipe Server Connected");

                        var marker = new byte[sizeof(int)];
                        await pipe.ReadAsync(marker, cancellationToken: cancellation.Token);

                        var length = BitConverter.ToInt32(marker);

                        var raw = new byte[length];
                        await pipe.ReadAsync(raw, cancellationToken: cancellation.Token);
                        Debug.Log("Localization Parser Pipe Server Recieved Data");

                        parser.Exited -= InvokeCancellation;

                        await pipe.WriteAsync(new byte[1]);

                        var text = Encoding.UTF8.GetString(raw);

                        var structure = Structure.Parse(text);
                        return structure;
                    }
                    finally
                    {
                        pipe.Close();
                        Debug.Log("Localization Parser Pipe Server Closed");
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
                    public const string DirectoryRelativePath = "API/Parser/Executable/MB.Localization-System.Parser.exe";

                    public static Process Start()
                    {
                        if (Application.platform != RuntimePlatform.WindowsEditor)
                            throw new InvalidOperationException($"Can Only Start Localization Parser from Windows Editor");

                        var target = RetrievePath();

                        var solution = GetSolutionPath();
                        var arguments = MUtility.Process.FormatArguments(solution, PipeName);

                        var info = new ProcessStartInfo(target, arguments);
                        info.CreateNoWindow = true;
                        info.UseShellExecute = false;
                        var process = System.Diagnostics.Process.Start(info);

                        process.EnableRaisingEvents = true;

                        return process;
                    }

                    public static string RetrievePath()
                    {
                        var target = Assembly.GetExecutingAssembly().GetName().Name;

                        foreach (var assembly in AssetCollection.FindAll<AssemblyDefinitionAsset>())
                        {
                            if (assembly.name == target)
                            {
                                var path = AssetDatabase.GetAssetPath(assembly);

                                path = System.IO.Path.GetDirectoryName(path);
                                path = System.IO.Path.Combine(path, DirectoryRelativePath);
                                path = System.IO.Path.GetFullPath(path);

                                return path;
                            }
                        }

                        throw new Exception("Localization Parser Executable Couldn't be Found");
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
            }

            public static class Unity
            {
                public static Structure Process()
                {
                    var structure = new Structure();

                    //Text
                    {
                        //Scriptable Objects
                        {
                            foreach (var asset in AssetCollection.FindAll<ScriptableObject>())
                                if (asset is ILocalizationTextTarget local)
                                    structure.Text.UnionWith(local.LocalizationText);
                        }

                        //Game Object Assets (Prefabs)
                        {
                            foreach (var gameObject in AssetCollection.FindAll<GameObject>())
                            {
                                using (ComponentQuery.Collection.NonAlloc.InHierarchy<ILocalizationTextTarget>(gameObject, out var list))
                                {
                                    for (int i = 0; i < list.Count; i++)
                                        structure.Text.UnionWith(list[i].LocalizationText);
                                }
                            }
                        }

                        //Game Objects in Scene (Scene Objects)
                        {
                            var scenes = new Scene[SceneManager.sceneCount];

                            for (int i = 0; i < scenes.Length; i++)
                                scenes[i] = SceneManager.GetSceneAt(i);

                            bool Load(string path, out Scene scene)
                            {
                                for (int i = 0; i < scenes.Length; i++)
                                {
                                    if (scenes[i].path == path)
                                    {
                                        scene = scenes[i];
                                        return true;
                                    }
                                }

                                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                                return false;
                            }

                            foreach (var entry in EditorBuildSettings.scenes)
                            {
                                if (entry.enabled == false) continue;

                                var preLoaded = Load(entry.path, out var scene);

                                using (ComponentQuery.Collection.NonAlloc.InScene<ILocalizationTextTarget>(scene, out var list))
                                {
                                    for (int i = 0; i < list.Count; i++)
                                        structure.Text.UnionWith(list[i].LocalizationText);
                                }

                                if (preLoaded == false) EditorSceneManager.CloseScene(scene, true);
                            }
                        }
                    }

                    return structure;
                }

                public class Structure
                {
                    public HashSet<string> Text { get; set; }

                    public Structure()
                    {
                        Text = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }

            public class Extractor : Localization.Extraction.Processor
            {
                public override async Task Modify(Localization.Extraction.Content content)
                {
                    //Roslyn
                    {
                        var strucutre = await Roslyn.Process();
                        content.Text.UnionWith(strucutre.Text);
                    }

                    //Unity
                    {
                        var strucutre = Unity.Process();
                        content.Text.UnionWith(strucutre.Text);
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// Parameter that marks any constant expression within a method invocation as a localized text to be collected while parsing
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public class LocalizationTextParameterAttribute : Attribute
    {

    }

    /// <summary>
    /// Interface to be implemented by ScriptableObjects & MonoBehaviours to expose any localized text
    /// </summary>
    public interface ILocalizationTextTarget
    {
        IEnumerable<string> LocalizationText { get; }
    }
}