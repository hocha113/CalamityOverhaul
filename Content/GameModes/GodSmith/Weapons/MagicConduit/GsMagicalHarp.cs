using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 魔法竖琴重铸：渐强奏鸣。材质身份：星彩琴弦（音符凝成的流光声波）。<br/>
    /// ①热量换皮「渐强」：连奏积攒声势，白热=最强奏（音符增大提速）；
    /// 顶格维持不断奏，只涨蓝耗；<br/>
    /// ②「余韵」：音符 0.5 秒内再命中同一目标伤害 +10%（至多三层）；③拨弦顿挫体感
    /// </summary>
    internal class GsMagicalHarp : GsHeatScheme
    {
        public override int TargetItemID => ItemID.MagicalHarp;

        protected override string GsDescFallback =>
            "Reforged: unbroken play builds crescendo; at fortissimo the notes swell, race and rain music\nrapid re-hits echo for bonus damage";
        internal override float HeatPerShot => 4f;
        internal override float CoolRatePerTick => 0.6f;
        internal override float WhiteHotDamageMult => 1.12f;
        internal override float BaseDamageMult => 1f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;

        private static int NoteType => ProjectileID.QuarterNote;

        /// <summary>顶格维持（Sustain）的临界蓝耗：声势拉满后每奏更费魔</summary>
        internal override float ExtraManaCostMult(Player player, GsHeatPlayer hp)
            => hp.BoundItemType == TargetItemID && hp.Heat >= GsHeatPlayer.HeatMax ? 1.6f : 1f;

        internal override void OnHeatCapped(Player player, GsHeatPlayer hp) {
            //声势顶格的一次性提示：定音（owner 本地反馈）
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.9f, Pitch = 0.5f }, player.Center);
        }

        //==================== 动画法：拨弦顿挫 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //拨弦顿挫：按 itemAnimation 奇偶小幅交替（琴身随拨弦轻颤），随进度回稳
            //（绝对交替剖面 ±0.04·p，差分施加，数学见 GsMagicKickMath；want 取负=原 += 语义）
            float n = player.itemAnimationMax;
            int a = player.itemAnimation;
            float progress = a / n;
            float pluck = a % 2 == 0 ? 1f : -1f;
            float pluckPrev = (a + 1) % 2 == 0 ? 1f : -1f;
            player.itemLocation += new Vector2(0f, pluck * 1.2f * progress);
            GsMagicKickMath.ApplyKickDiff(player,
                -pluck * 0.04f * progress,
                -pluckPrev * 0.04f * ((a + 1) / n));
        }

        //==================== 最强奏：音符升格 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            base.GsProjOnSpawnMarked(proj, router);
            //白热出生的音符：增大提速（原版音符本就无限穿透且逐穿衰减 5%，TML 源已证，
            //设计的「穿透 +1」不可加，落地为弹速 +15% 的覆盖增益）
            if (proj.owner == Main.myPlayer && proj.type == NoteType && router.MarkData >= 1f) {
                proj.scale *= 1.25f;
                proj.velocity *= 1.15f;
                proj.netUpdate = true;
            }
        }

        //==================== 余韵：连击回响 ====================

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != NoteType) {
                return;
            }
            //余韵消费：0.5 秒内再命中同一目标 +10%/层（层数是攻击方本地量，命中链天然攻击方端）
            GsMagicalHarpNPC echo = target.GetGlobalNPC<GsMagicalHarpNPC>();
            if (echo.EchoStacks > 0 && Main.GameUpdateCount < echo.EchoUntil) {
                modifiers.FinalDamage *= 1f + 0.10f * Math.Min(echo.EchoStacks, 3);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != NoteType) {
                return;
            }
            //余韵叠层：窗口内续层（上限 3），窗口外重开
            GsMagicalHarpNPC echo = target.GetGlobalNPC<GsMagicalHarpNPC>();
            uint now = Main.GameUpdateCount;
            echo.EchoStacks = now < echo.EchoUntil ? Math.Min(echo.EchoStacks + 1, 3) : 1;
            echo.EchoUntil = now + 30;
        }
    }

    /// <summary>
    /// 余韵标记（攻击方本地量：命中钩子只在攻击方端执行，加成只在攻击方端结算）
    /// </summary>
    internal class GsMagicalHarpNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>余韵层数（上限 3）</summary>
        internal int EchoStacks;

        /// <summary>余韵窗关闭时刻</summary>
        internal uint EchoUntil;
    }
}
