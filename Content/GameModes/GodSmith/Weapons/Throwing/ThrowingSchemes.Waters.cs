using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 掷瓶共享:环境转换功能原样保留(经典不毁的功能版),增强层只在爆点追加一片 3s 领域。<br/>
    /// 表现四相:飞行 = 瓶内液光呼吸 + 沿途滴洒液珠,落点 = 全端碎瓶迸溅演出,
    /// 余痕 = 3s 雾体领域(判定圈与可见体同半径)。<br/>
    /// 掷瓶不吃远程伤害体系,不参与连投轴,经济只走两成不消耗
    /// </summary>
    internal abstract class GsWaterScheme : GsThrowScheme
    {
        /// <summary>爆点领域类型</summary>
        protected abstract int ZoneKind { get; }
        /// <summary>瓶液主色(飞行液光与落点迸溅同色,与领域雾体同板)</summary>
        protected abstract Color LiquidTint { get; }
        /// <summary>领域半径</summary>
        protected virtual float ZoneRadius => 60f;
        /// <summary>领域覆盖 ≥3 敌返还的物品(0=不返还)</summary>
        protected virtual int ZoneRefundItem => 0;

        protected override float NoConsumeChance => 0.20f;
        protected override bool JoinsCombo => false;

        /// <summary>瓶弹幕 type(加载期从原版 item.shoot 读取;IsPrimary 只在 owner 端立,
        /// 表现层要全端可见,用 type 区分瓶体与承签的领域子弹幕)</summary>
        private int flaskProjType = -1;

        public override void GsSetStaticDefaults() {
            flaskProjType = new Item(TargetItemID).shoot;
        }

        /// <summary>飞行相:沿途滴洒液珠(各端表现,低频预算)</summary>
        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != flaskProjType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, LiquidTint.ToVector3() * 0.18f);
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.08f + Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                    LiquidTint, Main.rand.NextFloat(0.28f, 0.42f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        /// <summary>飞行相:瓶内液光呼吸(原版瓶贴图垫底,液色辉光压上;identity 定相)</summary>
        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != flaskProjType) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float pulse = 0.55f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + proj.identity * 0.83f);
            Color c = LiquidTint * pulse;
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c,
                0f, glow.Size() / 2f, 0.30f, SpriteEffects.None, 0);
        }

        protected override void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != flaskProjType) {
                return;
            }
            //落点演出:碎瓶液珠喷泉 + 液光闪 + 碎裂声(各端可见可闻)
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f, Pitch = 0.1f }, proj.Center);
                PRTLoader.NewParticle<PRT_Light>(proj.Center, Vector2.Zero, LiquidTint, 0.16f)
                    ?.Configure(10, 0.85f);
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1.5f, 4.5f));
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(proj.Center, vel, LiquidTint,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(16, 26));
                }
            }
            //碎瓶起域:owner 权威;原版环境转换已在原版 Kill 流程完成,这里只追加领域
            if (proj.owner != Main.myPlayer) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsZoneProj>(), 0, 0f, proj.owner,
                ZoneKind, ZoneRadius, ZoneRefundItem);
        }
    }

    /// <summary>圣水:爆点 3s 圣辉域,域内敌受所有来源 +10%,域内玩家每秒回 1 生命;域覆盖 3 敌返还一瓶</summary>
    internal class GsHolyWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.HolyWater;
        protected override int ZoneKind => GsZoneProj.KindHoly;
        protected override Color LiquidTint => new(255, 232, 150);
        protected override int ZoneRefundItem => ItemID.HolyWater;
        protected override string GsDescFallback =>
            "Reforged: still hallows the land; the burst also raises a 3s radiant field\nFoes inside take 10% more from everything, allies inside mend 1 life per second; covering 3 foes refunds a flask";
    }

    /// <summary>邪水:爆点 3s 邪雾域,域内敌持续暗影焰并微微迟滞</summary>
    internal class GsUnholyWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.UnholyWater;
        protected override int ZoneKind => GsZoneProj.KindUnholy;
        protected override Color LiquidTint => new(150, 92, 205);
        protected override string GsDescFallback =>
            "Reforged: still corrupts the land; the burst also raises a 3s miasma\nFoes inside smolder with shadowflame and wade as if through tar";
    }

    /// <summary>血水:爆点 3s 血雾域,域内玩家的命中吸血(每秒至多 3 点)</summary>
    internal class GsBloodWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.BloodWater;
        protected override int ZoneKind => GsZoneProj.KindBlood;
        protected override Color LiquidTint => new(200, 52, 68);
        protected override string GsDescFallback =>
            "Reforged: still spreads the crimson; the burst also raises a 3s blood haze\nWhile you stand inside, your strikes leech 1 life, up to 3 per second";
    }
}
