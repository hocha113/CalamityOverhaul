using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【箭袋链】三只箭袋三种猎法：魔法箭袋=蓄光第 7 矢（追加幻光箭）、
    /// 熔火箭袋=熔芯引燃（点燃+短窗穿甲）、潜猎者箭袋=潜猎态（脱战入影，远程增伤）。<br/>
    /// 全部按远程类过滤（幻光箭为 DamageClass.Default，防自喂）；
    /// 每玩家状态在同文件私有 <see cref="QuiverHuntPlayer"/>
    /// </summary>
    internal class GodSmithMagicQuiver : GodSmithAccEffect
    {
        /// <summary>蓄光所需远程命中数</summary>
        internal const int HitsPerBolt = 7;

        public override int[] TargetItemIDs => [ItemID.MagicQuiver];

        protected override string EffectDescFallback =>
            "Lumen Draw: every 7th ranged hit conjures a spectral bolt that homes in on the target\nThe bolt deals 45% of that hit as it pierces";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            QuiverHuntPlayer hunt = player.GetModPlayer<QuiverHuntPlayer>();
            if (++hunt.LumenCount < HitsPerBolt) {
                return;
            }
            hunt.LumenCount = 0;
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
            //蓄光星屑自佩戴者腕间凝出（命中钩子只在攻击方端跑）
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(10f, 14f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f),
                    Main.rand.NextBool() ? new Color(120, 170, 255) : new Color(220, 235, 255),
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(false, Main.rand.Next(12, 18));
            }
            if (player.whoAmI == Main.myPlayer) {
                int boltDamage = Math.Clamp((int)(damageDone * 0.45f), 8, 220);
                Vector2 vel = (target.Center - player.Center).SafeNormalize(Vector2.UnitX) * 13f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    ModContent.ProjectileType<GodSmithMagicQuiverBoltProj>(), boltDamage, 2.5f, player.whoAmI,
                    target.whoAmI);
            }
        }
    }

    /// <summary>熔火箭袋：命中引燃狱火并开熔芯穿甲窗，火攻节奏器</summary>
    internal class GodSmithMoltenQuiver : GodSmithAccEffect
    {
        /// <summary>引燃冷却帧数</summary>
        private const int IgniteCD = 45;

        /// <summary>熔芯窗口帧数</summary>
        internal const int MoltenDuration = 240;

        public override int[] TargetItemIDs => [ItemID.MoltenQuiver];

        protected override string EffectDescFallback =>
            "Molten Core: ranged hits ignite the target with hellfire, once every 0.75s\nEach ignition stokes your core for 4s: +6 ranged armor penetration";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            if (player.GetModPlayer<QuiverHuntPlayer>().MoltenTimer > 0) {
                player.GetArmorPenetration(DamageClass.Ranged) += 6f;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged) || !state.TryUseCooldown(item.type, IgniteCD)) {
                return;
            }
            target.AddBuff(BuffID.OnFire3, 300);
            player.GetModPlayer<QuiverHuntPlayer>().MoltenTimer = MoltenDuration;
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.2f }, target.Center);
            //熔火自创口喷涌
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f)),
                    Main.rand.NextBool() ? new Color(255, 130, 30) : new Color(255, 200, 90),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }
    }

    /// <summary>潜猎者箭袋：脱战 4 秒入潜猎态，远程增伤；受击破隐，猎手的呼吸节奏</summary>
    internal class GodSmithStalkersQuiver : GodSmithAccEffect
    {
        /// <summary>入影所需未受击帧数</summary>
        internal const int StealthDelay = 240;

        public override int[] TargetItemIDs => [ItemID.StalkersQuiver];

        protected override string EffectDescFallback =>
            "Stalker's Veil: after 4s without taking damage you slip into the veil: +8% ranged damage\nVeiled hits trail dusk sparks; taking a hit breaks the veil";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            QuiverHuntPlayer hunt = player.GetModPlayer<QuiverHuntPlayer>();
            if (!hunt.Veiled) {
                return;
            }
            player.GetDamage(DamageClass.Ranged) += 0.08f;
            //潜影暮尘绕身（个人读数）
            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(14f, 20f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)), new Color(110, 80, 160),
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(14, 0.6f);
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            QuiverHuntPlayer hunt = player.GetModPlayer<QuiverHuntPlayer>();
            if (!hunt.Veiled || !hit.DamageType.CountsAsClass(DamageClass.Ranged)
                || !state.TryUseCooldown(item.type, 12)) {
                return;
            }
            //影中命中带暮紫火花（节流防糊屏）
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? new Color(110, 80, 160) : new Color(180, 150, 230),
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(false, Main.rand.Next(12, 18));
            }
        }
    }

    /// <summary>
    /// 幻光箭：一支由蓄光凝成的幽蓝箭矢，箭意锁定既伤之敌；
    /// 双层曳光自绘 + 微幅摆尾，命中散作星屑
    /// </summary>
    internal class GodSmithMagicQuiverBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetIndex => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7717f % 2.51f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs) {
                NPC target = Main.npc[(int)TargetIndex];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.10f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.06f,
                    new Color(130, 180, 255), Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, 10);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.15f, 0.25f, 0.5f));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = 0.6f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? new Color(130, 180, 255) : new Color(230, 240, 255),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.LightShot?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.3f, 0.85f);
            //摆尾呼吸：宽度随确定性相位微颤
            float wob = 1f + MathF.Sin(Projectile.timeLeft * 0.6f + Seed * 5f) * 0.1f;
            //外层幽蓝箭体
            Main.EntitySpriteDraw(tex, pos, null, new Color(90, 150, 255) with { A = 0 } * 0.8f,
                Projectile.rotation, origin, new Vector2(stretch, 0.09f * wob), SpriteEffects.None, 0);
            //内层月白芯
            Main.EntitySpriteDraw(tex, pos, null, new Color(225, 240, 255) with { A = 0 } * 0.7f,
                Projectile.rotation, origin, new Vector2(stretch * 0.5f, 0.045f * wob), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>箭袋链私有状态载体：蓄光计数、熔芯窗口、潜猎脱战计时。攻击方端本地量，无需同步</summary>
    internal class QuiverHuntPlayer : ModPlayer
    {
        /// <summary>魔法箭袋：蓄光命中计数</summary>
        internal int LumenCount;

        /// <summary>熔火箭袋：熔芯窗口剩余帧数</summary>
        internal int MoltenTimer;

        //距上次受击的帧数（潜猎判据）
        private int sinceHurt = int.MaxValue / 2;

        /// <summary>潜猎者箭袋：当前是否处于潜猎态</summary>
        internal bool Veiled => sinceHurt >= GodSmithStalkersQuiver.StealthDelay;

        public override void PostUpdateMiscEffects() {
            if (MoltenTimer > 0) {
                MoltenTimer--;
            }
            if (sinceHurt < int.MaxValue / 2) {
                sinceHurt++;
            }
        }

        public override void OnHurt(Player.HurtInfo info) => sinceHurt = 0;

        public override void UpdateDead() {
            LumenCount = 0;
            MoltenTimer = 0;
            sinceHurt = 0;
        }
    }
}
