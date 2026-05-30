using System;
using System.Collections.Generic;
using System.Linq;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal static class LegendTrialRouteResolver
    {
        public static IReadOnlyList<LegendTrialDefinition> GetAvailableTrials(IReadOnlyList<LegendTrialDefinition> definitions) {
            if (definitions == null || definitions.Count == 0) {
                return [];
            }
            return [.. definitions.Where(static d => d?.IsAvailable == true)];
        }

        public static int GetSequentialLevel(IReadOnlyList<LegendTrialDefinition> definitions, Func<LegendTrialDefinition, bool> isCompleted = null) {
            int level = 0;
            foreach (LegendTrialDefinition trial in GetAvailableTrials(definitions)) {
                bool completed = isCompleted?.Invoke(trial) ?? trial.IsCompleted;
                if (!completed) {
                    break;
                }
                level++;
            }
            return level;
        }

        public static int GetSequentialOriginalLevel(IReadOnlyList<LegendTrialDefinition> definitions, Func<LegendTrialDefinition, bool> isCompleted = null) {
            if (definitions == null || definitions.Count == 0) {
                return 0;
            }

            int level = 0;
            for (int i = 0; i < definitions.Count; i++) {
                LegendTrialDefinition trial = definitions[i];
                if (trial?.IsAvailable != true) {
                    continue;
                }

                bool completed = isCompleted?.Invoke(trial) ?? trial.IsCompleted;
                if (!completed) {
                    break;
                }
                level = i + 1;
            }
            return level;
        }

        public static string GetRouteSignature(IReadOnlyList<LegendTrialDefinition> definitions) {
            return string.Join("|", GetAvailableTrials(definitions).Select(static d => d.Key));
        }

        public static IEnumerable<string> GetLegacyCompletedKeys(IReadOnlyList<LegendTrialDefinition> definitions, int legacyLevel) {
            if (legacyLevel <= 0) {
                yield break;
            }

            int index = 0;
            foreach (LegendTrialDefinition trial in definitions ?? []) {
                if (index++ >= legacyLevel) {
                    yield break;
                }
                if (trial != null && !string.IsNullOrEmpty(trial.Key)) {
                    yield return trial.Key;
                }
            }
        }
    }
}
