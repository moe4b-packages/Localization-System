using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MB.LocalizationSystem
{
    partial class Localization
    {
#if UNITY_EDITOR
        public static class Extraction
        {
            [MenuItem(Path + "Extract")]
            static void Execute() => Process().Forget();

            static bool IsRunning = false;
            public static async Task Process()
            {
                if (IsRunning) throw new InvalidOperationException("Localization Extraction Already in Progress");
                IsRunning = true;

                var id = Progress.Start("Localization Extraction", options: Progress.Options.Indefinite);

                try
                {
                    var content = await Content.Retrieve();

                    foreach (var entry in Entries)
                    {
                        entry.Load();
                        Process(entry, content);
                        entry.Save();
                    }
                }
                catch
                {
                    Debug.LogError("Localization Extraction Stopped Because of an Exception");
                    throw;
                }
                finally
                {
                    Progress.Remove(id);
                    IsRunning = false;
                }
            }

            static void Process(Entry entry, Content content)
            {
                //Text
                {
                    foreach (var text in content.Text)
                    {
                        if (entry.Text.ContainsKey(text) == false)
                            entry.Text.Add(text, text);
                    }
                }
            }

            public class Content
            {
                public HashSet<string> Text { get; }

                public Content()
                {
                    Text = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                public static async Task<Content> Retrieve()
                {
                    var content = new Content();

                    var processors = Processor.RetrieveAll();

                    foreach (var processor in processors)
                        await processor.Modify(content);

                    return content;
                }
            }

            public abstract class Processor
            {
                public abstract Task Modify(Content content);

                public Processor() { }

                public static Processor[] RetrieveAll()
                {
                    var types = TypeCache.GetTypesDerivedFrom<Processor>();

                    var array = new Processor[types.Count];

                    for (int i = 0; i < types.Count; i++)
                        array[i] = Activator.CreateInstance(types[i]) as Processor;

                    return array;
                }
            }
        }
#endif
    }
}