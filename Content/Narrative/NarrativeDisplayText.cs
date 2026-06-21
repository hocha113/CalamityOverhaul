using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static InnoVault.VaultUtils;

namespace CalamityOverhaul.Content.Narrative
{
    internal abstract class NarrativeDisplayText : VaultType<NarrativeDisplayText>, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        private readonly Dictionary<string, DialogueOverride> dialogueOverrides = [];
        private readonly Dictionary<string, Func<DialogueOverride>> dynamicDialogueProviders = [];

        public class DialogueOverride
        {
            public string Text { get; set; }
            public Color? Color { get; set; }
            public LocalizedText LocalizedText { get; set; }

            public DialogueOverride(string text, Color? color = null) {
                Text = text;
                Color = color;
            }

            public DialogueOverride(LocalizedText text, Color? color = null) {
                LocalizedText = text;
                Color = color;
            }

            public string GetDisplayText() => LocalizedText?.Value ?? Text;
        }

        protected sealed override void VaultRegister() {
            Instances.Add(this);
        }

        public sealed override void VaultSetup() {
            SetStaticDefaults();
        }

        public void SetDialogue(string key, string text, Color? color = null) {
            dialogueOverrides[key] = new DialogueOverride(text, color);
            dynamicDialogueProviders.Remove(key);
        }

        public void SetDialogueLocalized(string key, LocalizedText localizedText, Color? color = null) {
            dialogueOverrides[key] = new DialogueOverride(string.Empty, color) { LocalizedText = localizedText };
            dynamicDialogueProviders.Remove(key);
        }

        public void SetDynamicDialogue(string key, Func<DialogueOverride> provider) {
            dynamicDialogueProviders[key] = provider;
            dialogueOverrides.Remove(key);
        }

        public virtual bool Alive(Player player) => true;

        public virtual bool PreHandle(ref string key, ref Color color) => true;

        public bool Handle(ref string key, ref Color color) {
            string result = key.Split('.').Last();

            if (!PreHandle(ref key, ref color)) {
                return false;
            }

            if (dynamicDialogueProviders.TryGetValue(result, out Func<DialogueOverride> provider)) {
                DialogueOverride dialogue = provider();
                if (dialogue != null) {
                    Text(dialogue.GetDisplayText(), dialogue.Color ?? color);
                    return false;
                }
            }

            if (dialogueOverrides.TryGetValue(result, out DialogueOverride over)) {
                Text(over.GetDisplayText(), over.Color ?? color);
                return false;
            }

            return true;
        }
    }
}
