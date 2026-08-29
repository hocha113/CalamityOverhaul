using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.KingSlime
{
    /// <summary>
    /// 坠击态头顶王冠：跟随所有者悬于头顶，快坠期化作碾压判定并画出锁定指引柱。<br/>
    /// 生灭由所有者端裁决(状态机Kill)，各端按同步输入本地演出；
    /// 复用 <see cref="EffectLoader.BKSCrownFX"/> 的 GuideTech/HaloTech 与王冠gore贴图
    /// </summary>
    internal class FallenCrownProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>本地帧龄(实体化演出)</summary>
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>本地坠深积分px(演出强度用，判定不读它)</summary>
        private ref float LocalFallPx => ref Projectile.localAI[1];

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>快坠中(各端从同步输入自算)</summary>
        private bool FastDiving => Owner.controlDown && Owner.velocity.Y > 2f;

        /// <summary>下坠够快(含自然坠击)，碾压与残影共用门</summary>
        private bool FallingHard => Owner.velocity.Y > 5f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.netImportant = true;
        }

        /// <summary>只有下坠够快时才有碾压伤害</summary>
        public override bool? CanDamage() => FallingHard ? null : false;

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //所有者端权威生灭与实时碾压伤害(命中在所有者端结算，远端数值无关紧要)
            if (Projectile.owner == Main.myPlayer) {
                FallenKingsCrownPlayer mp = owner.GetModPlayer<FallenKingsCrownPlayer>();
                if (!mp.DiveArmed || !mp.Equipped) {
                    Projectile.Kill();
                    return;
                }
                //碾压吃玩家总伤加成，计伤深度封顶40格(30~90基伤)
                float hDmg = MathF.Min(mp.FallTiles, FallenKingsCrownPlayer.DamageCapTiles);
                Projectile.damage = Math.Max(
                    (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(30f + 1.5f * hDmg), 1);
            }

            Projectile.timeLeft = 90;
            Age++;

            //本地坠深积分：远端演出强度的近似来源
            if (owner.velocity.Y > 0f) {
                LocalFallPx += owner.velocity.Y;
            }
            else if (owner.velocity.Y == 0f) {
                LocalFallPx = 0f;
            }

            //首帧实体化：金鸣+金屑+泡沫(各端本地)
            if (Age == 1f && !VaultUtils.isServer) {
                KingSlimeGelFX.CrownChime(Projectile.Center, 0.35f, 0.9f);
                KingSlimeGelFX.GoldGlint(Projectile.Center, 10, 5f);
                KingSlimeGelFX.BubbleFizz(Projectile.Center, 26f, 3);
            }

            //悬停跟随：快坠期压近头顶，平飘期慢呼吸
            bool fast = FastDiving;
            float bob = fast ? 0f : MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + Projectile.whoAmI * 0.7f) * 4f;
            float hover = fast ? -20f : -30f;
            Projectile.Center = owner.Top + new Vector2(0f, hover + bob);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.rotation.AngleLerp(owner.velocity.X * 0.02f, 0.25f);

            if (!VaultUtils.isServer) {
                //金屑垂滴：王权微光常驻
                if (Age % 6f == 0f && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), 4f),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.4f)),
                        KingSlimeGelFX.CrownGold, Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(Main.rand.Next(14, 24), true);
                }
                //快坠气流：金线相对上掠
                if (fast && Main.GameUpdateCount % 2 == 0) {
                    PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                        Projectile.Center + Main.rand.NextVector2Circular(24f, 10f),
                        new Vector2(0f, -Main.rand.NextFloat(3f, 7f)),
                        KingSlimeGelFX.CrownGold * 0.8f, Main.rand.NextFloat(0.8f, 1.4f))
                        ?.Configure(Main.rand.Next(8, 14));
                }
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * 0.6f);
        }

        /// <summary>碾压判定盒：王冠自身+膨胀后的玩家身躯(王的质量)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle body = Owner.Hitbox;
            body.Inflate(26, 30);
            return projHitbox.Intersects(targetHitbox) || body.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            KingSlimeGelFX.SquishSound(target.Center, -0.2f, 0.7f);
            KingSlimeGelFX.GelSplatter(target.Center, -Owner.velocity.SafeNormalize(Vector2.UnitY), 6, 6f);
            KingSlimeGelFX.GoldGlint(target.Center, 6, 5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadGore(GoreID.KingSlimeCrown);
            Texture2D crown = TextureAssets.Gore[GoreID.KingSlimeCrown].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crown.Size() * 0.5f;
            bool fast = FastDiving;

            //结算冷却期间王冠压暗、光环停呼吸：可见充能(冷却仅所有者端计时，远端不暗)
            bool cooling = Projectile.owner == Main.myPlayer
                && Owner.GetModPlayer<FallenKingsCrownPlayer>().SlamCooldown > 0;
            float dim = cooling ? 0.5f : 1f;

            //实体化包络：快速撑起带一点过冲回落
            float t = MathHelper.Clamp(Age / 12f, 0f, 1f);
            float settle = MathHelper.Clamp((Age - 12f) / 6f, 0f, 1f);
            float scale = MathHelper.Lerp(0.4f, 1.14f, VaultUtils.EaseOutCubic(t));
            scale = MathHelper.Lerp(scale, 1f, settle);

            //王权光辉(HaloTech)与锁定指引柱(GuideTech)，皆为解析成形加色quad
            Effect fx = EffectLoader.BKSCrownFX?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx != null && noise != null) {
                if (!cooling) {
                    DrawHalo(fx, noise, fast, t);
                }
                DrawGuideColumn(fx, noise, fast);
            }

            //快坠残影：向上拖出的金冠虚像
            if (FallingHard) {
                for (int i = 1; i <= 4; i++) {
                    Vector2 ghost = pos - new Vector2(0f, Owner.velocity.Y * i * 0.7f);
                    Main.EntitySpriteDraw(crown, ghost, null,
                        KingSlimeGelFX.CrownGold with { A = 0 } * (0.3f - i * 0.06f),
                        Projectile.rotation, origin, scale, SpriteEffects.None, 0);
                }
            }

            //本体+金属泽光(与Boss扣冠层同款双层画法)，冷却期整体减半压暗
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Main.EntitySpriteDraw(crown, pos, null, light * dim, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.35f * dim),
                Projectile.rotation, origin, scale * 1.03f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>王权光辉：小尺寸指挥光环贴在王冠背后，快坠期增辉</summary>
        private void DrawHalo(Effect fx, Texture2D noise, bool fast, float growT) {
            fx.CurrentTechnique = fx.Techniques["HaloTech"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //identity跨端一致，噪声相位不因槽位漂移
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            fx.Parameters["uOpacity"]?.SetValue((fast ? 0.68f : 0.42f) * growT);
            fx.Parameters["uProg"]?.SetValue(MathHelper.Clamp(Age / 14f, 0f, 1f));
            fx.Parameters["uLock"]?.SetValue(0f);

            float half = fast ? 104f : 88f;
            Vector2 c = Projectile.Center;
            DrawFXQuad(fx, noise,
                c + new Vector2(-half, -half), c + new Vector2(half, -half),
                c + new Vector2(-half, half), c + new Vector2(half, half));
        }

        /// <summary>
        /// 锁定指引柱：从王冠到脚下地面，快坠期并拢提亮(uLock=1)——
        /// 把Boss天坠预告反过来变成玩家自己的落点承诺
        /// </summary>
        private void DrawGuideColumn(Effect fx, Texture2D noise, bool fast) {
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(Owner.Bottom, 90);
            float height = ground.Y - Projectile.Center.Y;
            if (height < 60f) {
                return;
            }

            fx.CurrentTechnique = fx.Techniques["GuideTech"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            fx.Parameters["uOpacity"]?.SetValue(fast ? 0.75f : 0.4f);
            fx.Parameters["uProg"]?.SetValue(MathHelper.Clamp(LocalFallPx / 16f / 40f, 0f, 1f));
            fx.Parameters["uLock"]?.SetValue(fast ? 1f : 0f);

            float halfW = fast ? 84f : 58f;
            Vector2 top = new Vector2(Owner.Center.X, Projectile.Center.Y);
            Vector2 bottom = new Vector2(Owner.Center.X, ground.Y + 14f);
            DrawFXQuad(fx, noise,
                new Vector2(top.X - halfW, top.Y), new Vector2(top.X + halfW, top.Y),
                new Vector2(bottom.X - halfW, bottom.Y), new Vector2(bottom.X + halfW, bottom.Y));
        }

        /// <summary>顶点quad提交：Additive+显式s1噪声绑定，设备状态自管(与Boss王冠弹幕同合同)</summary>
        private static void DrawFXQuad(Effect fx, Texture2D noise,
            Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br) {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(tl.ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(tr.ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(bl.ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(br.ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }
}
