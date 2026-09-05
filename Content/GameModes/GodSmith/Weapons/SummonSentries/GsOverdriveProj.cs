using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries
{
    /// <summary>
    /// 超频驻场弹幕：哨兵进入超频形态的全端同步承载体（隐形导演，自身不判伤不绘制）。<br/>
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
            //结束余韵：全端可闻的收尾音
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
        }

        //隐形导演：占位贴图是白方块，不允许画出来
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
