using InnoVault.DataModules;
using InnoVault.Narrative.Progress;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Narrative.Data
{
    internal sealed class StoryProgress : DataModule, INarrativeProgressStore
    {
        private readonly Dictionary<string, ScenarioProgress> progress = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> choices = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> flags = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> counters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> strings = new(StringComparer.Ordinal);

        public ScenarioProgress GetProgress(string scenarioKey)
            => scenarioKey != null && progress.TryGetValue(scenarioKey, out ScenarioProgress value) ? value : ScenarioProgress.None;

        public void SetProgress(string scenarioKey, ScenarioProgress value) {
            if (!string.IsNullOrEmpty(scenarioKey)) {
                progress[scenarioKey] = value;
            }
        }

        public bool TryGetChoice(string scenarioKey, out string choiceId) {
            if (scenarioKey != null && choices.TryGetValue(scenarioKey, out choiceId)) {
                return true;
            }
            choiceId = null;
            return false;
        }

        public void SetChoice(string scenarioKey, string choiceId) {
            if (!string.IsNullOrEmpty(scenarioKey)) {
                choices[scenarioKey] = choiceId ?? string.Empty;
            }
        }

        public bool GetFlag(NarrativeProgressKey key) => flags.TryGetValue(key.Flat, out bool value) && value;
        public void SetFlag(NarrativeProgressKey key, bool value) => flags[key.Flat] = value;
        public int GetCounter(NarrativeProgressKey key) => counters.TryGetValue(key.Flat, out int value) ? value : 0;
        public void SetCounter(NarrativeProgressKey key, int value) => counters[key.Flat] = value;
        public string GetString(NarrativeProgressKey key) => strings.TryGetValue(key.Flat, out string value) ? value : null;
        public void SetString(NarrativeProgressKey key, string value) => strings[key.Flat] = value ?? string.Empty;

        public override void Reset() {
            progress.Clear();
            choices.Clear();
            flags.Clear();
            counters.Clear();
            strings.Clear();
        }

        public override void SaveData(TagCompound tag) {
            tag["progress"] = progress.Select(kv => $"{kv.Key}={(int)kv.Value}").ToList();
            tag["choices"] = choices.Select(kv => $"{kv.Key}={kv.Value}").ToList();
            tag["flags"] = flags.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
            tag["counters"] = counters.Select(kv => $"{kv.Key}={kv.Value}").ToList();
            tag["strings"] = strings.Select(kv => $"{kv.Key}={kv.Value}").ToList();
        }

        public override void LoadData(TagCompound tag, int loadedVersion) {
            Reset();
            if (tag.TryGet("progress", out List<string> progressEntries)) {
                foreach (string entry in progressEntries) {
                    int eq = entry.LastIndexOf('=');
                    if (eq > 0 && int.TryParse(entry[(eq + 1)..], out int value)) {
                        progress[entry[..eq]] = (ScenarioProgress)value;
                    }
                }
            }
            LoadStringMap(tag, "choices", choices);
            if (tag.TryGet("flags", out List<string> flagEntries)) {
                foreach (string flag in flagEntries) {
                    flags[flag] = true;
                }
            }
            if (tag.TryGet("counters", out List<string> counterEntries)) {
                foreach (string entry in counterEntries) {
                    int eq = entry.LastIndexOf('=');
                    if (eq > 0 && int.TryParse(entry[(eq + 1)..], out int value)) {
                        counters[entry[..eq]] = value;
                    }
                }
            }
            LoadStringMap(tag, "strings", strings);
        }

        private static void LoadStringMap(TagCompound tag, string key, Dictionary<string, string> target) {
            if (!tag.TryGet(key, out List<string> entries)) {
                return;
            }
            foreach (string entry in entries) {
                int eq = entry.IndexOf('=');
                if (eq >= 0) {
                    target[entry[..eq]] = entry[(eq + 1)..];
                }
            }
        }
    }
}
