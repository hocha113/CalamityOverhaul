// ============================================================================
//ElysiumStaff.fx 天国极乐权杖特效
//采样 s0 VaultAsset.placeholder2 + s1 噪声，全程序化；AlphaBlend
//ps_3_0
// ============================================================================

float uTime;
float fadeAlpha;
float3 warmGold;         //暖金色调
float3 brightGold;       //亮金高光
float3 holyWhite;        //圣白

float chargeRatio;       //0~1 蓄力进度
float auraRotation;      //神圣光辉缓慢旋转角

float ringProgress;      //0刚生成 1消散
float3 ringColor;        //扩散环基本色
float ringRotation;      //扩散环旋转角

float burstProgress;     //0刚释放 1完全消散
float burstIntensity;    //爆发强度(受蓄力影响)

float waveRadius;        //化蛇波前锋半径(UV 0~0.5)，与判定环带同源
float waveFade;          //化蛇波整体透明度
float waveGrow;          //化蛇波扩张进度 0~1，控制前锋软化与拖裙加宽

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture   = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

sampler baseSamp : register(s0);

#define PI  3.14159265
#define TAU 6.28318530

float softRing(float d, float r, float sharp)
{
    float delta = d - r;
    return exp(-sharp * delta * delta);
}

struct PSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

//DivineAura 蓄力神圣光辉
float4 DivineAuraPS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords;
    float2 c   = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;
    float  ch  = chargeRatio;

    //=
    //1. 多层径向基础辉光
    //=
    //紧凑暖光核
    col += warmGold * exp(-dist * dist * 18.0) * 0.35 * ch;
    //广域微光
    col += warmGold * exp(-dist * dist * 5.0) * 0.08 * ch;

    //=
    //2. 炽白核心(双层)
    //=
    col += holyWhite  * exp(-dist * dist * 280.0) * ch;
    col += brightGold * exp(-dist * dist * 80.0) * 0.4 * ch;

    //=
    //3. 十字架神光(主十字+副十字)
    //=
    float cosR = cos(auraRotation);
    float sinR = sin(auraRotation);
    float2 rc  = float2(c.x * cosR - c.y * sinR,
                        c.x * sinR + c.y * cosR);

    //主十字，垂直光束略窄(拉丁十字比例)
    float beamH     = exp(-rc.y * rc.y * 500.0);
    float beamV     = exp(-rc.x * rc.x * 700.0);
    float crossFade = exp(-dist * 5.0);
    col += brightGold * (beamH + beamV * 1.3) * crossFade * 0.55 * ch;

    //副十字(45度，较弱较窄)
    float cos45 = 0.7071;
    float2 r45 = float2(rc.x * cos45 + rc.y * cos45,
                        -rc.x * cos45 + rc.y * cos45);
    float diagBeam = exp(-r45.y * r45.y * 1000.0)
                   + exp(-r45.x * r45.x * 1000.0);
    col += warmGold * diagBeam * exp(-dist * 7.0) * 0.2 * ch;

    //=
    //4. 同心光环(随蓄力逐步显现)
    //=
    //第一环
    float r1A = smoothstep(0.2, 0.35, ch);
    float r1R = 0.08 + ch * 0.08;
    float r1P = 0.8 + 0.2 * sin(uTime * 3.0);
    col += brightGold * softRing(dist, r1R, 3000.0) * r1A * r1P * 0.6;
    col += warmGold   * softRing(dist, r1R, 500.0)  * r1A * 0.2;

    //第二环
    float r2A = smoothstep(0.45, 0.6, ch);
    float r2R = 0.16 + ch * 0.06;
    float r2P = 0.75 + 0.25 * sin(uTime * 2.5 + 1.0);
    col += brightGold * softRing(dist, r2R, 2000.0) * r2A * r2P * 0.45;
    col += warmGold   * softRing(dist, r2R, 350.0)  * r2A * 0.15;

    //第三环
    float r3A = smoothstep(0.7, 0.85, ch);
    float r3R = 0.26 + ch * 0.04;
    float r3P = 0.7 + 0.3 * sin(uTime * 2.0 + 2.0);
    col += warmGold * softRing(dist, r3R, 1500.0) * r3A * r3P * 0.35;

    //=
    //5. 噪声日冕能量场
    //=
    float2 nUV1 = float2(dist * 5.0 + uTime * 0.12, ang * 0.318 + uTime * 0.08);
    float  n1   = tex2D(noiseSamp, frac(nUV1)).r;
    float2 nUV2 = float2(dist * 3.0 - uTime * 0.1, ang * 0.637 + 0.5);
    float  n2   = tex2D(noiseSamp, frac(nUV2)).g;
    float  nBl  = n1 * 0.6 + n2 * 0.4;

    float coronaMask = exp(-dist * dist * 10.0) * ch;
    col += lerp(warmGold, holyWhite, nBl * 0.5) * nBl * coronaMask * 0.35;

    //=
    //6. 神圣曼陀罗纹饰(高蓄力阶段)
    //=
    float mA = smoothstep(0.55, 0.75, ch);
    if (mA > 0.01)
    {
        float mR = 0.18 + ch * 0.04;

        //主纹环
        col += brightGold * softRing(dist, mR, 6000.0) * mA * 0.35;

        //12瓣花纹(正向旋转)
        float s12  = frac((ang + auraRotation * 0.7) / TAU * 12.0);
        float petal = exp(-(s12 - 0.5) * (s12 - 0.5) * 120.0);
        float petalR = mR + petal * 0.04;
        col += brightGold * softRing(dist, petalR, 4000.0) * mA * 0.25;

        //6瓣内层(反向旋转, 更精致)
        float s6 = frac((ang - auraRotation) / TAU * 6.0);
        float ip = exp(-(s6 - 0.5) * (s6 - 0.5) * 80.0);
        float iR = mR * 0.6 + ip * 0.03;
        col += warmGold * softRing(dist, iR, 5000.0) * mA * 0.2;
    }

    //=
    //7. 微光闪烁粒子
    //=
    float2 shimUV = float2(ang * 2.5 + uTime * 0.05, dist * 15.0 - uTime * 0.1);
    float  shim   = pow(tex2D(noiseSamp, frac(shimUV)).b, 8.0);
    float  shimM  = smoothstep(0.42, 0.08, dist) * smoothstep(0.01, 0.05, dist);
    col += holyWhite * shim * shimM * 0.2 * ch;

    //=
    //边缘衰减
    //=
    col *= (1.0 - smoothstep(0.38, 0.5, dist)) * fadeAlpha;

    return float4(col, 1.0);
}

