using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MB.LocalizationSystem
{
    [Global(ScriptableManagerScope.Project)]
    [SettingsMenu(Toolbox.Paths.Root + Name)]
    public partial class Localization : ScriptableManager<Localization>
    {
        public const string Name = "Localization";
        public const string Path = Toolbox.Paths.Root + Name + "/";

        [SerializeField]
        bool autoInitialize = true;
        public static bool AutoInitialize => Instance.autoInitialize;

        [SerializeField]
        Entry[] entries = Array.Empty<Entry>();
        public static Entry[] Entries => Instance.entries;

        [Serializable]
        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        public class Entry : ISerializationCallbackReceiver
        {
            [SerializeField]
            [HideInInspector]
            string title;
            public string Title => title;

            [SerializeField]
            TextAsset asset;
            public TextAsset Asset => asset;

            public void OnBeforeSerialize()
            {
                title = asset == null ? null : asset.name;
            }
            public void OnAfterDeserialize() { }

#if UNITY_EDITOR
            public void Save()
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);

                asset.WriteText(json);
            }
#endif
            public bool IsLoaded { get; private set; } = false;

            public void Load()
            {
                var json = asset.text;

                Text?.Clear();

                if (string.IsNullOrEmpty(json) == false)
                    JsonConvert.PopulateObject(json, this);

                if (Text == null) Text = new TextDictionary();
            }

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
                            return $"*{key}*";
                        }

                        return value;
                    }
                }

                public TextDictionary() : base(StringComparer.OrdinalIgnoreCase) { }
            }
        }

        public static Dictionary<string, Entry> Dictionary { get; } = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static Entry Selection { get; private set; }
        public static Entry.TextDictionary Text => Selection.Text;

        public static string Choice
        {
            get
            {
                return AutoPreferences.Read("Localization/Choice", fallback: Entries[0].Title);
            }
            private set
            {
                AutoPreferences.Set("Localization/Choice", value);
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
                Entries[i].Load();

            Dictionary.Clear();

            for (int i = 0; i < Entries.Length; i++)
                Dictionary.Add(Entries[i].Title, Entries[i]);

            Set(Choice);
        }

        public delegate void SetDelegate(Entry entry);
        public static event SetDelegate OnSet;
        public static void Set(string id)
        {
            if (Dictionary.TryGetValue(id, out var entry) == false)
                throw new Exception($"Cannot Set Localization With ID: '{id}' Because No Entry Was Registerd With That ID");

            Choice = id;

            Selection = entry;

            OnSet?.Invoke(Selection);
        }

        public static string Format(string text, IDictionary<string, string> phrases)
        {
            text = Text[text];

            if (phrases.Count == 0) return text;

            using (DisposablePool.StringBuilder.Lease(out var builder))
            {
                builder.Append(text);

                foreach (var pair in phrases)
                    builder.Replace($"{{{pair.Key}}}", Text[pair.Value]);

                return builder.ToString();
            }
        }
    }
}