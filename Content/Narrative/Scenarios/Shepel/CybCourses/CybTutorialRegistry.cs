using System.Collections.Generic;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    //教程目标注册表，在SHPCHUDTargets.Load时填入，Unload时清空
    //CybTutorialLead通过Key查询目标位置，不持有目标引用
    internal static class CybTutorialRegistry
    {
        private static readonly Dictionary<string, ICybTutorialTarget> _targets = new();

        public static void Register(ICybTutorialTarget target) => _targets[target.Key] = target;

        public static bool TryGet(string key, out ICybTutorialTarget target) =>
            _targets.TryGetValue(key, out target);

        public static void Clear() => _targets.Clear();
    }
}
