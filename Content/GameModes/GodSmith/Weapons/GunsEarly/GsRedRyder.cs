using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 红莱德「圣诞铃跳」：胡桃木杠杆气枪·圣诞红漆。<br/>
    /// ①铜珠会在物块间弹跳两次，每跳摇一声铃、蹦一串红绿彩屑；
    /// ②同一目标 90t 内连响 5 铃：下一发化「礼物弹」（重击 + 彩屑爆开箱）；
    /// ③管式杠杆逐发压弹可打断（压几发打几发），杠杆咔嗒如拆礼物。<br/>
    /// 后坐 0.8px + 轻角度踢（气枪手感轻）。<br/>
    /// 账目：原版红莱德公认玩具枪，伤害行 ×1.35 顶格；弹跳提升弹著、礼物弹 4 发一循环均摊 +18%，
    /// 合计约 130%（对齐弱势武器上限，待游戏内标定）
    /// </summary>
    internal class GsRedRyder : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.RedRyder;

        protected override string GsDescFallback =>
            "Reforged: copper BBs ricochet off blocks twice, jingling a bell with every bounce.\n" +
            "Ring the same target five times in quick succession and the next shot is a gift round\n" +
            "that bursts into festive shrapnel. Lever-load one BB at a time; fire whenever you like";

        public override int MagSize => 10;
        public override int ReloadTicks => 50;
        public override GsReloadStyle Style => GsReloadStyle.Tube;
        protected override float GetRecoil(bool lastRound) => 0.8f;
        protected override bool EjectsShell => false;

        /// <summary>礼物弹漂字</summary>
        internal static LocalizedText GiftText;

        public override void GsSetStaticDefaults() {
            GiftText = this.GetLocalization("Gift", () => "Gift round!");
        }

        /// <summary>伤害行 ×1.35：原版玩具枪，弱势顶格，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.35f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (player.GetModPlayer<GsRedRyderPlayer>().giftArmed) {
                damage = (int)(damage * 2.2f);  //礼物弹
                knockback *= 1.5f;
            }
        }

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBB(player, source, position, velocity, damage, knockback, last: false);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBB(player, source, position, velocity, (int)(damage * 1.2f), knockback, last: true);

        /// <summary>压掉原版子弹，改射自家弹跳铜珠；礼物态在此消费</summary>
        private bool? FireBB(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int damage, float knockback, bool last) {
            GsRedRyderPlayer rp = player.GetModPlayer<GsRedRyderPlayer>();
            bool gift = rp.giftArmed;
            rp.giftArmed = false;
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<GsRedRyderPelletProj>(), damage, knockback,
                player.whoAmI, gift ? 1f : 0f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.6f, Pitch = last ? -0.1f : 0.25f }, position);
                if (gift) {
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.7f, Pitch = 0.4f }, position);
                }
            }
            return false;
        }

        //==================== 杠杆逐发压弹 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //拉开杠杆
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.1f }, player.Center);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (VaultUtils.isServer) {
                return;
            }
            //杠杆咔嗒 + 一粒铜珠落管，音阶爬升如拆礼物
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.2f + 0.07f * roundIndex }, player.Center);
            PRTLoader.NewParticle<PRT_Sparkle>(player.Center + new Vector2(player.direction * 10f, -2f),
                new Vector2(-player.direction * 0.4f, -0.6f),
                roundIndex % 2 == 0 ? new Color(226, 78, 78) : new Color(96, 200, 110),
                Main.rand.NextFloat(0.3f, 0.45f))
                ?.Configure(Color.White, Main.rand.Next(10, 16), 0.1f, 0.6f);
        }

        //==================== 后坐姿态（差分，见 GsGunKickMath） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 0.8f, 0.04f);
    }

    /// <summary>
    /// 红莱德专属本地态：铃响连击与礼物弹待发。全部只在 owner 命中路径读写，不同步
    /// </summary>
    internal class GsRedRyderPlayer : ModPlayer
    {
        public int jingleTarget = -1;   //连铃目标
        public int jingleCount;         //连铃数
        public int jingleWindow;        //连铃窗口余帧
        public bool giftArmed;          //礼物弹待发

        public override void PostUpdate() {
            if (jingleWindow > 0 && --jingleWindow == 0) {
                jingleCount = 0;
                jingleTarget = -1;
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource) {
            jingleCount = 0;
            jingleTarget = -1;
            giftArmed = false;
        }
    }

    /// <summary>
    /// 圣诞铜珠：小口径 BB 弹，物块间弹跳两次（跳后减速带坠），每跳摇铃蹦彩。
    /// ai[0]=礼物弹旗标。珠体自绘（铜芯 + 红绿交替星闪），identity 决定本珠彩色相位
    /// </summary>
    internal class GsRedRyderPelletProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color FestiveRed = new(226, 78, 78);
        private static readonly Color FestiveGreen = new(96, 200, 110);
        private static readonly Color CopperBody = new(214, 150, 92);

        private bool Gift => Projectile.ai[0] > 0f;
        private int Bounces {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }
        private Color FestiveColor => (Projectile.identity + Bounces) % 2 == 0 ? FestiveRed : FestiveGreen;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 300;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            //跳过一次后开始坠弧（气枪珠没那么硬气）
            if (Bounces > 0) {
                Projectile.velocity.Y += 0.12f;
                Projectile.velocity *= 0.995f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.08f;

            if (!VaultUtils.isServer && Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center - Projectile.velocity * 0.3f,
                    -Projectile.velocity * 0.04f,
                    Gift ? Color.White : FestiveColor,
                    Main.rand.NextFloat(0.25f, 0.4f))
                    ?.Configure(FestiveColor, Main.rand.Next(8, 13), 0.12f, 0.65f);
            }
            if (Gift) {
                Lighting.AddLight(Projectile.Center, FestiveColor.ToVector3() * 0.3f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Bounces >= 2) {
                return true;    //第三次触块寿终
            }
            Bounces++;
            //镜面反弹带衰减
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.85f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.85f;
            }
            if (!VaultUtils.isServer) {
                //每跳一声铃 + 彩屑
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.5f,
                    Pitch = -0.1f + Bounces * 0.25f
                }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                        Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY,
                        FestiveColor, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Color.White, Main.rand.Next(10, 16), 0.15f, 0.7f);
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                Player player = Main.player[Projectile.owner];
                GsRedRyderPlayer rp = player.GetModPlayer<GsRedRyderPlayer>();
                if (Gift) {
                    //礼物弹开箱：彩屑爆（族内共享调色爆，identity 定红绿）
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                        Math.Max(1, Projectile.damage / 2), 2f, Projectile.owner,
                        70f, 2f, Projectile.identity % 2 == 0 ? 0f : 3f);
                }
                else {
                    //连铃计数
                    if (rp.jingleTarget != target.whoAmI) {
                        rp.jingleTarget = target.whoAmI;
                        rp.jingleCount = 0;
                    }
                    rp.jingleCount++;
                    rp.jingleWindow = 90;
                    if (rp.jingleCount >= 5 && !rp.giftArmed) {
                        rp.giftArmed = true;
                        rp.jingleCount = 0;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = 0.6f }, player.Center);
                            CombatText.NewText(player.getRect(), FestiveGreen, GsRedRyder.GiftText.Value);
                        }
                    }
                    else if (!VaultUtils.isServer) {
                        //命中铃音阶
                        SoundEngine.PlaySound(SoundID.Item35 with {
                            Volume = 0.45f,
                            Pitch = -0.2f + rp.jingleCount * 0.15f
                        }, target.Center);
                    }
                }
            }
            if (!VaultUtils.isServer && Gift) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.3f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float twinkle = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);

            if (Gift) {
                //礼物弹：白芯 + 缎带双星
                Color ribbon = FestiveColor * (0.85f * twinkle);
                ribbon.A = 0;
                Main.EntitySpriteDraw(star, drawPos, null, ribbon, Projectile.rotation,
                    star.Size() / 2f, 0.34f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, drawPos, null, ribbon * 0.7f, Projectile.rotation + MathHelper.PiOver4,
                    star.Size() / 2f, 0.24f, SpriteEffects.None, 0);
                Color core = Color.White;
                core.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f, 0.11f, SpriteEffects.None, 0);
            }
            else {
                //铜珠本体 + 循环红绿微星
                Color body = CopperBody;
                body.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, body, 0f, glow.Size() / 2f, 0.09f, SpriteEffects.None, 0);
                Color tint = FestiveColor * (0.6f * twinkle);
                tint.A = 0;
                Main.EntitySpriteDraw(star, drawPos, null, tint, Projectile.rotation,
                    star.Size() / 2f, 0.18f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