//SacredRing 神圣扩散环
float4 SacredRingPS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords;
    float2 c   = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;
    float  t   = ringProgress;
    //二次衰减，让环在前期保持更久的可见时间
    float  fade = (1.0 - t);
    fade *= fade;

    //环的UV空间半径(从核心向外扩张)
    float rPos = 0.08 + t * 0.34;

    //=
    //1. 主环线(锐利+中层辉光+远层微光)
    //=
    col += ringColor * softRing(dist, rPos, 4000.0) * 0.9;
    col += ringColor * softRing(dist, rPos, 300.0)  * 0.35;
    col += ringColor * softRing(dist, rPos, 60.0)   * 0.1;

    //=
    //2. 四方十字标记
    //=
    float cAng    = ang - ringRotation;
    float s4      = frac(cAng / TAU * 4.0 + 0.5);
    float s4d     = abs(s4 - 0.5) * 2.0;
    float crossMk = exp(-s4d * s4d * 300.0);

    //十字标记处的宽域辉光
    col += holyWhite * crossMk * softRing(dist, rPos, 600.0) * 0.5;

    //径向延伸(向外的精细短线)
    float radExt = smoothstep(rPos - 0.01, rPos, dist)
                 * smoothstep(rPos + 0.055, rPos + 0.005, dist);
    col += brightGold * crossMk * radExt * 0.6;

    //=
    //3. 12点装饰(纤细珠列)
    //=
    float s12  = frac(cAng / TAU * 12.0 + 0.5);
    float s12d = abs(s12 - 0.5) * 2.0;
    float fili = exp(-s12d * s12d * 500.0);
    col += warmGold * fili * softRing(dist, rPos, 2000.0) * 0.3;

    //=
    //4. 内侧伴线 + 外侧伴线
    //=
    col += warmGold * softRing(dist, rPos - 0.018, 8000.0) * 0.2;
    col += warmGold * softRing(dist, rPos + 0.012, 8000.0) * 0.15;

    //=
    //寿命衰减 + 外部裁切
    //=
    col *= fade * fadeAlpha;
    col *= 1.0 - smoothstep(0.44, 0.5, dist);

    return float4(col, 1.0);
}

