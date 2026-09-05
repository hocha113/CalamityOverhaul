using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【腐蚀之吻·刃舌】材质：绯红教团养出的活体舌刃，命中会「亲吻」。
    /// 签名：①近战命中喷出双股灵液液束（重力下坠的金色液滴，命中降防）
    /// ②终结拍命中改在原地立起灵液间歇泉，驻场短喷柱逐跳蚀甲
    /// </summary>
    internal class GsBladetongue : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Bladetongue;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladetongueHeld>();

        protected override string GsDescFallback =>
            "Reforged: a living crimson blade; striking foes spits twin arcing streams of ichor that lower defense, and the finishing beat's kiss leaves a lingering ichor geyser erupting on the spot";
        internal static readonly Color FleshBright = new(255, 168, 160); //苍肉粉刃缘
        internal static readonly Color FleshMain = new(196, 60, 70);     //绯红活体色
        internal static readonly Color IchorGold = new(255, 212, 84);    //灵液金

        //底伤不加成（原版 55/28f 近战命中喷 0.5x 灵液流）：刀身拍均 1.02x；命中一拍一次喷
        //双股灵液束（0.25x×2 重力下坠溅射周边），终结拍改原地间歇泉（0.15x/跳、至多 3 跳）；
        //按三拍循环约 72 帧摊算，综合单体约原版 102%~118%，灵液降防是额外互惠收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 腐蚀之吻手持：三拍。0 舔斩 / 1 回舌斩 / 2 深吻重斩（前压+间歇泉）。
    /// 命中上灵液 buff，一拍一次喷灵液。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladetongueHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Bladetongue;
        protected override Color EdgeBright => GsBladetongue.FleshBright;
        protected override Color BodyMain => GsBladetongue.FleshMain;
        protected override Color HotAccent => GsBladetongue.IchorGold;

        /// <summary>一拍只喷一次灵液</summary>
        private bool spitFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 舔斩
            0 => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.12f,
            },
            //拍1 回舌斩
            1 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 1.95f, Follow = 1.1f, ReachScale = 1.02f, LeanAmp = 0.055f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍2 深吻重斩：前压
            _ => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.25f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.085f,
                DamageMult = 1.15f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.3f,
            },
        };

        //==================== 活体演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //深吻起手：湿滑的低啸
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>命中上灵液；一拍一次喷液：普通拍双股液束，终结拍原地间歇泉</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI == Main.myPlayer) {
                target.AddBuff(BuffID.Ichor, 240);
            }
            if (spitFired) {
                return;
            }
            spitFired = true;
            if (IsFinisher) {
                //腐蚀之吻：在目标脚下立起灵液间歇泉
                SpawnOwnedProj(ModContent.ProjectileType<GsBladetongueGeyserProj>(),
                    target.Bottom, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.15f)), 0.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
                }
            }
            else {
                //顺挥砍切线喷出双股液束，带着上抛的弧
                Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.25f));
                for (int i = -1; i <= 1; i += 2) {
                    SpawnOwnedProj(ModContent.ProjectileType<GsBladetongueSpitProj>(),
                        target.Center, (tangent.RotatedBy(i * 0.32f) * 6.5f) - (Vector2.UnitY * 2.2f), dmg, 0.8f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.45f, Pitch = 0.25f }, target.Center);
                }
            }
        }
    }

    /// <summary>
    /// 灵液液束：命中喷出的金色液滴，重力下坠划出液弧，命中降防；用原版金色淋浴灵液贴图
    /// </summary>
    internal class GsBladetongueSpitProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GoldenShowerFriendly;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.GoldenShowerFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            //液滴弧线：重力下坠，横速微阻
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.32f, 12f);
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Ichor, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.25f, Pitch = 0.45f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 灵液间歇泉：深吻命中处立起的驻场喷柱。周期 16 帧一涌，逐跳蚀甲（命中冷却 14）；
    /// 用原版金色淋浴灵液贴图按柱体判定拉伸画一笔作范围提示
    /// </summary>
    internal class GsBladetongueGeyserProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GoldenShowerFriendly;

        private const int TotalLife = 48;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.GoldenShowerFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f) {
                //生成点是目标脚底：柱体向上立起
                Projectile.Center += new Vector2(0f, -Projectile.height * 0.5f + 8f);
                Projectile.netUpdate = true;
            }
            //每次涌起的轻声喷溅
            if (!VaultUtils.isServer && Life % 16f == 2f) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.3f, Pitch = 0.1f }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                target.AddBuff(BuffID.Ichor, 180);
            }
        }

        /// <summary>范围提示：原版灵液贴图按柱体判定框拉伸画一笔，起落各留几帧淡入淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float fadeIn = MathHelper.Clamp(Life / 5f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            Vector2 stretch = new(Projectile.width / MathF.Max(frame.Width, 1), Projectile.height / MathF.Max(frame.Height, 1));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (fadeIn * fadeOut),
                0f, frame.Size() * 0.5f, stretch, SpriteEffects.None, 0);
            return false;
        }
    }
}
