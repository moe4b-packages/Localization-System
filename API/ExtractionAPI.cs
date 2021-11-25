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
            public static async void Process()
            {
                var data = await Data.Retrieve();

                foreach (var entry in Entries)
                {
                    entry.Load();
                    Process(entry, data);
                    entry.Save();
                }
            }

            public static void Process(Entry entry, Data data)
            {
                //Text
                {
                    foreach (var text in data.Text)
                    {
                        if (entry.Text.ContainsKey(text) == false)
                            entry.Text.Add(text, text);
                    }
                }
            }

            public class Data
            {
                public HashSet<string> Text { get; }

                public Data()
                {
                    Text = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                public static async Task<Data> Retrieve()
                {
                    var data = new Data();

                    var processors = Processor.RetrieveAll();

                    foreach (var processor in processors)
                        await processor.Modify(data);

                    return data;
                }
            }

            public abstract class Processor
            {
                public abstract Task Modify(Data data);

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