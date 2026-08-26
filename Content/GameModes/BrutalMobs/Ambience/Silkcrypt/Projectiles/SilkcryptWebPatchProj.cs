using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt.Projectiles
{
    /// <summary>
    /// 黏络网斑（场地实体，恒零伤害）。ai[0]=贴附面(0地面/1天花/2左壁/3右壁) ai[1]=视觉种子。
    /// 织成 45 帧（成形前无效）→ 就绪存续 → 自然老化消隐；玩家触碰吃短暂原版
    /// Webbed（时长克制，不随档位变长）+ 挣脱丝屑，网斑随即消耗坍缩。
    /// 亮白高光泽 = 新网，与背景灰暗旧网做视觉区分；自然过期时褪灰融入背景。
    /// 触碰判定各端对全部玩家对称推演（输入是同步的玩家位置），减益只由本机玩家给自己挂
    /// </summary>
    internal class SilkcryptWebPatchProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WebSpit;

        /// <summary>织网成形帧数（成形前无判定）</summary>
        private const int FormFrames = 45;
        /// <summary>就绪存续帧数</summary>
        private const int ActiveFrames = 3000;
        /// <summary>自然老化消隐帧数</summary>
        private const int FadeFrames = 90;
        /// <summary>被消耗后的坍缩帧数</summary>
        private const int CollapseFrames = 16;
        /// <summary>触碰判定半径（像素，可见网斑 = 判定网斑）</summary>
        private const float TouchRadius = 26f;
        /// <summary>Webbed 时长（0.66 秒，残酷不等于折磨；档位不加长）</summary>
        private const int WebbedFrames = 40;

        private int Side => (int)Projectile.ai[0];
        private int Seed => (int)Projectile.ai[1];
        private int TotalLife => FormFrames + ActiveFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>被踩中后的坍缩倒计时（各端对称推演出的本地状态）</summary>
        private bool Consumed {
            get => Projectile.localAI[1] > 0f;
            set => Projectile.localAI[1] = value ? CollapseFrames : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 160;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = false;//恒零伤害，纯减益场地
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FormFrames + ActiveFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                //织网起手：很轻的丝声，新网不该敲锣打鼓
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.16f, Pitch = 0.1f, MaxInstances = 4,
                }, Projectile.Center);
            }

            //坍缩流程：踩中后各端本地走完 16 帧再移除，服务端广播的销毁是兜底
            if (Consumed) {
                Projectile.localAI[1] -= 1f;
                if (Projectile.localAI[1] <= 0f) {
                    Projectile.Kill();
                }
                return;
            }

            bool armed = elapsed >= FormFrames && elapsed < FormFrames + ActiveFrames;

            //织网期：偶发一粒上升网尘（≤0.2 粒/帧）
            if (!Main.dedServ && elapsed < FormFrames && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Web, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)),
                    140, default, 0.8f);
                dust.noGravity = true;
            }

            if (!armed || CWRWorld.HasBoss) {
                return;//Boss 在场：网斑留着看，但不黏人
            }

            //触碰：对全部玩家对称检查（玩家位置各端同步，结论近似一致；
            //误差窗口由存续上限与服务端销毁兜底自愈）
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                SilkcryptPlayer nest = player.GetModPlayer<SilkcryptPlayer>();
                if (nest.WebGraceTicks > 0) {
                    continue;//刚挣脱过的玩家有一段免黏窗口
                }
                if (!TouchBox().Intersects(player.Hitbox)) {
                    continue;
                }

                //减益只由本机玩家给自己挂（原版 AddBuff 自带同步）
                if (player.whoAmI == Main.myPlayer && !Main.dedServ) {
                    player.AddBuff(BuffID.Webbed, WebbedFrames);
                }
                nest.WebGraceTicks = SilkcryptPlayer.WebGraceFrames;
                OnTouched();
                break;
            }
        }

        private Rectangle TouchBox() {
            int r = (int)TouchRadius;
            return new Rectangle((int)Projectile.Center.X - r, (int)Projectile.Center.Y - r, r * 2, r * 2);
        }

        /// <summary>被踩中：绷断丝屑四散 + 断丝声，进入坍缩</summary>
        private void OnTouched() {
            Consumed = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item17 with {
                Volume = 0.32f, Pitch = -0.2f, MaxInstances = 4,
            }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    DustID.Web, Main.rand.NextVector2Circular(2.6f, 2f),
                    100, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        //==================== 绘制 ====================

        /// <summary>贴附面的朝向：网斑往空气一侧张开</summary>
        private float SideRotation => Side switch {
            1 => MathHelper.Pi,       //天花板：向下垂张
            2 => MathHelper.PiOver2,  //左壁：向右张
            3 => -MathHelper.PiOver2, //右壁：向左张
            _ => 0f,                  //地面：向上张
        };

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            //成形进度（缓出），坍缩/老化各自给衰减
            float form = MathHelper.Clamp(elapsed / (float)FormFrames, 0f, 1f);
            form = 1f - (1f - form) * (1f - form);
            float alpha = 1f;
            float aging = 0f;
            if (Consumed) {
                alpha = Projectile.localAI[1] / CollapseFrames;
                form *= 0.65f + 0.35f * alpha;
            }
            else if (elapsed >= FormFrames + ActiveFrames) {
                //自然老化：亮白褪灰，融进背景旧网
                aging = MathHelper.Clamp((elapsed - FormFrames - ActiveFrames) / (float)FadeFrames, 0f, 1f);
                alpha = 1f - aging;
            }
            if (alpha <= 0.01f || form <= 0.05f) {
                return false;
            }

            Texture2D wad = TextureAssets.Projectile[Type].Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float rot = SideRotation;
            //新网亮白，老化向灰暗收敛（与背景旧网的区分随寿命自然消失）
            Color fresh = Color.Lerp(new Color(236, 236, 244), new Color(150, 148, 158), aging);
            Color threadColor = fresh * (0.5f * alpha * form);

            //放射丝线：从贴附点向空气侧扇开（确定性角度，白像素拉伸）
            if (px != null) {
                for (int i = 0; i < 6; i++) {
                    float spread = (i / 5f - 0.5f) * 2.2f;
                    float ang = rot - MathHelper.PiOver2 + spread
                        + MathF.Sin(Seed * 1.7f + i * 2.3f) * 0.14f;
                    float len = (20f + 12f * MathF.Abs(MathF.Sin(Seed * 0.9f + i * 1.9f))) * form;
                    Vector2 dir = ang.ToRotationVector2();
                    Main.EntitySpriteDraw(px, center, null, threadColor, ang,
                        new Vector2(0f, 0.5f), new Vector2(len, 1.3f), SpriteEffects.None, 0);
                }
            }

            //网团：中团 + 两侧小团（实体锚），亮白吃一点环境光
            Color wadColor = Color.Lerp(lightColor, fresh, 0.75f) * (0.95f * alpha);
            Vector2 wadOrigin = wad.Size() / 2f;
            Main.EntitySpriteDraw(wad, center, null, wadColor,
                Seed * 0.7f, wadOrigin, 1.05f * form, SpriteEffects.None, 0);
            Vector2 sideOff = (rot + MathHelper.PiOver2).ToRotationVector2() * 10f * form;
            Main.EntitySpriteDraw(wad, center + sideOff, null, wadColor * 0.8f,
                Seed * 1.3f, wadOrigin, 0.7f * form, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(wad, center - sideOff, null, wadColor * 0.8f,
                Seed * 2.1f, wadOrigin, 0.66f * form, SpriteEffects.None, 0);

            //新鲜光泽：加色敷料脉动（老化即熄，是"新网"的招牌）
            float sheen = (0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Seed))
                * (1f - aging);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color sheenColor = new Color(255, 255, 255, 0) * (0.22f * alpha * form * sheen);
            Main.EntitySpriteDraw(glow, center, null, sheenColor, 0f,
                glow.Size() / 2f, new Vector2(0.62f, 0.5f) * form, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.Web, Main.rand.NextVector2Circular(1.2f, 1f),
                    130, default, 0.9f);
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