//DivineBurst 释放攻击爆发
float4 DivineBurstPS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords;
    float2 c   = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;
    float  t   = burstProgress;
    float  inv = 1.0 - t;
    float  intensity = burstIntensity;

    //总体衰减曲线：前期猛烈，后期迅速消散
    float masterFade = inv * inv * inv;

    //=
    //1. 中心微光点(克制的核心，不做大光球)
    //=
    //极小的亮点，仅在最初一瞬可见
    float coreSize = 600.0 + t * 2000.0;
    float core = exp(-dist * dist * coreSize) * masterFade;
    col += holyWhite * core * 0.35 * intensity;

    //微弱暖色底光(营造温度感，不喧宾夺主)
    float warmCore = exp(-dist * dist * (200.0 + t * 300.0)) * masterFade;
    col += warmGold * warmCore * 0.12 * intensity;

    //=
    //2. 十字架光柱(主体视觉焦点)
    //=
    float rot = auraRotation + t * 0.5;
    float cosR = cos(rot);
    float sinR = sin(rot);
    float2 rc = float2(c.x * cosR - c.y * sinR,
                       c.x * sinR + c.y * cosR);

    //光柱宽度：保持纤细锐利，能清晰辨认十字形
    float beamSharp = 800.0 + t * 600.0;
    float beamH = exp(-rc.y * rc.y * beamSharp);
    float beamV = exp(-rc.x * rc.x * (beamSharp * 1.4));

    //拉丁十字：竖臂比横臂长(竖臂衰减更慢)
    float beamRangeH = exp(-dist * (3.0 + t * 6.0));
    float beamRangeV = exp(-dist * (2.0 + t * 5.0));
    col += brightGold * beamH * beamRangeH * 0.65 * masterFade * intensity;
    col += brightGold * beamV * beamRangeV * 0.8  * masterFade * intensity;

    //十字架轮廓加强线(更锐利的内核线)
    float innerBeamH = exp(-rc.y * rc.y * (beamSharp * 4.0));
    float innerBeamV = exp(-rc.x * rc.x * (beamSharp * 5.0));
    col += holyWhite * innerBeamH * beamRangeH * 0.25 * masterFade * intensity;
    col += holyWhite * innerBeamV * beamRangeV * 0.3  * masterFade * intensity;

    //副十字(45度，辅助宗教元素)
    float cos45 = 0.7071;
    float2 r45 = float2(rc.x * cos45 + rc.y * cos45,
                        -rc.x * cos45 + rc.y * cos45);
    float diagBeam = exp(-r45.y * r45.y * (beamSharp * 2.0))
                   + exp(-r45.x * r45.x * (beamSharp * 2.0));
    col += warmGold * diagBeam * exp(-dist * (4.5 + t * 8.0)) * 0.25 * masterFade * intensity;

    //=
    //3. 冲击波光环(由内向外扩张)
    //=
    //主冲击环
    float waveR1 = t * 0.42;
    float w1 = softRing(dist, waveR1, 4000.0 + t * 2000.0);
    float w1glow = softRing(dist, waveR1, 300.0);
    col += brightGold * w1     * 0.5 * inv * intensity;
    col += warmGold   * w1glow * 0.15 * inv * intensity;

    //第二冲击环(稍延迟)
    float t2 = saturate(t * 1.4 - 0.15);
    float waveR2 = t2 * 0.38;
    float w2 = softRing(dist, waveR2, 3000.0 + t2 * 1500.0);
    col += warmGold * w2 * 0.3 * (1.0 - t2) * (1.0 - t2) * intensity;

    //=
    //4. 曼陀罗圣纹(宗教纹饰主体)
    //=
    //更早出现，持续更久
    float mA = smoothstep(0.0, 0.08, t) * smoothstep(0.85, 0.4, t) * intensity;
    if (mA > 0.01)
    {
        float mRot = rot * 2.0 + t * 3.0;

        //12瓣放射花纹(教堂玫瑰窗风格)
        float s12 = frac((ang + mRot) / TAU * 12.0);
        float petal12 = exp(-(s12 - 0.5) * (s12 - 0.5) * 80.0);
        float petalR = 0.06 + t * 0.22;
        float petalBand = softRing(dist, petalR, 2000.0) + softRing(dist, petalR, 300.0) * 0.4;
        col += brightGold * petal12 * petalBand * mA * 0.55;

        //外层纹环(托住花瓣)
        col += warmGold * softRing(dist, petalR, 600.0) * mA * 0.15;

        //24线精细放射丝(反向旋转，更精致)
        float s24 = frac((ang - mRot * 0.5) / TAU * 24.0);
        float fili24 = exp(-(s24 - 0.5) * (s24 - 0.5) * 400.0);
        float filiR = 0.04 + t * 0.28;
        col += warmGold * fili24 * softRing(dist, filiR, 4000.0) * mA * 0.3;

        //6瓣内层花纹(层次感)
        float s6 = frac((ang + mRot * 1.3) / TAU * 6.0);
        float inner6 = exp(-(s6 - 0.5) * (s6 - 0.5) * 60.0);
        float innerR = 0.04 + t * 0.12;
        col += brightGold * inner6 * softRing(dist, innerR, 3000.0) * mA * 0.35;

        //十字架嵌入曼陀罗(4方位加强标记)
        float s4 = frac((ang + rot) / TAU * 4.0);
        float cross4 = exp(-(s4 - 0.5) * (s4 - 0.5) * 200.0);
        col += holyWhite * cross4 * softRing(dist, petalR, 1500.0) * mA * 0.3;
    }

    //=
    //5. 噪声能量碎片飞散
    //=
    float2 nUV = float2(dist * 4.0 - t * 2.0, ang * 0.318 + uTime * 0.05);
    float  n = tex2D(noiseSamp, frac(nUV)).r;
    float  debrisMask = smoothstep(t * 0.4 - 0.05, t * 0.4, dist)
                      * smoothstep(t * 0.4 + 0.08, t * 0.4 + 0.02, dist);
    col += lerp(warmGold, holyWhite, n) * n * debrisMask * masterFade * 0.25 * intensity;

    //=
    //6. 外层余韵微光
    //=
    float afterR = t * 0.44;
    float after = exp(-(dist - afterR) * (dist - afterR) * 50.0);
    col += warmGold * after * 0.1 * inv * intensity;

    //=
    //边缘裁切
    //=
    col *= 1.0 - smoothstep(0.45, 0.5, dist);
    col *= fadeAlpha;

    return float4(col, 1.0);
}

