using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【节日硬糖剑】材质：红白螺旋的节日硬糖。签名：①「碎糖脆响」——对同一目标
    /// 累计第 5 次命中触发碎糖：该击 +50% 伤害、玻璃脆响 ②糖质轻快的高音挥砍
    /// </summary>
    internal class GsCandyCaneSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CandyCaneSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCandyCaneSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every fifth strike on the same target cracks the candy coating, dealing 50% bonus damage in a burst of red-and-white sugar shards";
        internal static readonly Color CandyWhite = new(255, 244, 244);  //糖霜白
        internal static readonly Color CandyRed = new(224, 62, 74);      //硬糖红
        internal static readonly Color CandyHot = new(255, 148, 158);    //碎糖亮粉

        /// <summary>
        /// 糖衣计数：目标 whoAmI → 累计命中数。只在 owner 命中路径读写
        /// （近战命中只在 owner 端结算），跨挥砍持久
        /// </summary>
        internal static readonly Dictionary<int, int> SugarMarks = [];

        //底伤 +8%：第 5 击 +50%（均摊约 +10%），节日糖剑本就偏弱，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 112%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 节日硬糖剑手持：三拍轻快连击，0/1 交替脆斩，2 重敲终结。
    /// 碎糖计数存方案静态表（owner 命中路径独占）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsCandyCaneSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CandyCaneSword;
        protected override Color EdgeBright => GsCandyCaneSword.CandyWhite;
        protected override Color BodyMain => GsCandyCaneSword.CandyRed;
        protected override Color HotAccent => GsCandyCaneSword.CandyHot;

        /// <summary>本次挥砍里将触发碎糖的目标（Modify 判定，OnHitTarget 消费）</summary>
        private readonly HashSet<int> shatterTargets = [];

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //脆斩一：轻快高音
                0 => new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 1.8f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.25f,
                },
                //脆斩二：更高一格的回手
                1 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 1.65f, Follow = 1f, ReachScale = 1f, LeanAmp = 0.04f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.35f,
                },
                //重敲终结：糖锤落下
                _ => new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 5, Recover = 10,
                    RaiseBack = 2.15f, Follow = 1.15f, ReachScale = 1.12f, LeanAmp = 0.08f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = 0.05f,
                },
            };
        }

        /// <summary>碎糖判定：累计已 4 次，本击是第 5 次，+50% 并登记脆响</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            if (GsCandyCaneSword.SugarMarks.TryGetValue(target.whoAmI, out int count) && count >= 4) {
                modifiers.FinalDamage *= 1.5f;
                shatterTargets.Add(target.whoAmI);
            }
        }

        /// <summary>计数推进：碎糖归零重新裹糖并放玻璃脆响，否则 +1；顺手清理失效条目防表膨胀</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            if (shatterTargets.Remove(target.whoAmI)) {
                GsCandyCaneSword.SugarMarks.Remove(target.whoAmI);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f, Pitch = 0.4f }, target.Center);
                }
            }
            else {
                GsCandyCaneSword.SugarMarks.TryGetValue(target.whoAmI, out int count);
                GsCandyCaneSword.SugarMarks[target.whoAmI] = count + 1;
            }
            if (GsCandyCaneSword.SugarMarks.Count > 64) {
                GsCandyCaneSword.SugarMarks.Clear();
            }
        }
    }
}
