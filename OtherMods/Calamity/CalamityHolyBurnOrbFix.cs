using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Calamity
{
    /// <summary>
    /// 亵渎守卫 / 亵渎天神战斗崩溃修复。<br/>
    /// 灾厄的 HolyBurnOrbDrawer 在 PostUpdateProjectiles 里按类型把圣焰球 / 治愈光球的 <see cref="Projectile"/>
    /// 引用缓存进两张列表，之后只按 active / timeLeft 清理，从不复核类型。弹幕死亡后槽位被别的弹幕复用
    /// （同一拍内的连锁生成，或多人模式下服务器的同步覆写都会触发），引用仍留在列表里，
    /// DrawOrbTrails 对其调用 ModProjectile&lt;HolyBurnOrb&gt;() 得到 null 直接崩溃，且下一帧仍会复现直到进程退出。<br/>
    /// 这里在 DrawOrbTrails 执行前先按类型剔除失效引用，绘制逻辑本身不作改动。
    /// 对应上游 PR CalamityTeam/CalamityModPublic#121
    /// </summary>
    internal sealed class CalamityHolyBurnOrbFix : CalamityPatchBase
    {
        private const string HolyBurnOrbName = "HolyBurnOrb";
        private const string HolyLightName = "HolyLight";

        private static FieldInfo holyBurnOrbsField;
        private static FieldInfo holyLightsField;
        private static int holyBurnOrbType = -1;
        private static int holyLightType = -1;

        private static bool Enabled => CWRClientConfig.Instance?.CalamityHolyBurnOrbFix ?? true;

        protected override bool Install(Mod calamity) {
            Type drawerType = FindType(calamity, "CalamityMod.Projectiles.Boss.HolyBurnOrbDrawer");
            if (drawerType == null) {
                return false;
            }

            BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            holyBurnOrbsField = FindField(drawerType, "HolyBurnOrbs", privateInstance);
            holyLightsField = FindField(drawerType, "HolyLights", privateInstance);
            if (holyBurnOrbsField == null && holyLightsField == null) {
                return false;
            }

            MethodInfo drawOrbTrails = FindMethod(drawerType, "DrawOrbTrails", privateInstance);
            return Hook(drawOrbTrails, new Action<Action<object>, object>(OnDrawOrbTrails));
        }

        protected override void Setup(Mod calamity) {
            holyBurnOrbType = ModContent.TryFind(calamity.Name, HolyBurnOrbName, out ModProjectile orb) ? orb.Type : -1;
            holyLightType = ModContent.TryFind(calamity.Name, HolyLightName, out ModProjectile light) ? light.Type : -1;
        }

        protected override void Cleanup() {
            holyBurnOrbsField = null;
            holyLightsField = null;
            holyBurnOrbType = -1;
            holyLightType = -1;
        }

        private static void OnDrawOrbTrails(Action<object> orig, object self) {
            if (Enabled && self != null) {
                Prune(holyBurnOrbsField, self, holyBurnOrbType, HolyBurnOrbName);
                Prune(holyLightsField, self, holyLightType, HolyLightName);
            }
            orig(self);
        }

        //列表是 List<Projectile>，泛型参数均为可直接引用的类型，无需再走反射
        private static void Prune(FieldInfo field, object self, int expectedType, string expectedName) {
            if (field?.GetValue(self) is not List<Projectile> list || list.Count == 0) {
                return;
            }
            list.RemoveAll(projectile => !IsStillValid(projectile, expectedType, expectedName));
        }

        private static bool IsStillValid(Projectile projectile, int expectedType, string expectedName) {
            if (projectile == null || !projectile.active || projectile.ModProjectile == null) {
                return false;
            }
            if (expectedType > 0) {
                return projectile.type == expectedType;
            }
            //内容 ID 未解析成功时退回按名称比对
            ModProjectile modProjectile = projectile.ModProjectile;
            return modProjectile.Name == expectedName && modProjectile.Mod == CalamityMod;
        }
    }
}
