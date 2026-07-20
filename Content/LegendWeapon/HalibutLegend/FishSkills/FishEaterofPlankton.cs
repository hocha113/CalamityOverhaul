using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishEaterofPlankton : FishSkill
    {
        public override int UnlockFishID => ItemID.EaterofPlankton;
        public override int DefaultCooldown => 60;
        public override int ResearchDuration => 60 * 22;
        /// <summary>
        /// 每次射击生成的噬魂虫数量
        /// </summary>
        private const int BitesPerShot = 1;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //检查技能是否在冷却中
            if (Cooldown > 0) {
                return null;
            }

            //每次射击生成多条噬魂虫
            for (int i = 0; i < BitesPerShot; i++) {
                //计算随机偏移角度
                float angleOffset = Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 biteVelocity = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.8f, 1.2f);

                //生成噬魂虫
                int proj = Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    biteVelocity,
                    ModContent.ProjectileType<SoulEaterBite>(),
                    (int)(damage * 0.35f + HalibutData.GetDomainLayer() * 0.1f),
                    knockback * 0.5f,
                    player.whoAmI,
                    ai0: i //个体索引
                );
            }

            //播放音效
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = -0.3f }, position);

            return null;
        }
    }

    /// <summary>
    /// 噬魂虫弹幕：腐肉环节虫，中脊线 TriangleStrip 蛇形连续体，
    /// 蠕动 = 正弦相位沿体节向尾传递的推进波（头稳尾摆），
    /// 撕咬 = 3 帧定帧 + 双颚咬合 + 肉屑滴液飞溅，死亡 = 尾向头噪声腐解
    /// </summary>
    internal class SoulEaterBite : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float BiteID => ref Projectile.ai[0];
        private ref float AIState => ref Projectile.ai[1];
        private ref float AITimer => ref Projectile.localAI[0];

        //中脊线体节
        private const int SpinePoints = 14;
        private const float SegSpacing = 6f;
        private readonly Vector2[] spine = new Vector2[SpinePoints];
        private readonly Vector2[] renderSpine = new Vector2[SpinePoints];
        private bool spineInit;

        //蠕动参数
        private float crawlPhase;
        private float wriggleAmplitude = 8f;
        private const float WaveK = 0.85f;//推进波沿体节的相位差, 波形向尾传

        //演出状态
        private float materialize;  //0..1 出生展开
        private float dissolve;     //0..1 腐解蚀散, 尾向头
        private int freezeFrames;   //撕咬定帧
        private Vector2 frozenVel;
        private float biteOpen;     //双颚张角
        private float biteHeat;     //咬击充血, 头段短暂泛肉红

        //追踪目标
        private int targetNPC = -1;
        private float homingStrength = 0f;

        //状态枚举
        private enum State
        {
            Launching,    //发射阶段
            Seeking,      //寻找目标
            Homing,       //追踪目标
            Biting        //咬击
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 5; //可穿透5个敌人
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override void AI() {
            //初始化体节:压缩出生, 靠 materialize 撑开成形
            if (!spineInit) {
                InitSpine();
            }

            //撕咬定帧:世界位置冻结, 颚咬死不放
            if (freezeFrames > 0) {
                freezeFrames--;
                if (freezeFrames == 0) {
                    Projectile.velocity = frozenVel;
                }
                crawlPhase += 0.03f;
                UpdateSpine();
                return;
            }

            AITimer++;

            //出生展开:10帧内从压缩体撑到全宽, 禁pop-in
            if (materialize < 1f) {
                materialize = Math.Min(1f, materialize + 0.1f);
            }

            //寿命末端进入腐解:尾向头蚀散, 蚀散前沿沿途剥落碎屑与滴液
            if (Projectile.timeLeft < 26) {
                dissolve = Math.Min(1f, dissolve + 1f / 26f);
                if (!Main.dedServ && Projectile.timeLeft % 2 == 0) {
                    int idx = (int)MathHelper.Clamp((1f - dissolve) * (SpinePoints - 1), 0f, SpinePoints - 1);
                    Vector2 tangent = idx > 0
                        ? (renderSpine[idx - 1] - renderSpine[idx]).SafeNormalize(Vector2.UnitX)
                        : Vector2.UnitX;
                    FishEaterofPlanktonVFX.DecaySlough(renderSpine[idx], tangent * 2f);
                }
            }

            //状态机
            State currentState = (State)AIState;
            switch (currentState) {
                case State.Launching:
                    LaunchingAI();
                    break;
                case State.Seeking:
                    SeekingAI();
                    break;
                case State.Homing:
                    HomingAI();
                    break;
                case State.Biting:
                    BitingAI();
                    break;
            }

            //推进波相位:游得越快蠕动越急
            crawlPhase += 0.14f + Projectile.velocity.Length() * 0.012f;

            //更新身体段位置（蠕动推进波）
            UpdateSpine();

            //旋转朝向速度方向
            Projectile.rotation = Projectile.velocity.ToRotation();

            //体节渗液:低频腐绿粘液滴自节间坠落
            if (!Main.dedServ && dissolve < 0.3f && Main.rand.NextBool(9)) {
                int idx = Main.rand.Next(2, SpinePoints - 2);
                FishEaterofPlanktonVFX.SegmentOoze(renderSpine[idx], Projectile.velocity);
            }

            //双颚开合:撕咬冲刺渐张, 平时微启呼吸
            float openTarget = currentState == State.Biting
                ? 0.62f
                : 0.10f + 0.05f * MathF.Sin(crawlPhase * 0.6f);
            biteOpen = MathHelper.Lerp(biteOpen, openTarget, 0.25f);
            biteHeat *= 0.9f;
        }

        private void InitSpine() {
            spineInit = true;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < SpinePoints; i++) {
                //压缩排布:出生时体节挤在一起, 游动中被拉开
                spine[i] = Projectile.Center - dir * (SegSpacing * 0.35f * i);
                renderSpine[i] = spine[i];
            }
            crawlPhase = BiteID * MathHelper.TwoPi / 3f; //不同虫子相位不同
            FishEaterofPlanktonVFX.SpawnLurch(Projectile.Center, dir);
        }

        private void LaunchingAI() {
            //出膛过冲:前4帧猛冲, 随后拖拽回落, 读作被甩出去的活物
            if (AITimer < 4) {
                Projectile.velocity *= 1.1f;
            }
            else {
                Projectile.velocity *= 0.975f;
            }

            //发射15帧后进入寻找阶段
            if (AITimer >= 15) {
                AIState = (float)State.Seeking;
                AITimer = 0;
            }
        }

        private void SeekingAI() {
            //寻找目标阶段：寻找最近的敌人
            targetNPC = -1;
            var npc = Projectile.Center.FindClosestNPC(800f);
            if (npc != null) {
                targetNPC = npc.whoAmI;
            }


            if (targetNPC != -1) {
                AIState = (float)State.Homing;
                AITimer = 0;
                homingStrength = 0.02f;

                //播放锁定音效
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
            }
            else {
                //没有目标时蠕动前进
                ApplyWriggleMotion();
            }
        }

        private void HomingAI() {
            //追踪目标阶段
            if (targetNPC < 0 || !Main.npc[targetNPC].active) {
                //目标丢失，返回寻找状态
                AIState = (float)State.Seeking;
                targetNPC = -1;
                return;
            }

            NPC target = Main.npc[targetNPC];

            //逐渐增强追踪强度
            if (homingStrength < 0.15f) {
                homingStrength += 0.005f;
            }

            //计算追踪方向
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);

            //速度逐渐加快
            if (Projectile.velocity.Length() < 25f) {
                Projectile.velocity *= 1.02f;
            }

            //应用蠕动效果
            ApplyWriggleMotion();

            //接近目标时进入咬击状态
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);
            if (distanceToTarget < 80f) {
                AIState = (float)State.Biting;
                AITimer = 0;
            }
        }

        private void BitingAI() {
            //咬击阶段：短暂的爆发加速
            if (AITimer < 10) {
                Projectile.velocity *= 1.1f;
                wriggleAmplitude = 15f; //咬击时蠕动幅度加大

                //冲刺喷发:体中段甩落腐绿粘液
                if (!Main.dedServ && AITimer % 3 == 0) {
                    FishEaterofPlanktonVFX.SegmentOoze(renderSpine[SpinePoints / 2], Projectile.velocity);
                }
            }
            else {
                //咬击后回到寻找状态
                AIState = (float)State.Seeking;
                AITimer = 0;
                targetNPC = -1;
                wriggleAmplitude = 8f;
            }
        }

        /// <summary>蠕动偏移，轨迹本身蛇行</summary>
        private void ApplyWriggleMotion() {
            //计算蠕动的垂直偏移
            float wriggleOffset = MathF.Sin(crawlPhase) * wriggleAmplitude * 0.6f;

            //将偏移应用到速度的垂直方向
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            Projectile.velocity += perpendicular * wriggleOffset;
        }

        /// <summary>身体段 tick：跟随约束链 + 推进波横向偏移</summary>
        private void UpdateSpine() {
            //头部位置
            spine[0] = Projectile.Center;

            //每个身体段跟随前一段;间距随出生展开从压缩态拉到全长, 蜷缩的虫被甩直
            float spacing = SegSpacing * (0.35f + 0.65f * materialize);
            for (int i = 1; i < SpinePoints; i++) {
                Vector2 toPrev = spine[i - 1] - spine[i];
                float dist = toPrev.Length();
                if (dist > 0.001f) {
                    spine[i] = spine[i - 1] - toPrev / dist * spacing;
                }
            }

            //推进波:相位沿体节向尾传递, 包络头稳尾摆(真实蠕虫推进而非整体正弦飘)
            renderSpine[0] = spine[0];
            for (int i = 1; i < SpinePoints; i++) {
                Vector2 tangent = (spine[i - 1] - spine[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float envelope = 0.25f + 0.75f * (i / (float)(SpinePoints - 1));
                float wave = MathF.Sin(crawlPhase - i * WaveK) * (3.2f + wriggleAmplitude * 0.28f) * envelope;
                renderSpine[i] = spine[i] + normal * wave * materialize;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中效果：咬击音效分层(肉击+湿咬)
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);

            //撕咬定帧:3帧咬死不放, 颚瞬间咬合
            freezeFrames = 3;
            frozenVel = Projectile.velocity;
            Projectile.velocity = Vector2.Zero;
            biteOpen = 0.03f;
            biteHeat = 1f;

            //撕肉:咬向滴液锥+肉屑块+腐肉屑底噪, 量与初速∝动能
            float ke = MathHelper.Clamp(frozenVel.Length() / 22f, 0f, 1f);
            Vector2 dir = frozenVel.SafeNormalize(Vector2.UnitX);
            FishEaterofPlanktonVFX.BiteBurst(Projectile.Center + dir * 8f, dir, ke);

            //击中后穿透继续寻找下一个目标
            if (Projectile.penetrate > 1) {
                AIState = (float)State.Seeking;
                targetNPC = -1;
                AITimer = 0;
                wriggleAmplitude = 8f;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spineInit) {
                return;
            }
            //虫体崩解:碎屑与滴液活得比本体久(aftermath)
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.18f, Pitch = -0.55f }, Projectile.Center);
            Vector2 drift = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                int idx = (int)(i / 3f * (SpinePoints - 1));
                FishEaterofPlanktonVFX.DecaySlough(renderSpine[idx], drift);
            }
        }

        //本体全部由条带与颚层承担, 实体pass不画
        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>虫体条带：沿中脊线的 TriangleStrip，纺锤形体宽，顶点色乘环境光</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !spineInit || materialize <= 0.02f) {
                return;
            }
            Effect fx = FishEaterofPlanktonAssets.FishPlanktonWorm;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            float expand = 1f - MathF.Pow(1f - materialize, 2.2f);
            var verts = new VertexPositionColorTexture[SpinePoints * 2];
            for (int i = 0; i < SpinePoints; i++) {
                float t = i / (float)(SpinePoints - 1);
                Vector2 tangent = i < SpinePoints - 1
                    ? (renderSpine[i] - renderSpine[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (renderSpine[i - 1] - renderSpine[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                //纺锤形体宽:头钝, 前1/4最宽, 尾收尖
                float width = 7.5f * expand * MathHelper.Lerp(0.62f, 1f, MathHelper.Clamp(t / 0.22f, 0f, 1f))
                    * MathF.Pow(1f - t, 1.25f) * Projectile.scale;
                Color light = Lighting.GetColor(renderSpine[i].ToTileCoordinates());
                verts[i * 2] = new VertexPositionColorTexture((renderSpine[i] + normal * width).ToVector3()
                    , light, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((renderSpine[i] - normal * width).ToVector3()
                    , light, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.37f % 1f);
            fx.Parameters["uCrawlPhase"]?.SetValue(crawlPhase);
            fx.Parameters["uDissolve"]?.SetValue(dissolve);
            fx.Parameters["uBiteHeat"]?.SetValue(biteHeat);
            fx.Parameters["uFade"]?.SetValue(Math.Min(1f, materialize * 2f));
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        /// <summary>头端细节：双颚开合 + 头壳拱片 + 极小湿光点，全部乘环境光</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ || !spineInit || materialize <= 0.05f || dissolve > 0.9f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 head = renderSpine[0];
            Vector2 dir = (renderSpine[0] - renderSpine[1]).SafeNormalize(Vector2.UnitX);
            float headRot = dir.ToRotation();
            Vector2 pos = head - Main.screenPosition;
            Color light = Lighting.GetColor(head.ToTileCoordinates());
            float fade = materialize * (1f - MathHelper.Clamp((dissolve - 0.7f) / 0.2f, 0f, 1f));

            Color jawDark = FishEaterofPlanktonVFX.RotDark.MultiplyRGB(light) * fade;
            Color jawFlesh = Color.Lerp(FishEaterofPlanktonVFX.FleshPink, FishEaterofPlanktonVFX.RotGreen, 0.25f)
                .MultiplyRGB(light) * fade;

            //双颚:自头端向前张合, 咬击定帧时咬死闭合
            for (int s = -1; s <= 1; s += 2) {
                float jawRot = headRot + biteOpen * s;
                Vector2 jawRoot = pos + dir * 1.5f;
                spriteBatch.Draw(pixel, jawRoot, src, jawDark, jawRot
                    , new Vector2(0f, 0.5f), new Vector2(5.2f, 2.1f), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, jawRoot, src, jawFlesh * 0.9f, jawRot + 0.06f * s
                    , new Vector2(0f, 0.5f), new Vector2(3.4f, 1.1f), SpriteEffects.None, 0f);
            }

            //头壳:横跨头端的暗腐绿拱片, 压住条带截断面
            spriteBatch.Draw(pixel, pos, src
                , Color.Lerp(FishEaterofPlanktonVFX.RotGreen, FishEaterofPlanktonVFX.RotDark, 0.45f).MultiplyRGB(light) * fade
                , headRot, new Vector2(0.5f, 0.5f), new Vector2(4.6f, 7.2f), SpriteEffects.None, 0f);
            //湿光点:头背极小面积, 非纯白
            spriteBatch.Draw(pixel, pos - dir * 1.5f, src
                , (FishEaterofPlanktonVFX.WetPale with { A = 0 }) * (0.30f * fade)
                , headRot, new Vector2(0.5f, 0.5f), new Vector2(1.8f, 1.2f), SpriteEffects.None, 0f);
        }
    }
}
