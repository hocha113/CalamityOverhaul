using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Defense
{
    /// <summary>
    /// 【星披风链】四件受击星祝：星之披风=星怒延落（追加神匠星）、蜜蜂披风=蜂群守卫（放蜂+蜂蜜）、
    /// 十字项链=殉道恢复（受击后回复窗）、星星面纱=星幕（受击后减伤窗，防连击爆发）。<br/>
    /// 受击钩子受击方本地权威；神匠星 owner 侧生成；
    /// 每玩家状态在同文件私有 <see cref="StarwardPlayer"/>
    /// </summary>
    internal class GodSmithStarCloak : GodSmithAccEffect
    {
        /// <summary>延落冷却帧数</summary>
        private const int StarCD = 120;

        public override int[] TargetItemIDs => [ItemID.StarCloak];

        protected override string EffectDescFallback =>
            "Starfall Echo: taking a hit calls down 2 godsmith stars beyond the vanilla ones\nEach deals 40 damage and rakes a golden trail, once every 2s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, StarCD)) {
                return;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = -0.2f }, player.Center);
            }
            //神匠星 owner 侧生成（受击方本地端权威）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Vector2 from = player.Center + new Vector2(Main.rand.NextFloat(-160f, 160f), -560f);
                Vector2 aim = player.Center + Main.rand.NextVector2Circular(60f, 30f);
                Vector2 vel = (aim - from).SafeNormalize(Vector2.UnitY) * 15f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), from, vel,
                    ModContent.ProjectileType<GodSmithStarCloakStarProj>(), 40, 3.5f, player.whoAmI);
            }
        }
    }

    /// <summary>蜜蜂披风：受击惊起护主蜂群，并渡一口蜂蜜疗愈，甜也蛰人</summary>
    internal class GodSmithBeeCloak : GodSmithAccEffect
    {
        /// <summary>蜂群冷却帧数</summary>
        private const int SwarmCD = 180;

        public override int[] TargetItemIDs => [ItemID.BeeCloak];

        protected override string EffectDescFallback =>
            "Guardian Swarm: taking a hit startles 3 bees from the cloak and coats you in honey for 5s\nTriggers once every 3s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, SwarmCD)) {
                return;
            }
            player.AddBuff(BuffID.Honey, 300);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.5f }, player.Center);
                //蜜珠迸落
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(12f, 16f),
                        DustID.Honey, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 2f)));
                    dust.noGravity = Main.rand.NextBool();
                }
            }
            //护主蜂 owner 侧生成，走原版蜂参数（受蜂巢背包等加成）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 5f);
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    player.beeType(), player.beeDamage(12), player.beeKB(0f), player.whoAmI);
            }
        }
    }

    /// <summary>十字项链：受击立殉道恢复窗，硬吃伤害后快速回血，站桩派的信仰</summary>
    internal class GodSmithCrossNecklace : GodSmithAccEffect
    {
        /// <summary>殉道窗口帧数</summary>
        internal const int MartyrDuration = 180;

        public override int[] TargetItemIDs => [ItemID.CrossNecklace];

        protected override string EffectDescFallback =>
            "Martyr's Mending: taking a hit opens a 3s mending window: +3 HP/s regeneration\nand knockback immunity while it lasts";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            StarwardPlayer star = player.GetModPlayer<StarwardPlayer>();
            if (star.MartyrTimer <= 0) {
                return;
            }
            player.lifeRegen += 6;
            player.noKnockback = true;
            //殉道白金微光升腾（个人读数）
            if (!VaultUtils.isServer && Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(12f, 18f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), new Color(255, 245, 210),
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(14, 0.8f);
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            player.GetModPlayer<StarwardPlayer>().MartyrTimer = MartyrDuration;
            if (VaultUtils.isServer) {
                return;
            }
            //白金十字竖闪（受击方本地端权威）
            for (int i = 0; i < 4; i++) {
                Vector2 vel = (i % 2 == 0 ? Vector2.UnitY : Vector2.UnitX) * (i < 2 ? 2f : -2f);
                PRTLoader.NewParticle<PRT_Spark>(player.Center, vel, new Color(255, 245, 210),
                    Main.rand.NextFloat(0.28f, 0.42f))?.Configure(false, Main.rand.Next(14, 20));
            }
        }
    }

    /// <summary>星星面纱：受击垂下星幕，短窗内再挨的打都减三成，专克连段爆发</summary>
    internal class GodSmithStarVeil : GodSmithAccEffect
    {
        /// <summary>星幕窗口帧数</summary>
        internal const int VeilDuration = 150;

        /// <summary>星幕触发冷却</summary>
        private const int VeilCD = 300;

        public override int[] TargetItemIDs => [ItemID.StarVeil];

        protected override string EffectDescFallback =>
            "Star Curtain: taking a hit draws a curtain of starlight for 2.5s: further damage taken is reduced by 30%\nTriggers once every 5s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            StarwardPlayer star = player.GetModPlayer<StarwardPlayer>();
            //星幕存续：星点沿幕缓落（个人读数）
            if (star.VeilTimer > 0 && !VaultUtils.isServer && Main.rand.NextBool(5)) {
                Vector2 at = player.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), -26f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                    new Color(180, 200, 255), Main.rand.NextFloat(0.05f, 0.09f))?.Configure(16, 0.8f);
            }
        }

        public override void ModifyHurt(Item item, Player player, GodSmithPlayer state, ref Player.HurtModifiers modifiers) {
            if (player.GetModPlayer<StarwardPlayer>().VeilTimer > 0) {
                modifiers.FinalDamage *= 0.7f;
            }
        }

        public override void OnHurt(Item item, Player player, GodSmithPlayer state, in Player.HurtInfo info) {
            if (!state.TryUseCooldown(item.type, VeilCD)) {
                return;
            }
            player.GetModPlayer<StarwardPlayer>().VeilTimer = VeilDuration;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                new Color(180, 200, 255), 0.05f)?.Configure(0.07f, 0.42f, 16);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), -20f),
                    new Vector2(0f, Main.rand.NextFloat(0.8f, 1.6f)),
                    Main.rand.NextBool() ? new Color(180, 200, 255) : new Color(240, 245, 255),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(16, 24));
            }
        }
    }

    /// <summary>
    /// 神匠星：一颗替披风主人讨债的坠星，划金迹自天而降；
    /// 星芒双层旋转自绘 + 拖尾星屑，落点炸开星尘
    /// </summary>
    internal class GodSmithStarCloakStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.8443f % 2.97f;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //坠星微加速，划出弧线
            if (Projectile.velocity.Length() < 19f) {
                Projectile.velocity *= 1.015f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.06f, Main.rand.NextBool()
                        ? new Color(255, 220, 120) : new Color(255, 250, 220),
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(12, 18));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.4f, 0.2f));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                    Main.rand.NextBool() ? new Color(255, 220, 120) : new Color(255, 250, 220),
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float spin = Life * 0.2f + Seed * 2f;
            float pulse = 1f + MathF.Sin(Life * 0.5f + Seed * 4f) * 0.1f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.1f, 0.5f);
            //坠速金晕拉尾
            Main.EntitySpriteDraw(glow, pos - Projectile.velocity * 0.6f, null,
                new Color(255, 190, 80) with { A = 0 } * 0.4f, Projectile.rotation, glow.Size() * 0.5f,
                new Vector2(0.7f + stretch * 1.6f, 0.5f) * pulse, SpriteEffects.None, 0);
            //星芒本体
            Main.EntitySpriteDraw(star, pos, null, new Color(255, 225, 140) with { A = 0 } * 0.95f,
                spin, star.Size() * 0.5f, 0.4f * pulse, SpriteEffects.None, 0);
            //白炽星芯
            Main.EntitySpriteDraw(star, pos, null, new Color(255, 255, 245) with { A = 0 } * 0.7f,
                -spin * 0.6f, star.Size() * 0.5f, 0.22f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>星披风链私有状态载体：殉道窗与星幕窗。受击方本地量，无需同步</summary>
    internal class StarwardPlayer : ModPlayer
    {
        /// <summary>十字项链：殉道恢复窗剩余帧数</summary>
        internal int MartyrTimer;

        /// <summary>星星面纱：星幕窗剩余帧数</summary>
        internal int VeilTimer;

        public override void PostUpdateMiscEffects() {
            if (MartyrTimer > 0) {
                MartyrTimer--;
            }
            if (VeilTimer > 0) {
                VeilTimer--;
            }
        }

        public override void UpdateDead() {
            MartyrTimer = 0;
            VeilTimer = 0;
        }
    }
}
