using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·女猎人套 T1】「布网狩猎」：荆棘猎矢（缠着荆条的猎装铁矢）。
    /// ①被自己的爆炸陷阱炸中的敌人带上猎印（印记随目标闪烁）；②远程命中带印目标时
    /// 消耗印记，两支荆棘猎矢自侧翼合围而来、越飞越快；③命中荆叶迸散，叶屑飘落。<br/>
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

        //荆棘猎矢色板（暗荆绿 + 猎装红 + 琥珀亮芯）
        internal static readonly Color HuntBright = new(255, 222, 150);
        internal static readonly Color HuntMain = new(214, 74, 62);
        internal static readonly Color HuntDeep = new(34, 62, 30);

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

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;//印记只在攻击方端存在，读数也只画给本人
            }
            var marks = player.GetModPlayer<GsHuntressArmorPlayer>();
            uint now = Main.GameUpdateCount;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!marks.IsMarked(npc.whoAmI, now)) {
                    continue;
                }
                //猎印读数：目标头顶一点猎红准星微光
                if (Main.rand.NextBool(7)) {
                    PRTLoader.NewParticle<PRT_Light>(npc.Top + new Vector2(0f, -10f),
                        Vector2.Zero, HuntMain, 0.09f)?.Configure(10, 0.8f);
                }
            }
        }

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
                    for (int i = 0; i < 4; i++) {
                        float ang = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                        PRTLoader.NewParticle<PRT_Spark>(target.Center + ang.ToRotationVector2() * 16f,
                            -ang.ToRotationVector2() * 1.4f, HuntMain, 0.36f)?.Configure(false, 14);
                    }
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
    /// 荆棘猎矢：缠着荆条的猎装铁矢，自侧翼咬向带印目标（追踪修正 + 持续加速），
    /// 矢体沿速度拉长、琥珀矢尖前置；ai[1]=1 时淬毒；命中荆叶迸散，叶屑受重力飘落
    /// </summary>
    internal class GsHuntressSnareBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>1 = 淬毒（红裳档）</summary>
        private ref float VenomMode => ref Projectile.ai[1];

        /// <summary>合围目标的 NPC 下标</summary>
        private ref float TargetIndex => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.5419f % 3.31f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

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
            Life++;
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

            //飞行相：矢尾抖落荆叶碎屑
            if (!Main.dedServ && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? GsHuntressArmor.HuntMain : GsHuntressArmor.HuntDeep,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsHuntressArmor.HuntMain.ToVector3() * (0.22f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VenomMode == 1f) {
                target.AddBuff(BuffID.Venom, 180);
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：荆叶迸散后受重力飘落，比矢体活得久
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsHuntressArmor.HuntBright, 0.11f)?.Configure(8, 0.7f);
            for (int i = 0; i < 6; i++) {
                Color leaf = Main.rand.NextBool() ? GsHuntressArmor.HuntDeep : GsHuntressArmor.HuntMain;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 4f),
                    leaf, Main.rand.NextFloat(0.24f, 0.42f))?.Configure(true, Main.rand.Next(16, 28));
            }
        }

        //==================== 绘制：三层荆棘矢体 + 速度拉伸 + 琥珀矢尖 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0.15f, 0.85f);
            //荆条缠绕的细微扭动
            float wob = MathF.Sin(Life * 0.65f + Seed * 5f) * 0.06f;
            //淬毒矢体透出毒紫
            Color mainColor = VenomMode == 1f ? new Color(186, 74, 130) : GsHuntressArmor.HuntMain;

            //暗荆压边
            Main.EntitySpriteDraw(tex, pos, null, GsHuntressArmor.HuntDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.16f + wob, 0.26f + stretch * 0.9f), SpriteEffects.None, 0);
            //猎红主体
            Main.EntitySpriteDraw(tex, pos, null, mainColor * fade, rotation, origin,
                new Vector2(0.12f + wob, 0.20f + stretch * 0.75f), SpriteEffects.None, 0);
            //琥珀亮芯：加色，前置矢尖
            Color core = GsHuntressArmor.HuntBright with { A = 0 };
            Vector2 tipPos = pos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * (8f + stretch * 20f);
            Main.EntitySpriteDraw(tex, tipPos, null, core * (0.65f * fade), rotation, origin,
                new Vector2(0.07f, 0.12f + stretch * 0.25f), SpriteEffects.None, 0);
            return false;
        }
    }
}
