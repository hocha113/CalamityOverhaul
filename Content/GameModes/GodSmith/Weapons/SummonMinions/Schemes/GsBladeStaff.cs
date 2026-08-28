using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 刀刃法杖「八卦剑轮」：环绕剑轮缓转（原版已顶级，克制档 104%）。材质：附魔星辉双匕。<br/>
    /// 签名行为：①协同「破防斩」= 单把匕首对同目标连击满 5 次，第 6 击 +40% 并在
    /// 目标身上闪过「八卦剑影」（双匕交叉幻影真弹幕，追加一段 40% 召唤伤害）
    /// ②集结「万剑门」= 旗点立剑门 60 帧（穿门敌人受三段链斩，冷却 300 帧）
    /// </summary>
    internal class GsBladeStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.Smolstar;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Bagua Blade Wheel: daggers orbit as a slow-turning wheel; a dagger's sixth consecutive strike on one foe cuts armor-deep and flashes a phantom cross-slash for 40% more, and the rally order raises a gate of blades";

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Ring,
            Radius = 90f,
            RotSpeed = 0.02f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.Smolstar];

        /// <summary>破防斩连击账（键 = 匕首 identity；owner 命中路径独占消费）</summary>
        private struct ComboEntry
        {
            public int NpcWho;
            public int NpcType;
            public int Count;
            public uint Expire;
        }

        private readonly Dictionary<int, ComboEntry> combo = [];
        private uint gateReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.04f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router)
            => TryKeepRallyField(proj, GsRallyFieldProj.StanceBladeGate, 0.5f, 2f,
                ref gateReadyTick, 300);

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //第 6 击破防斩（此前同目标已连击 5 次）
            if (combo.TryGetValue(proj.identity, out ComboEntry e)
                && e.NpcWho == target.whoAmI && e.NpcType == target.type
                && e.Expire >= Main.GameUpdateCount && e.Count >= 5) {
                modifiers.FinalDamage *= 1.4f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            uint now = Main.GameUpdateCount;
            if (!combo.TryGetValue(proj.identity, out ComboEntry e)
                || e.NpcWho != target.whoAmI || e.NpcType != target.type || e.Expire < now) {
                //换目标或超窗：连击重记
                e = new ComboEntry { NpcWho = target.whoAmI, NpcType = target.type };
            }
            e.Count++;
            e.Expire = now + 90;

            if (e.Count >= 6) {
                //破防斩已落地：清账 + 八卦剑影（owner 生成真弹幕，全端可见，演出与音效随弹幕走）
                e.Count = 0;
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsBladeStaffPhantomProj>(),
                    Math.Max(1, (int)(proj.damage * 0.40f)), 2f, proj.owner);
            }
            combo[proj.identity] = e;

            //连击账防膨胀
            if (combo.Count > 64) {
                List<int> stale = [];
                foreach (KeyValuePair<int, ComboEntry> pair in combo) {
                    if (pair.Value.Expire < now) {
                        stale.Add(pair.Key);
                    }
                }
                foreach (int key in stale) {
                    combo.Remove(key);
                }
            }
        }
    }
}
