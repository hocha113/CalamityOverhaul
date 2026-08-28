using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【湮光刃·光束剑】材质：地牢幽蓝晶簇里析出的纯光之刃，掷出的束是一柄光铸的剑影。
    /// 签名：①每一斩发出光束波（光剑剪影：长核线+护手短横），光束命中为刃身蓄光
    /// ②蓄满三层后下一道升格为湮光巨束：更宽更亮、贯穿 +3，出膛自带轰鸣
    /// ③蓄光鞘沿刀脊点亮，蓄几成亮几成，满蓄呼吸脉动
    /// </summary>
    internal class GsBeamSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BeamSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBeamSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash casts a beam wave; beam hits charge the blade, " +
            "and at three charges the next beam erupts into a vast annihilating lance, " +
            "wider, brighter, piercing deeper";

        //湮光色板
        internal static readonly Color IonBright = new(214, 232, 255); //光刃白蓝
        internal static readonly Color IonBlue = new(110, 150, 240);   //幽蓝体色
        internal static readonly Color IonViolet = new(190, 160, 255); //湮光紫白
        internal static readonly Color IonDeep = new(18, 18, 40);      //暗夜垫影

        /// <summary>蓄光层数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Charge;

        //底伤不加成（原版 52/useAnim20 每挥一发全伤光束）：刀身拍均 1.07x + 光束 0.8x，
        //三层蓄光（光束命中攒）后下一道升格 1.4x 湮光巨束（贯穿 6），升格增益摊进循环；
        //按三拍循环约 53 帧摊算，贴脸（刀+束齐中）约原版 117%、纯刃外约 113%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 湮光刃手持：三拍快剑。0 顺斩 / 1 返斩（音调上扬）/ 2 重斩（小前压）。
    /// 每拍斩切爆发发出光束波；蓄满三层时 owner 侧把下一道升格为湮光巨束
    /// （远端靠生成包看到正确形态）。能量刃无血尘。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBeamSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BeamSword;
        protected override Color EdgeBright => GsBeamSword.IonBright;
        protected override Color BodyMain => GsBeamSword.IonBlue;
        protected override Color HotAccent => GsBeamSword.IonViolet;
        protected override Color DeepShadow => GsBeamSword.IonDeep;

        /// <summary>纯光之刃不喷血</summary>
        protected override bool BleedOnFlesh => false;

        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsBeamSword.IonViolet : GsBeamSword.IonBright;

        private bool beamFired;

        private GsBeamSword Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsBeamSword : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.0f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.8f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
            },
            //拍2 重斩：小前压
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.0f, SwingPitch = -0.2f,
            },
        };

        //==================== 湮光演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);
            //光刃嗡鸣
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.3f, Pitch = 0.3f }, Owner.Center);
        }

        /// <summary>每拍斩切爆发发光束；蓄满三层则升格湮光巨束（owner 决策，束形态随生成包过线）</summary>
        protected override void OnSlashBegin() {
            if (beamFired) {
                return;
            }
            beamFired = true;
            if (IsFinisher) {
                SetFlash(6);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.38f, Pitch = 0.2f + 0.06f * ComboStage }, Owner.Center);
            }
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsBeamSword scheme = Scheme;
            bool annihilate = scheme != null && scheme.Charge >= 3;
            if (annihilate) {
                scheme.Charge = 0;
                SetFlash(8);
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * (annihilate ? 1.4f : 0.8f)));
            SpawnOwnedProj(ModContent.ProjectileType<GsBeamSwordBeamProj>(),
                Hand + dir * (FullReach * 0.7f), dir * (annihilate ? 16f : 15f), dmg,
                Projectile.knockBack * 0.5f, swingDir, annihilate ? 1f : 0f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //能量切口：蓝白光屑顺切线飞出
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            for (int i = 0; i < (IsFinisher ? 5 : 3); i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    tangent.RotatedByRandom(0.4) * Main.rand.NextFloat(3f, 7f),
                    Main.rand.NextBool() ? GsBeamSword.IonBright : GsBeamSword.IonViolet,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>蓄光鞘：软光沿刀脊点亮，蓄几成亮几成；满蓄呼吸脉动+尖端星辉（owner 侧，层数不共享）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return;
            }
            int charge = Scheme?.Charge ?? 0;
            if (charge <= 0 || fanFade <= 0.05f) {
                return;
            }
            float frac = charge / 3f;
            bool full = charge >= 3;
            float pulse = full ? 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f) : 1f;

            //光鞘自护手向刀尖爬升
            float len = mainReach * 0.72f * frac;
            Vector2 mid = Hand + (mainAngle.ToRotationVector2() * (mainReach * 0.16f + len * 0.5f)) - Main.screenPosition;
            Color sheath = GsBeamSword.IonViolet * (0.42f * frac * fanFade * pulse);
            sheath.A = 0;
            sb.Draw(glow, mid, null, sheath, mainAngle, glow.Size() * 0.5f,
                new Vector2(len / glow.Width, 0.2f), SpriteEffects.None, 0f);

            //满蓄：刀尖星辉候发
            if (full) {
                Color tip = GsBeamSword.IonBright * (0.6f * fanFade * pulse);
                tip.A = 0;
                sb.Draw(star, mainTip - Main.screenPosition, null, tip, 0f, star.Size() * 0.5f,
                    0.2f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 光束波：光铸的剑影（长核线+护手短横的光剑剪影）。出膛 16 帧减速回稳后滑行，
    /// 行进中渐薄；命中为刃身蓄光（owner 记账）。ai[0]=挥动符号 ai[1]=湮光旗
    /// （更宽更亮、贯穿 6、湮光巨束命中不再蓄光、出膛自带轰鸣与光环）
    /// </summary>
    internal class GsBeamSwordBeamProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool Annihilate => Projectile.ai[1] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.timeLeft = 46;
        }

        public override void AI() {
            Life++;
            if (Life == 1f) {
                if (Annihilate) {
                    //湮光巨束：贯穿 +3、体格更大、出膛轰鸣
                    Projectile.penetrate = 6;
                    Projectile.Resize(60, 60);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.0f }, Projectile.Center);
                        for (int i = 0; i < 10; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                                Main.rand.NextBool() ? GsBeamSword.IonBright : GsBeamSword.IonViolet,
                                Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
                        }
                    }
                }
            }

            //出膛减速回稳：前 16 帧 15~16 → 约 11，尾段收速
            if (Life <= 16f) {
                Projectile.velocity *= 0.978f;
            }
            else if (Projectile.timeLeft < 10) {
                Projectile.velocity *= 0.95f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsBeamSword.IonBlue.ToVector3() * (Annihilate ? 0.7f : 0.4f));

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //光尘航迹
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Projectile.velocity * 0.06f,
                    Annihilate ? GsBeamSword.IonViolet : GsBeamSword.IonBlue,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.6f);
            }
        }

        /// <summary>命中蓄光（湮光巨束不再蓄，防永动）；满层时 owner 听到候发提示音</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Annihilate && Projectile.owner == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.BeamSword, out GodSmithScheme s) && s is GsBeamSword scheme) {
                int old = scheme.Charge;
                scheme.Charge = Math.Min(3, scheme.Charge + 1);
                if (old < 3 && scheme.Charge == 3 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.4f }, Main.player[Projectile.owner].Center);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < (Annihilate ? 6 : 3); i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsBeamSword.IonBright : GsBeamSword.IonViolet,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //光屑散场
            for (int i = 0; i < (Annihilate ? 8 : 4); i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.1f),
                    Main.rand.NextBool() ? GsBeamSword.IonBright : GsBeamSword.IonBlue,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(12, 0.65f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            float rot = Projectile.rotation;
            Vector2 fwd = rot.ToRotationVector2();
            float grow = Life <= 3f
                ? 1.12f * (Life / 3f)
                : MathHelper.Lerp(1.12f, 1f, MathHelper.Clamp((Life - 3f) / 5f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float thin = MathHelper.Lerp(1f, 0.62f, MathHelper.Clamp(Life / 46f, 0f, 1f));
            float sizeMul = (Annihilate ? 1.5f : 1f) * grow;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(1) * 6.28f);

            //拖尾：旧位置的淡蓝光刃残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - screen;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color trail = GsBeamSword.IonBlue * (0.13f * t * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(star, at, null, trail, rot,
                    star.Size() * 0.5f, new Vector2(0.04f, 0.2f * thin) * sizeMul * t, SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - screen;

            //光晕鞘
            Color shroud = (Annihilate ? GsBeamSword.IonViolet : GsBeamSword.IonBlue) * (0.42f * fade);
            shroud.A = 0;
            Main.EntitySpriteDraw(glow, center, null, shroud, 0f, glow.Size() * 0.5f, 0.52f * sizeMul, SpriteEffects.None, 0);

            //剑身长核线：顺速度的白蓝亮束
            Color blade = GsBeamSword.IonBright * (0.85f * fade * pulse);
            blade.A = 0;
            Main.EntitySpriteDraw(star, center + (fwd * 4f), null, blade, rot,
                star.Size() * 0.5f, new Vector2(0.05f, 0.3f * thin) * sizeMul, SpriteEffects.None, 0);

            //护手短横：偏后三成的一道短亮横，勾出光剑剪影
            Vector2 guardAt = center - (fwd * 16f * sizeMul);
            Color guard = (Annihilate ? GsBeamSword.IonViolet : GsBeamSword.IonBright) * (0.6f * fade);
            guard.A = 0;
            Main.EntitySpriteDraw(star, guardAt, null, guard, rot + MathHelper.PiOver2,
                star.Size() * 0.5f, new Vector2(0.045f, 0.1f) * sizeMul, SpriteEffects.None, 0);

            //湮光巨束：双卫星光点绕束身盘旋
            if (Annihilate) {
                for (int i = 0; i < 2; i++) {
                    float ang = Main.GlobalTimeWrappedHourly * 7f + (i * MathHelper.Pi) + SegRand(i + 20) * 6.28f;
                    Vector2 at = center + (ang.ToRotationVector2() * 20f);
                    Color sc = GsBeamSword.IonViolet * (0.5f * fade);
                    sc.A = 0;
                    Main.EntitySpriteDraw(glow, at, null, sc, 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
