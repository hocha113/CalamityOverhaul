using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【节日硬糖剑】材质：红白螺旋的节日硬糖。签名：①「碎糖脆响」——对同一目标
    /// 累计第 5 次命中触发碎糖：该击 +50% 伤害、玻璃脆响、红白糖屑大迸溅
    /// ②残影层红白条纹交替染色 ③糖质轻快的高音挥砍
    /// </summary>
    internal class GsCandyCaneSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CandyCaneSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCandyCaneSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every fifth strike on the same target cracks the candy coating, " +
            "dealing 50% bonus damage in a burst of red-and-white sugar shards";

        //红白糖色板
        internal static readonly Color CandyWhite = new(255, 244, 244);  //糖霜白
        internal static readonly Color CandyRed = new(224, 62, 74);      //硬糖红
        internal static readonly Color CandyHot = new(255, 148, 158);    //碎糖亮粉
        internal static readonly Color CandyDeep = new(46, 16, 22);      //暗糖影

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
    /// 碎糖计数存方案静态表（owner 命中路径独占）；残影红白条纹在 DrawExtra 叠层。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsCandyCaneSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CandyCaneSword;
        protected override Color EdgeBright => GsCandyCaneSword.CandyWhite;
        protected override Color BodyMain => GsCandyCaneSword.CandyRed;
        protected override Color HotAccent => GsCandyCaneSword.CandyHot;
        protected override Color DeepShadow => GsCandyCaneSword.CandyDeep;

        /// <summary>本次挥砍里将触发碎糖的目标（Modify 判定，OnHitFX 消费）</summary>
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

        protected override Color GlowColor => GsCandyCaneSword.CandyHot;

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

        /// <summary>计数推进：碎糖归零重新裹糖，否则 +1；顺手清理失效条目防表膨胀</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            if (shatterTargets.Contains(target.whoAmI)) {
                GsCandyCaneSword.SugarMarks.Remove(target.whoAmI);
            }
            else {
                GsCandyCaneSword.SugarMarks.TryGetValue(target.whoAmI, out int count);
                GsCandyCaneSword.SugarMarks[target.whoAmI] = count + 1;
            }
            if (GsCandyCaneSword.SugarMarks.Count > 64) {
                GsCandyCaneSword.SugarMarks.Clear();
            }
        }

        /// <summary>碎糖脆响升级反馈：玻璃高音 + 红白糖屑大迸溅（循环索引取色，不掷绘制 rand）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            if (!shatterTargets.Remove(target.whoAmI)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f, Pitch = 0.4f }, target.Center);
            for (int i = 0; i < 16; i++) {
                //红白交替按索引取色，速度沿全周均布再加散布
                Color c = i % 2 == 0 ? GsCandyCaneSword.CandyRed : GsCandyCaneSword.CandyWhite;
                Vector2 vel = (MathHelper.TwoPi * i / 16f).ToRotationVector2()
                    .RotatedByRandom(0.25) * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(true, Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsCandyCaneSword.CandyHot, 0.28f)
                ?.Configure(12, 0.85f);
        }

        /// <summary>条纹感：按 timer 奇偶交替再画一层红/白错位 1px 的加色刀身</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (CurrentPhase == PhaseRecover && fanFade <= 0.15f) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 drawPos = Hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;

            bool redFrame = (timer & 1) == 0;
            Color stripe = (redFrame ? GsCandyCaneSword.CandyRed : GsCandyCaneSword.CandyWhite) * 0.28f;
            stripe.A = 0;
            Vector2 offset = (mainAngle + MathHelper.PiOver2).ToRotationVector2() * (redFrame ? 1f : -1f);
            sb.Draw(tex, drawPos + offset, null, stripe, mainAngle + rotOffset, tex.Size() / 2f, scale, effect, 0);
        }
    }
}
