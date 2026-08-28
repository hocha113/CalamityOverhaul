//HalibutRestartTide.fx 比目鱼大范围重启的潮汐吞没合成
//TechTide: 潮水自屏底涌起吞没实时画面（前沿泡沫锋+上缘飞沫），
//          水下按深度做深渊吸收调色+双向正弦干涉焦散（只提亮部）+海雪悬浮，
//          倒带段逐行微幅回卷抖动、海雪逆飞上浮、焦散随回卷脉冲涌动，
//          巨型渊眼在水雾深处睁开注视、结算时阖上。白闪走 CPU 叠层。
//直线算术+平 tex2D，无动态分支；s0=实时屏幕帧 s1=PerlinNoise
//极角审计：atan2 唯一消费是 sin(14θ)（整数倍角，跨 ±π 连续），其余噪声全走平移坐标
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一

float uTime;    //秒
float uFlood;   //0-1 潮水覆盖：0=无水，1=全没
float uRewind;  //0-1 倒带强度：水下扰动/回卷抖动/海雪逆飞
float uPulse;   //0-1 当帧回卷脉冲，焦散涌动随它呼吸
float uEye;     //0-1 渊眼开度（0=闭合不存在）
float uDim;     //0-1 演出在场度：未没入部分的风暴压暗
float uSeed;    //本场种子
float uAspect;  //宽/高
float2 uEyeUv;  //渊眼中心（uv）

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

static const float3 LUMA = float3(0.299, 0.587, 0.114);
static const float3 ABSORB_SHALLOW = float3(0.55, 0.78, 1.02); //浅水吸收乘色
static const float3 ABSORB_DEEP    = float3(0.14, 0.32, 0.60); //深水吸收乘色
static const float3 SCATTER_COL    = float3(0.045, 0.150, 0.285); //水体散射加色
static const float3 CAUSTIC_COL    = float3(0.36, 0.78, 1.00); //焦散
static const float3 FOAM_COL       = float3(0.72, 0.94, 1.05); //泡沫锋
static const float3 SNOW_COL       = float3(0.55, 0.80, 1.00); //海雪
static const float3 EYE_IRIS_COL   = float3(0.30, 0.85, 1.05); //渊眼虹膜
static const float3 EYE_PUPIL_COL  = float3(0.85, 1.05, 1.15); //渊眼瞳芯

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float v) { return saturate((v - 0.227) / 0.549); }

