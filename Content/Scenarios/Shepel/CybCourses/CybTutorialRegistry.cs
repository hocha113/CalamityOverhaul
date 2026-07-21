using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //SHPCHUDTargets.Load填入，Lead按Key查
    internal static class CybTutorialRegistry
    {
        private static readonly Dictionary<string, ICybTutorialTarget> _targets = new();

        public static void Register(ICybTutorialTarget target) => _targets[target.Key] = target;

        public static bool TryGet(string key, out ICybTutorialTarget target) =>
            _targets.TryGetValue(key, out target);

        public static void Clear() => _targets.Clear();
    }
}
