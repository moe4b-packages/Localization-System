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

[assembly: AssemblySymbolDefine("MB_LOCALIZATION")]

namespace MB.LocalizationSystem
{
    [Manager]
    [SettingsMenu(Toolbox.Paths.Root + Name)]
    [LoadOrder(Runtime.Defaults.LoadOrder.LocalizationSystem)]
    public partial class Localization : ScriptableManager<Localization>
    {
        public const string Name = "Localization";

        public const string Path = Toolbox.Paths.Root + Name + "/";

        [SerializeField]
        Entry[] entries = Array.Empty<Entry>();
        public Entry[] Entries => entries;

        public Dictionary<string, Entry> Dictionary { get; } = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public Entry Selection { get; private set; }
        public Entry.TextDictionary Text => Selection.Text;

        public AutoPreferenceVariable<string> Choice { get; private set; }

        protected override void OnLoad()
        {
            base.OnLoad();

            if (IsPlaying == false) return;

            if (Entries.Length == 0)
                throw new Exception($"No Narrative Localization Entries Set," +
                    $" Please Set at Least Once in the Project Settings Window");

            Choice = new AutoPreferenceVariable<string>("Localization/Choice", Entries[0].Title);

            for (int i = 0; i < Entries.Length; i++)
                Entries[i].Load();

            Dictionary.Clear();

            for (int i = 0; i < Entries.Length; i++)
                Dictionary.Add(Entries[i].Title, Entries[i]);

            Set(Choice);
        }

        public delegate void SetDelegate(Entry entry);
        public event SetDelegate OnSet;
        public void Set(string id)
        {
            if (Dictionary.TryGetValue(id, out var entry) == false)
                throw new Exception($"Cannot Set Localization With ID: '{id}' Because No Entry Was Registerd With That ID");

            Choice.Value = id;

            Selection = entry;

            OnSet?.Invoke(Selection);
        }

        public string Format(string text, ILocalizationFormat format)
        {
            text = Text[text];

            if (format.Phrases.Count == 0) return text;

            using (DisposablePool.StringBuilder.Lease(out var builder))
            {
                builder.Append(text);

                foreach (var pair in format.Phrases)
                    builder.Replace($"{{{pair.Key}}}", Text[pair.Value]);

                return builder.ToString();
            }
        }

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
    }

    public interface ILocalizationFormat
    {
        public Dictionary<string, string> Phrases { get; }
    }
}