using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 夺命枪「收殓人」：黑铁殓棺左轮·血锈铭纹。<br/>
    /// ①殓魂：击杀或重创濒死之敌收下一盏魂灯，魂灯飞回入怀补血（每匣一次）；
    /// ②末发「葬钟」：血色重弹鸣钟出膛（曳血光痕），命中震出哀鸣冲击、击退翻倍；
    /// ③逐膛装填可打断，膛声如钉棺，钉一颗少一声。<br/>
    /// 后坐 1.2px（末发 2.4px）+ 角度踢。<br/>
    /// 账目：周期 150t 打 6 发对原版 5.8 发（×1.03），末发均值 1.1、魂灯为续航收益，
    /// 伤害行 ×1.2（原版公认偏弱）→ 约 118%（待游戏内标定）
    /// </summary>
    internal class GsTheUndertaker : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.TheUndertaker;

        protected override string GsDescFallback =>
            "Reforged: fell a foe, or wound one near death, and the gun collects a soul lantern\n" +
            "that drifts back to mend you once per cylinder.\n" +
            "The final chamber tolls the burial bell: a heavy blood round with doubled knockback";

        public override int MagSize => 6;
        public override int ReloadTicks => 44;
        public override GsReloadStyle Style => GsReloadStyle.Cylinder;
        protected override float GetRecoil(bool lastRound) => lastRound ? 2.4f : 1.2f;

        //血锈色板
        private static readonly Color BloodDeep = new(126, 26, 30);
        private static readonly Color BloodBright = new(220, 66, 58);

        /// <summary>伤害行 ×1.2：原版夺命枪公认偏弱，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.2f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (lastRound) {
                damage = (int)(damage * 1.5f);  //葬钟重弹
                knockback *= 2f;
                velocity *= 1.2f;
            }
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 1f;
            if (!VaultUtils.isServer) {
                //钟鸣出膛：低哑钟声 + 血色光痕驻留
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = -0.55f }, position);
                Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_PallbearerTracer>(position, Vector2.Zero, BloodBright, 1f)
                    ?.Configure(position, position + aim * 150f, 14f, 14);
            }
            return null;
        }

        //==================== 殓魂（owner 端权威） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsGunsEarlyPlayer mp = State(player);

            //葬钟命中：哀鸣冲击（个人反馈层）
            if (router.MarkData >= 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.55f, Pitch = -0.2f }, target.Center);
                PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero,
                    BloodDeep * 0.85f, 0.16f)?.Configure(Vector2.One, 0f, 1.6f, 14);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(target.Center,
                        Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY * 1.5f,
                        BloodBright, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 32));
                }
            }

            //收殓：击杀或打至 20% 血线以下，各匣一盏
            if (!mp.healUsedThisMag && (target.life <= 0 || target.life < target.lifeMax / 5)) {
                mp.healUsedThisMag = true;
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, -Vector2.UnitY * 2f,
                    ModContent.ProjectileType<GsUndertakerSoulProj>(), 0, 0f, proj.owner);
            }
        }

        //==================== 钉棺装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //甩轮 + 空壳如钉散落
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.4f }, player.Center);
            int shells = MagSize - mp.magLeft;
            for (int i = 0; i < shells; i++) {
                PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 6f, -2f),
                    new Vector2(-player.direction * Main.rand.NextFloat(0.5f, 1.4f), -Main.rand.NextFloat(1.5f, 2.8f)),
                    new Color(96, 60, 54), Main.rand.NextFloat(0.4f, 0.55f))
                    ?.Configure(BloodDeep, Main.rand.Next(22, 32), 0.6f);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //钉棺闷响：低音定死、不上行，钉一颗少一声
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.75f, Pitch = -0.5f }, player.Center);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.15f, Pitch = -0.6f }, player.Center);
            }
        }

        //==================== 后坐姿态（差分，见 GsGunKickMath） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 1.6f, 0.07f);

        //==================== 弹幕表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //葬钟弹：血雾曳尾
            Lighting.AddLight(proj.Center, 0.22f, 0.06f, 0.06f);
            if (proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.35f,
                    -proj.velocity * 0.05f, BloodBright, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(BloodDeep, Main.rand.Next(10, 16), 0.1f, 0.7f);
            }
        }
    }

    /// <summary>
    /// 殓魂灯：从收殓处升起、绕行一拍后归主的魂灯，归怀补 10 血。
    /// 无伤害判定，纯 owner 端结算；灯体自绘（魂芯 + 外晕 + 尾焰魂点）
    /// </summary>
    internal class GsUndertakerSoulProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color SoulTeal = new(150, 226, 214);
        private static readonly Color SoulDeep = new(60, 130, 140);

        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //起灯一拍上飘，随后渐加速归主
            if (Projectile.localAI[0] < 22f) {
                Projectile.localAI[0]++;
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y -= 0.06f;
            }
            else {
                Vector2 toOwner = owner.MountedCenter - Projectile.Center;
                float dist = toOwner.Length();
                float speed = MathHelper.Clamp(4f + Projectile.localAI[0] * 0.16f, 4f, 16f);
                Projectile.localAI[0]++;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    toOwner.SafeNormalize(Vector2.UnitY) * speed, 0.12f);
                //侧摆游魂步
                Vector2 side = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
                Projectile.Center += side * MathF.Sin(Projectile.localAI[0] * 0.25f + Seed * 6f) * 1.2f;

                if (dist < 26f) {
                    //归怀：owner 端结算补血（HealEffect 自带广播）
                    if (Projectile.owner == Main.myPlayer) {
                        owner.Heal(10);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.3f }, owner.Center);
                        PRTLoader.NewParticle<PRT_StarPulseRing>(owner.Center, Vector2.Zero, SoulTeal, 0f)
                            ?.Configure(0.03f, 0.3f, 10);
                    }
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, SoulTeal.ToVector3() * 0.4f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0) {
                //尾焰魂点
                PRTLoader.NewParticle<PRT_SoulLight>(Projectile.Center, -Projectile.velocity * 0.1f,
                    SoulTeal, Main.rand.NextFloat(0.25f, 0.4f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Seed * 10f);
            //外晕
            Color halo = SoulDeep * (0.5f * pulse);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f, 0.34f, SpriteEffects.None, 0);
            //魂芯：速度拉伸的灯焰
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0f, 0.5f);
            Color core = Color.Lerp(SoulTeal, Color.White, 0.4f) * pulse;
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, Projectile.velocity.ToRotation(),
                glow.Size() / 2f, new Vector2(0.2f + stretch, 0.16f), SpriteEffects.None, 0);
            return false;
        }
    }
}