float4 PSTide(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //====== 潮水面线：自屏底涌起，行进期带波动，漫顶后收平 ======
    float transit = saturate(uFlood * (1.0 - uFlood) * 4.0); //行进强度：来/去各一峰
    float lineWave = (tex2D(uImage1, float2(uv.x * 2.6 * uAspect + uSeed * 0.11, uTime * 0.13)).r - 0.5)
        * (0.030 * transit + 0.006);
    float ripple = (tex2D(uImage1, float2(uv.x * 9.0 * uAspect - uSeed * 0.07, uTime * 0.31)).r - 0.5)
        * (0.012 * transit + 0.003);
    //超没 1.34：漫顶后面线推出画外，全屏都算水下
    float surfaceY = 1.0 - uFlood * 1.34 + lineWave + ripple;

    float frontDist = uv.y - surfaceY;              //<0 在水上
    float under = saturate(frontDist * 26.0);       //水下掩码（窄过渡带）
    float depth = saturate(frontDist * 1.1);        //屏内深度：越靠屏底越深

    //====== 实时画面采样：水下折射晃动 + 倒带逐行回卷抖动 ======
    float2 wob;
    wob.x = tex2D(uImage1, uv * float2(3.0 * uAspect, 3.0) + float2(uTime * 0.050, uSeed)).r - 0.5;
    wob.y = tex2D(uImage1, uv * float2(3.0 * uAspect, 3.0) + float2(uSeed, uTime * 0.045)).r - 0.5;
    float rowJitter = (tex2D(uImage1, float2(uv.y * 3.1, uTime * 0.9)).r - 0.5) * 0.007 * uRewind;
    float2 liveUv = uv + wob * (0.0035 + 0.0045 * uRewind) * under;
    liveUv.x = saturate(liveUv.x + rowJitter * under);
    float3 live = tex2D(uImage0, liveUv).rgb;

    //====== 干侧风暴压暗：海要来了，天色先沉 ======
    float3 col = lerp(live,
        live * float3(0.80, 0.87, 0.97) + float3(0.0, 0.012, 0.030),
        (1.0 - under) * uDim * 0.70);

    //====== 水下吸收+散射：深渊压蓝，世界仍可读 ======
    float3 absorb = lerp(ABSORB_SHALLOW, ABSORB_DEEP, depth);
    col = lerp(col, col * absorb + SCATTER_COL * (0.35 + depth * 0.55), under);

    //====== 焦散：域扭曲后的双正弦相消丝网（裸正弦=规则网格，扭过才是水光），只提亮部 ======
    float warp = tex2D(uImage1, uv * 2.2 + float2(uTime * 0.030, uSeed * 0.07)).r;
    float2 wuv = uv * float2(uAspect, 1.0) + (warp - 0.5) * 0.17;
    float c1 = sin(wuv.x * 31.0 + wuv.y * 11.0 + uTime * 1.3 + uSeed);
    float c2 = sin(wuv.x * -19.0 + wuv.y * 26.0 - uTime * 0.9);
    float web = saturate(1.0 - abs(c1 + c2) * 0.85);
    float caust = web * web * web * web;
    float liveLuma = dot(live, LUMA);
    col += CAUSTIC_COL * caust * liveLuma * under
        * (0.16 + 0.08 * uRewind + 0.38 * uPulse * uRewind);

    //====== 海雪：两层视差细点，倒带段逆着时间上浮（低频 Perlin 出雾球，频率必须够高） ======
    float snowDir = uRewind * 2.0 - 1.0;   //-1=下沉 +1=上浮
    float sn1 = nrm(tex2D(uImage1, float2(uv.x * 20.0 * uAspect + uSeed * 0.31,
        uv.y * 20.0 + uTime * 0.050 * snowDir)).r);
    float sn2 = nrm(tex2D(uImage1, float2(uv.x * 36.0 * uAspect - uSeed * 0.17,
        uv.y * 36.0 + uTime * 0.080 * snowDir + 0.37)).r);
    float snow = smoothstep(0.87, 0.97, sn1) * 0.7 + smoothstep(0.89, 0.985, sn2) * 0.5;
    col += SNOW_COL * snow * under * (0.20 + 0.20 * uRewind);

    //====== 泡沫锋：面线窄带 + 上缘飞沫，行进期最旺；漫顶后面线出画自然消失 ======
    float foamN = nrm(tex2D(uImage1, float2(uv.x * 14.0 * uAspect + uSeed, uTime * 0.36)).r);
    float foamBand = saturate(1.0 - abs(frontDist) * 34.0);
    col += FOAM_COL * foamBand * (0.40 + 0.60 * foamN) * (0.30 + 0.95 * transit);
    float sprayN = nrm(tex2D(uImage1, float2(uv.x * 30.0 * uAspect + uSeed * 0.53,
        uv.y * 3.0 - uTime * 0.50)).r);
    float spray = saturate(1.0 - abs(frontDist + 0.035) * 22.0)
        * smoothstep(0.78, 0.97, sprayN) * transit;
    col += FOAM_COL * spray * 0.50;

    //====== 渊眼：水雾深处的注视者，uEye 驱动纵向开阖，全部件按眼眶尺寸缩放 ======
    float2 ep = (uv - uEyeUv) * float2(uAspect, 1.0);
    float eyeRx = 0.30;
    float eyeRy = eyeRx * 0.50 * max(uEye, 0.0001);
    float er = length(ep / float2(eyeRx, eyeRy));       //椭圆距离：0=中心 1=轮廓
    float lid = saturate(1.0 - abs(ep.x) / eyeRx);      //越靠眼角纵向越窄（杏仁收角）
    float inner = saturate(1.0 - er);
    float eyeAmt = uEye * under;

    //暗色巩膜体：吸光压暗背后世界，雾里巨物不是亮牌
    float scleraMask = smoothstep(0.05, 0.55, inner * (0.35 + 0.65 * lid));
    col = lerp(col, col * 0.22 + float3(0.004, 0.014, 0.030), scleraMask * 0.72 * eyeAmt);
    //睑线：轮廓一道细冷光，给眼一个可读的剪影
    float lidLine = saturate(1.0 - abs(er - 1.0) * 10.0) * lid;
    col += EYE_IRIS_COL * lidLine * 0.10 * eyeAmt;
    //虹膜宽环带：辐纹弱化，读整环不读珠子
    float irisBand = (1.0 - smoothstep(0.60, 0.80, er)) * smoothstep(0.26, 0.44, er);
    float ang = atan2(ep.y, ep.x);
    float stria = 0.85 + 0.15 * sin(ang * 14.0 + uSeed);
    col += EYE_IRIS_COL * irisBand * stria * 0.16 * eyeAmt * (0.55 + 0.45 * lid);
    //竖瞳：眼眶内含（随开阖同步压扁），缓慢注视漂移
    float2 pp = ep - float2(sin(uTime * 0.37 + uSeed) * 0.035, 0.0);
    float slitCore = saturate(1.0 - length(pp / float2(eyeRx * 0.085, eyeRy * 0.62)));
    float slitGlow = saturate(1.0 - length(pp / float2(eyeRx * 0.22, eyeRy * 0.95)));
    col += EYE_IRIS_COL * slitGlow * slitGlow * 0.22 * eyeAmt;
    col += EYE_PUPIL_COL * slitCore * slitCore * 0.75 * eyeAmt;

    return float4(col, 1.0);
}

technique TechTide
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSTide();
    }
}