//ConversionWave 化蛇波潮(世界空间环形波，前锋位置由 waveRadius 精确给出)
float4 ConversionWavePS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords;
    float2 c   = uv - 0.5;
    float  dist = length(c);
    float  ang  = atan2(c.y, c.x);

    float3 col = 0;
    float  r   = waveRadius;

    //绑定噪声实测值域 0.22~0.78，阈值前先归一
    //噪声取样走刚体旋转笛卡尔坐标，避免极角接缝
    float  na = uTime * 0.35;
    float  cs = cos(na);
    float  sn = sin(na);
    float2 rc = float2(c.x * cs - c.y * sn, c.x * sn + c.y * cs);
    float  nRaw = tex2D(noiseSamp, rc * 2.6 + 0.5).g;
    float  n01  = saturate((nRaw - 0.22) / 0.56);

    //=
    //1. 前锋主线：扩张中逐渐变软变宽(能量耗散)，内侧贴一道白热伴线
    //=
    float sharpMain = lerp(7000.0, 1600.0, waveGrow);
    float front = softRing(dist, r, sharpMain);
    col += brightGold * front * 0.9;
    col += holyWhite  * softRing(dist, r, sharpMain * 3.5) * 0.55;
    col += holyWhite  * softRing(dist, r - 0.012, sharpMain * 1.6) * 0.3;

    //=
    //2. 内侧能量拖裙：自前锋向内指数衰减
    //   径向光芒调制(两组整数倍角谐波错频，跨缝连续)撕出放射丝缕
    //=
    float inner = max(r - dist, 0.0);
    float skirtLen = 0.05 + waveGrow * 0.08;
    float skirt = exp(-inner / skirtLen * 2.2) * step(0.0001, inner);
    float rayPat = 0.5 + 0.28 * sin(ang * 24.0 + uTime * 1.7)
                       + 0.22 * sin(ang * 15.0 - uTime * 1.1);
    skirt *= 0.4 + 0.6 * saturate(rayPat);
    skirt *= 0.6 + 0.4 * n01;
    col += warmGold * skirt * 0.62;
    col += brightGold * skirt * skirt * 0.3;

    //=
    //3. 前锋外缘碎光：被抛在波前的能量碎屑，窄带+噪声阈值
    //=
    float outer = max(dist - r, 0.0);
    float debris = exp(-outer * 70.0) * step(0.0001, outer);
    debris *= smoothstep(0.42, 0.78, n01);
    col += holyWhite * debris * 0.9 * (1.0 - waveGrow * 0.3);

    //=
    //4. 前锋十二刻度圣徽点(整数倍角，跨缝连续)
    //=
    float s12  = frac((ang + uTime * 0.12) / TAU * 12.0);
    float s12d = abs(s12 - 0.5) * 2.0;
    float tick = exp(-s12d * s12d * 300.0);
    col += brightGold * tick * softRing(dist, r, sharpMain * 0.5) * 0.55;

    //=
    //5. 盘内残留焦散微光：波扫过的区域短暂留下圣光涟漪
    //=
    float caust = n01 * exp(-dist * 5.0) * (1.0 - waveGrow) * 0.12;
    col += warmGold * caust;

    //=
    //寿命衰减 + 画布边界保险
    //=
    col *= waveFade;
    col *= 1.0 - smoothstep(0.46, 0.5, dist);

    return float4(col, 1.0);
}

technique DivineAura
{
    pass P0
    {
        PixelShader = compile ps_3_0 DivineAuraPS();
    }
};

technique ConversionWave
{
    pass P0
    {
        PixelShader = compile ps_3_0 ConversionWavePS();
    }
};

technique SacredRing
{
    pass P0
    {
        PixelShader = compile ps_3_0 SacredRingPS();
    }
};

technique DivineBurst
{
    pass P0
    {
        PixelShader = compile ps_3_0 DivineBurstPS();
    }
};
