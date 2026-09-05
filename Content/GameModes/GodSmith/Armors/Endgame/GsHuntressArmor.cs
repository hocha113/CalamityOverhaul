using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·女猎人套 T1】「布网狩猎」：荆棘猎矢（缠着荆条的猎装铁矢）。
    /// ①被自己的爆炸陷阱炸中的敌人带上猎印；②远程命中带印目标时
    /// 消耗印记，两支荆棘猎矢自侧翼合围而来、越飞越快。<br/>
    /// 与原版套装技联动：原版加快爆炸陷阱重装，神赋让陷阱兼任标记源，陷阱本体一概不改；
    /// 印记表是攻击方端本地量（消耗也在攻击方端），猎矢 owner 侧生成
    /// </summary>
    internal class GsHuntressArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.HuntressWig];

        public override int BodyID => ItemID.HuntressJerkin;

        public override int LegsID => ItemID.HuntressPants;

        protected override string EndowLineFallback =>
            "Snare the Prey: enemies caught in your explosive traps are marked; ranged hits on marked prey call two thorn bolts from the flanks";

        /// <summary>猎印持续帧数</summary>
        private const int MarkFrames = 360;

        /// <summary>合围猎矢数</summary>
        protected virtual int BoltCount => 2;

        /// <summary>猎矢是否淬毒（红裳档）</summary>
        protected virtual bool VenomBolts => false;

        /// <summary>本套的三档爆炸陷阱爆炸弹幕</summary>
        private static bool IsTrapBoom(int type) =>
            type == ProjectileID.DD2ExplosiveTrapT1Explosion
            || type == ProjectileID.DD2ExplosiveTrapT2Explosion
            || type == ProjectileID.DD2ExplosiveTrapT3Explosion;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //猎矢自身命中不标记也不触发，防自循环；假人不算数
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsHuntressSnareBoltProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            var marks = player.GetModPlayer<GsHuntressArmorPlayer>();
            uint now = Main.GameUpdateCount;

            //陷阱爆炸命中：布下猎印
            if (sourceProj != null && IsTrapBoom(sourceProj.type)) {
                marks.Mark(target.whoAmI, now + MarkFrames);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
                }
                return;
            }

            //远程命中带印目标：消耗印记，侧翼合围
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged) || !marks.IsMarked(target.whoAmI, now)) {
                return;
            }
            marks.Clear(target.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_PhantomPhoenixShot with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //猎矢伤害按触发伤害折算并封顶；需要陷阱先布印，收益在神赋包络内
                int boltDamage = Math.Clamp((int)(damageDone * 0.30f), 10, 300);
                float toTarget = (target.Center - player.Center).ToRotation();
                for (int i = 0; i < BoltCount; i++) {
                    //自目标侧后方位合围出矢（左右交替，红裳第三支自正后）
                    float side = i == 2 ? MathHelper.Pi : (i % 2 == 0 ? 1.9f : -1.9f);
                    Vector2 spawn = target.Center + (toTarget + side).ToRotationVector2() * 240f;
                    Vector2 vel = (target.Center - spawn).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithHuntressEndow"),
                        spawn, vel, ModContent.ProjectileType<GsHuntressSnareBoltProj>(),
                        boltDamage, 2f, player.whoAmI, 0f, VenomBolts ? 1f : 0f, target.whoAmI);
                }
            }
        }

        public override void OnEndowLost(Player player, GodSmithArmorPlayer state) {
            base.OnEndowLost(player, state);
            player.GetModPlayer<GsHuntressArmorPlayer>().ClearAll();
        }
    }

    /// <summary>
    /// 【神赋·女猎人套 T3 红裳装】「布网狩猎·红裳」：同一张猎网收得更紧。
    /// 合围猎矢增至三支，且淬上剧毒
    /// </summary>
    internal class GsHuntressRedRidingArmor : GsHuntressArmor
    {
        public override int[] HeadIDs => [ItemID.HuntressAltHead];

        public override int BodyID => ItemID.HuntressAltShirt;

        public override int LegsID => ItemID.HuntressAltPants;

        protected override string EndowLineFallback =>
            "Snare the Prey, Red Riding: marked prey draws three venom-tipped thorn bolts instead";

        protected override int BoltCount => 3;

        protected override bool VenomBolts => true;
    }

    /// <summary>
    /// 猎印记录本：每玩家一份、纯攻击方端本地的 NPC 印记到期表；
    /// 不进存档不联网，换装或换方案时由 OnEndowLost 清空
    /// </summary>
    internal class GsHuntressArmorPlayer : ModPlayer
    {
        private uint[] markExpiry;

        public override void Initialize() => markExpiry = new uint[Main.maxNPCs];

        internal void Mark(int npcIndex, uint expiry) => markExpiry[npcIndex] = expiry;

        internal bool IsMarked(int npcIndex, uint now) => markExpiry[npcIndex] > now;

        internal void Clear(int npcIndex) => markExpiry[npcIndex] = 0;

        internal void ClearAll() => Array.Clear(markExpiry, 0, markExpiry.Length);
    }

    /// <summary>
    /// 荆棘猎矢：缠着荆条的猎装铁矢，自侧翼咬向带印目标（追踪修正 + 持续加速）；
    /// ai[1]=1 时淬毒
    /// </summary>
    internal class GsHuntressSnareBoltProj : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.DD2BetsyArrow}";

        /// <summary>1 = 淬毒（红裳档）</summary>
        private ref float VenomMode => ref Projectile.ai[1];

        /// <summary>合围目标的 NPC 下标</summary>
        private ref float TargetIndex => ref Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //合围修正：向既定猎物持续折向并加速（各端从 ai 取同一目标，确定一致）
            int idx = (int)TargetIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC prey = Main.npc[idx];
                if (prey.active && prey.CanBeChasedBy(Projectile)) {
                    Vector2 want = (prey.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 19f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VenomMode == 1f) {
                target.AddBuff(BuffID.Venom, 180);
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, target.Center);
            }
        }
    }
}
