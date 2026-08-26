using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries
{
    /// <summary>
    /// 超频光环：哨兵进入超频形态的全端可见承载体（远端只看里程碑的诚实口径）。<br/>
    /// ai[0]=锚定塔网络身份 ai[1]=塔类型 ai[2]=超频持续帧，全部经生成形参过线。<br/>
    /// 每帧把锚定塔的 OverdriveExpire 向后滑动续期（各端一致），owner 端驱动周期技并按龄收尾；
    /// 模式关闭或塔消亡即自灭，加法层当帧停发
    /// </summary>
    internal class GsOverdriveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        /// <summary>满充就绪提示（owner 个人读数）</summary>
        internal static LocalizedText ChargeReadyText { get; private set; }

        /// <summary>右键无可用目标提示</summary>
        internal static LocalizedText NotReadyText { get; private set; }

        //超频色板：熔金橙底 + 鎏金亮缘
        private static readonly Color OdBright = new(255, 226, 142);
        private static readonly Color OdMain = new(232, 146, 38);
        private static readonly Color OdDeep = new(140, 74, 20);

        private ref float TowerIdentity => ref Projectile.ai[0];
        private ref float TowerType => ref Projectile.ai[1];
        private ref float Duration => ref Projectile.ai[2];

        /// <summary>本端龄计数（各端独立走同一节奏，判权归 owner）</summary>
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>锚定塔本地槽快取</summary>
        private ref float CachedWho => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ChargeReadyText = this.GetLocalization("ChargeReady", () => "Overdrive ready");
            NotReadyText = this.GetLocalization("NotReady", () => "No sentry charged");
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //owner 端主导寿命；timeLeft 每帧回写，owner 掉线后 30 帧全端自然过期
            Projectile.timeLeft = 30;
            CachedWho = -1f;
        }

        private Projectile ResolveTower() {
            int who = (int)CachedWho;
            if (who >= 0 && who < Main.maxProjectiles) {
                Projectile cached = Main.projectile[who];
                if (cached.active && cached.identity == (int)TowerIdentity
                    && cached.type == (int)TowerType && cached.owner == Projectile.owner) {
                    return cached;
                }
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Projectile.owner && proj.identity == (int)TowerIdentity
                    && proj.type == (int)TowerType) {
                    CachedWho = proj.whoAmI;
                    return proj;
                }
            }
            return null;
        }

        public override void AI() {
            //模式关闭 = 加法层当帧停发：光环即刻退场，塔回原版
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            Projectile tower = ResolveTower();
            if (tower == null) {
                //塔寿命到期或被顶替：超频随之结束
                Projectile.Kill();
                return;
            }
            Projectile.Center = tower.Center;
            Projectile.timeLeft = 30;
            Age++;

            //各端滑动续期：塔与弹体读 OverdriveExpire 判超频态
            SentryGrid.StateOf(tower).OverdriveExpire = Main.GameUpdateCount + 3;

            if (Age == 1f && !VaultUtils.isServer) {
                //触发瞬间：全端可听的爆点（距离衰减免费）
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.65f, Pitch = 0.25f }, tower.Center);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(tower.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f),
                        Main.rand.NextBool() ? OdBright : OdMain,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(16, 26));
                }
            }

            //持续期辉光照明与缓升余烬（预算：每 15 帧 1 粒）
            if (!VaultUtils.isServer) {
                Lighting.AddLight(tower.Center, OdMain.ToVector3() * 0.4f);
                if (Age % 15f == 0f) {
                    PRTLoader.NewParticle<PRT_Light>(
                        tower.Center + Main.rand.NextVector2Circular(tower.width * 0.55f, tower.height * 0.4f),
                        new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.2f)),
                        OdBright, Main.rand.NextFloat(0.09f, 0.15f))?.Configure(18, 0.8f);
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //owner 端：周期技驱动 + 按龄收尾
            if (Age >= Duration) {
                Projectile.Kill();
                return;
            }
            if (SentryGrid.TryGetTowerKit(tower.type, out SentryKit kit) && kit.Host != null) {
                kit.Host.OverdrivePulse(tower, Projectile, (int)Age);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //结束余韵：一圈内收余烬，全端可见
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2CircularEdge(26f, 26f),
                    Main.rand.NextVector2Circular(1f, 1f) + new Vector2(0f, -0.6f),
                    Main.rand.NextBool() ? OdMain : OdDeep,
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float age = Age;
            float dur = MathF.Max(Duration, 1f);
            //淡入 12 帧，尾程 30 帧淡出
            float env = MathHelper.Clamp(age / 12f, 0f, 1f) * MathHelper.Clamp((dur - age) / 30f, 0f, 1f);
            if (env <= 0.01f) {
                return false;
            }
            float phase = Projectile.identity * 0.77f;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + phase);

            //触发冲击环：前 14 帧扩张消散
            if (age < 14f) {
                float t = age / 14f;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                    20f + t * 90f, 10f, OdBright, OdMain, OdDeep,
                    (1f - t) * 0.85f, timeSeed: phase);
            }

            //常驻光环：底晕 + 双星芒缓旋（去同相）
            Color halo = OdMain * (0.30f * env * pulse);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() * 0.5f, 2.6f, SpriteEffects.None, 0);
            Color core = OdBright * (0.55f * env * pulse);
            core.A = 0;
            float spin = Main.GlobalTimeWrappedHourly * 0.9f + phase;
            Main.EntitySpriteDraw(flare, pos, null, core * 0.7f, spin, flare.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flare, pos, null, core * 0.45f, -spin * 0.6f, flare.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
