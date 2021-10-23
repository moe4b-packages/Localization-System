using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MB.LocalizationSystem
{
    [Global(ScriptableManagerScope.Project)]
    [SettingsMenu(Toolbox.Paths.Root + Name)]
    public class Localization : ScriptableManager<Localization>
    {
        public const string Name = "Localization";
        public const string Path = Toolbox.Paths.Root + Name + "/";

        [SerializeField]
        bool autoInitialize = true;
        public static bool AutoInitialize => Instance.autoInitialize;

        [SerializeField]
        LocalizationEntry[] entries = Array.Empty<LocalizationEntry>();
        public static LocalizationEntry[] Entries => Instance.entries;

        public static Dictionary<string, LocalizationEntry> Dictionary { get; } = new Dictionary<string, LocalizationEntry>(StringComparer.OrdinalIgnoreCase);

        public static LocalizationEntry Selection { get; private set; }

        public static LocalizationComposition.TextDictionary Text => Selection.Composition.Text;

        public static string Choice
        {
            get
            {
                return AutoPreferences.Read(ChoiceID, fallback: Entries[0].Title);
            }
            private set
            {
                AutoPreferences.Set(ChoiceID, value);
            }
        }
        const string ChoiceID = "Localization/Choice";

        public static class IO
        {
#if UNITY_EDITOR
            public static void Save(TextAsset asset, LocalizationComposition entry)
            {
                var json = JsonConvert.SerializeObject(entry, Formatting.Indented);

                asset.WriteText(json);
            }
#endif

            public static LocalizationComposition Load(TextAsset asset)
            {
                var json = asset.text;

                if (string.IsNullOrEmpty(json))
                    return new LocalizationComposition();

                return JsonConvert.DeserializeObject<LocalizationComposition>(json);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnRuntime()
        {
            if (AutoInitialize) Prepare();
        }

        internal static void Prepare()
        {
            if (Entries.Length == 0)
                throw new Exception($"No Narrative Localization Entries Set," +
                    $" Please Set at Least Once in the Project Settings Window");

            for (int i = 0; i < Entries.Length; i++)
            {
                var composition = IO.Load(Entries[i].Asset);
                Entries[i].Set(composition);
            }

            Dictionary.Clear();

            for (int i = 0; i < Entries.Length; i++)
                Dictionary.Add(Entries[i].Title, Entries[i]);

            Set(Choice);
        }

        public delegate void SetDelegate(LocalizationEntry entry);
        public static event SetDelegate OnSet;
        public static void Set(string id)
        {
            if (Dictionary.TryGetValue(id, out var entry) == false)
                throw new Exception($"Cannot Set Localization With ID: '{id}' Because No Entry Was Registerd With That ID");

            Choice = id;

            Selection = entry;

            OnSet?.Invoke(Selection);
        }

#if UNITY_EDITOR
        public static class Extraction
        {
            [MenuItem(Path + "Extract")]
            public static void ProcessAll()
            {
                var processors = RetrieveAllProcessors();

                foreach (var entry in Entries)
                    Process(entry, processors);
            }

            public static LocalizationComposition Process(LocalizationEntry entry)
            {
                var processors = RetrieveAllProcessors();

                return Process(entry, processors);
            }
            public static LocalizationComposition Process(LocalizationEntry entry, Processor[] processors)
            {
                var composition = IO.Load(entry.Asset);

                ProcessText(composition.Text, processors);

                IO.Save(entry.Asset, composition);

                return composition;
            }

            public static void ProcessText(LocalizationComposition.TextDictionary text, Processor[] processors)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < processors.Length; i++)
                {
                    foreach (var entry in processors[i].RetrieveText())
                    {
                        if (text.ContainsKey(entry) == false)
                            text.Add(entry, entry);

                        set.Add(entry);
                    }
                }

                foreach (var key in text.Keys.ToArray())
                {
                    if (set.Contains(key) == false)
                        text.Remove(key);
                }
            }

            public static Processor[] RetrieveAllProcessors()
            {
                var types = TypeCache.GetTypesDerivedFrom<Processor>();

                var array = new Processor[types.Count];

                for (int i = 0; i < types.Count; i++)
                    array[i] = Activator.CreateInstance(types[i]) as Processor;

                return array;
            }

            public abstract class Processor
            {
                public abstract HashSet<string> RetrieveText();

                public Processor() { }
            }
        }
#endif
    }

    [Serializable]
    public class LocalizationEntry : ISerializationCallbackReceiver
    {
        [SerializeField]
        [HideInInspector]
        string title;
        public string Title => title;

        [SerializeField]
        TextAsset asset;
        public TextAsset Asset => asset;

        public LocalizationComposition Composition { get; protected set; }
        internal void Set(LocalizationComposition reference)
        {
            Composition = reference;
        }

        public void OnBeforeSerialize()
        {
            title = asset == null ? null : asset.name;
        }
        public void OnAfterDeserialize() { }
    }

    [JsonObject]
    public class LocalizationComposition
    {
        [JsonProperty]
        public TextDictionary Text { get; set; }
        [JsonArray]
        public class TextDictionary : Dictionary<string, string>
        {
            public new string this[string key]
            {
                get
                {
                    if (TryGetValue(key, out var value) == false)
                    {
                        Debug.LogWarning($"No Localization Text Found for '{key}'");
                        return $"${key}$";
                    }

                    return value;
                }
            }

            public TextDictionary() : base(StringComparer.OrdinalIgnoreCase) { }
        }

        public LocalizationComposition()
        {
            Text = new TextDictionary();
        }
        public LocalizationComposition(TextDictionary text)
        {
            this.Text = text;
        }
    }
}