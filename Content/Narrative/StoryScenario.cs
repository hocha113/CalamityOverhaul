using InnoVault.Narrative.Audio;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Narrative
{
    internal abstract class StoryScenario : NarrativeScenario
    {
        private const string ScenarioNamespaceRoot = ".Content.Scenarios.";
        private const string ScenarioPathRoot = "Content/Scenarios";

        /// <summary>是否按命名空间约定加载 <c>L1.ogg...Ln.ogg</c> 配音</summary>
        public virtual bool AutoLoadVoice => true;

        protected virtual string VoiceDirectory => ResolveVoiceDirectory();
        protected OptionalVoiceBank Voice { get; } = new();

        public sealed override void VaultSetup() {
            base.VaultSetup();
            LoadVoiceBank();
        }

        protected sealed class OptionalVoiceBank
        {
            private NarrativeVoiceBank voiceBank;

            public SoundStyle? this[int lineNumber]
                => voiceBank != null && voiceBank.TryGet(lineNumber, out SoundStyle voice) ? voice : null;

            internal void Set(NarrativeVoiceBank value) => voiceBank = value;
        }

        private void LoadVoiceBank() {
            Voice.Set(null);
            if (!AutoLoadVoice || Main.dedServ) {
                return;
            }

            string voiceDirectory = VoiceDirectory;
            if (string.IsNullOrWhiteSpace(voiceDirectory)) {
                Mod.Logger.Warn($"Unable to infer the voice directory for {GetType().FullName}.");
                return;
            }

            int lineCount = CountVoiceLines(voiceDirectory);
            if (lineCount == 0) {
                return;
            }

            Voice.Set(NarrativeVoiceBank.Create(Mod, voiceDirectory, lineCount));
        }

        private int CountVoiceLines(string voiceDirectory) {
            int lineNumber = 1;
            while (Mod.FileExists($"{voiceDirectory}/L{lineNumber}.ogg")) {
                lineNumber++;
            }
            return lineNumber - 1;
        }

        private string ResolveVoiceDirectory() {
            string typeNamespace = GetType().Namespace;
            string namespacePrefix = $"{Mod.Name}{ScenarioNamespaceRoot}";
            if (typeNamespace == null || !typeNamespace.StartsWith(namespacePrefix, StringComparison.Ordinal)) {
                return null;
            }

            string relativeNamespace = typeNamespace[namespacePrefix.Length..];
            int nestedNamespaceIndex = relativeNamespace.IndexOf('.');
            if (nestedNamespaceIndex < 0) {
                return $"{ScenarioPathRoot}/{relativeNamespace}/Lines/{GetType().Name}";
            }

            string scenarioScope = relativeNamespace[..nestedNamespaceIndex];
            string nestedNamespace = relativeNamespace[(nestedNamespaceIndex + 1)..].Replace('.', '/');
            return $"{ScenarioPathRoot}/{scenarioScope}/Lines/{nestedNamespace}/{GetType().Name}";
        }
    }

    internal static class StoryScenarioCompositionExtensions
    {
        public static NarrativeComposer Say(
            this NarrativeComposer composer,
            CharacterId speaker,
            string text,
            SoundStyle? voice,
            Action onEnter = null,
            Action onExit = null)
            => voice.HasValue
                ? composer.Say(speaker, text, voice.Value, onEnter, onExit)
                : composer.Say(speaker, text, onEnter, onExit);

        public static NarrativeComposer Say(
            this NarrativeComposer composer,
            CharacterId speaker,
            ExpressionId expression,
            string text,
            SoundStyle? voice,
            Action onEnter = null,
            Action onExit = null)
            => voice.HasValue
                ? composer.Say(speaker, expression, text, voice.Value, onEnter, onExit)
                : composer.Say(speaker, expression, text, onEnter, onExit);

        public static NarrativeComposer SayTimed(
            this NarrativeComposer composer,
            CharacterId speaker,
            string text,
            float seconds,
            SoundStyle? voice,
            Action onEnter = null,
            Action onExit = null)
            => voice.HasValue
                ? composer.SayTimed(speaker, text, seconds, voice.Value, onEnter, onExit)
                : composer.SayTimed(speaker, text, seconds, onEnter, onExit);

        public static NarrativeComposer SayTimed(
            this NarrativeComposer composer,
            CharacterId speaker,
            ExpressionId expression,
            string text,
            TimedSettings timed,
            SoundStyle? voice,
            bool muteTypingSound = true,
            Action onEnter = null,
            Action onExit = null)
            => voice.HasValue
                ? composer.SayTimed(speaker, expression, text, timed, voice.Value, muteTypingSound, onEnter, onExit)
                : composer.SayTimed(speaker, expression, text, timed, onEnter, onExit);

        public static ChoiceBuilder Voice(
            this ChoiceBuilder builder,
            SoundStyle? voice,
            bool muteTypingSound = true)
            => voice.HasValue ? builder.Voice(voice.Value, muteTypingSound) : builder;
    }
}