// ============================================================================
//SerpentTrail.fx 神圣之蛇拖尾
//Trail 条带蛇鳞+圣光；UV.x 沿身 UV.y 径向；ps_3_0/vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;         //整体透明度
float glowIntensity;     //发光强度(攻击时增强)

//颜色参数
float3 holyGold;         //神圣金(主色)
float3 scaleGreen;       //蛇鳞绿(尾部)
float3 pureWhite;        //纯净白(高光)
float3 mysticPurple;     //神秘紫(眼/装饰)

//噪声纹理(s1)
texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

#define PI  3.14159265
#define TAU 6.28318530

//SerpentBody — 蛇身主体(金琉璃圣蛇：发光半实体，鳞格清晰，圣光沿身流转)
float4 SerpentBodyPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          //0=尾 1=头
    float cross_ = uv.y;
    float crossDist = abs(cross_ - 0.5) * 2.0; //0=中心 1=边缘

    //=
    //1. 圆柱体底色：中脊受光，两侧收暗；整体亮度按主体读而非暗带
    //=
    float cylinder = 1.0 - crossDist * crossDist;
    float shade = 0.42 + cylinder * 0.58;
    float3 baseColor = lerp(scaleGreen * 0.85, holyGold, smoothstep(0.0, 0.85, along));
    float3 col = baseColor * shade * 0.75;

    //=
    //2. 鳞片菱格(交错排列)：暗缝分格 + 鳞心亮面 + 上游反光弧
    //=
    float scaleU = along * 20.0;
    float scaleV = cross_ * 3.0;
    float row = floor(scaleV);
    float cellU = frac(scaleU + fmod(row, 2.0) * 0.5);
    float cellV = frac(scaleV);
    float diamond = abs(cellU - 0.5) + abs(cellV - 0.5);

    float scaleGap = smoothstep(0.36, 0.5, diamond);
    col *= 1.0 - scaleGap * 0.55;

    float scaleBody = saturate(1.0 - diamond * 2.0);
    col += lerp(scaleGreen, holyGold, along) * scaleBody * 0.32 * shade;

    float scaleTopEdge = smoothstep(0.33, 0.40, diamond) * smoothstep(0.5, 0.42, diamond);
    col += pureWhite * scaleTopEdge * smoothstep(0.5, 0.25, cellU) * 0.35;

    //=
    //3. 脊线白热 + 边缘轮缘光
    //=
    float spine = exp(-crossDist * crossDist * 60.0);
    col += lerp(holyGold, pureWhite, along) * spine * 0.3;

    float rim = smoothstep(0.55, 0.92, crossDist);
    float rimPulse = 0.75 + 0.25 * sin(uTime * 2.0 + along * 6.0);
    col += holyGold * rim * rimPulse * 0.35;

    //=
    //4. 圣光行波：两道错相错频的亮波沿身推向头部
    //=
    float wave1 = pow(saturate(sin((along - uTime * 0.55) * TAU * 2.0) * 0.5 + 0.5), 5.0);
    float wave2 = pow(saturate(sin((along - uTime * 0.34) * TAU * 3.0 + 2.1) * 0.5 + 0.5), 7.0);
    col += pureWhite * wave1 * 0.22 * cylinder;
    col += holyGold * wave2 * 0.14 * cylinder;

    //=
    //5. 头部渐亮
    //=
    col += holyGold * smoothstep(0.78, 1.0, along) * 0.2 * cylinder;

    //=
    //端部收口：尾尖淡出、横向羽化
    //=
    float crossFade = smoothstep(1.0, 0.8, crossDist);
    float tailFade = smoothstep(0.0, 0.12, along);

    col *= crossFade * tailFade * fadeAlpha * glowIntensity;

    float alpha = crossFade * tailFade * fadeAlpha * (0.72 + 0.28 * cylinder);
    return float4(col, alpha);
}

//SerpentHead — 蛇头(俯视长水滴轮廓，鼻吻朝+X；脑后光环 + 吻前十字圣徽)
float4 SerpentHeadPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 c = uv - 0.5;
    float dist = length(c);
    float ang = atan2(c.y, c.x);

    float3 col = 0;

    //=
    //1. 蛇首轮廓：前尖后圆的长水滴，颈侧(cos2θ)微收出眼后轮廓
    //=
    float headShape = 0.155 + 0.115 * cos(ang) - 0.035 * cos(ang * 2.0);
    float headMask = smoothstep(headShape + 0.015, headShape - 0.012, dist);
    float headEdge = smoothstep(headShape - 0.04, headShape - 0.008, dist) * headMask;

    //头体：金琉璃底，圆顶明暗
    float dome = 1.0 - saturate(dist / max(headShape, 0.001));
    col += lerp(holyGold * 0.55, holyGold, dome) * headMask * 0.85;
    //鼻吻白热(朝行进方向)
    col += pureWhite * smoothstep(0.02, 0.15, c.x) * headMask * 0.3;
    //轮缘受光
    col += pureWhite * headEdge * 0.45;

    //=
    //2. 头面鳞格暗示(细缝，不抢主体)
    //=
    float headScaleU = (c.x + 0.2) * 12.0;
    float headScaleV = c.y * 8.0;
    float hRow = floor(headScaleV);
    float hDiamond = abs(frac(headScaleU + fmod(hRow, 2.0) * 0.5) - 0.5) + abs(frac(headScaleV) - 0.5);
    col -= holyGold * smoothstep(0.42, 0.5, hDiamond) * headMask * 0.18;

    //=
    //3. 双目(俯视对称)：秘紫眼辉 + 白瞳，眼窝一线压暗
    //=
    float2 eyeOffset = float2(0.045, 0.062);
    float eye1Dist = length(c - eyeOffset);
    float eye2Dist = length(c - float2(eyeOffset.x, -eyeOffset.y));

    float socket = exp(-eye1Dist * eye1Dist * 1600.0) + exp(-eye2Dist * eye2Dist * 1600.0);
    col -= holyGold * socket * 0.25 * headMask;

    float eyeGlow = exp(-eye1Dist * eye1Dist * 2600.0) + exp(-eye2Dist * eye2Dist * 2600.0);
    col += mysticPurple * eyeGlow * 1.5;
    float pupil = exp(-eye1Dist * eye1Dist * 9000.0) + exp(-eye2Dist * eye2Dist * 9000.0);
    col += pureWhite * pupil * 0.85;

    //=
    //4. 脑后神性光环：细环托在颈后，呼吸明灭
    //=
    float2 haloC = c + float2(0.115, 0.0);
    float haloDelta = length(haloC) - 0.205;
    float halo = exp(-haloDelta * haloDelta * 2600.0);
    float haloPulse = 0.7 + 0.3 * sin(uTime * 2.6);
    col += holyGold * halo * (1.0 - headMask * 0.75) * haloPulse * 0.55;

    //=
    //5. 吻前十字圣徽：悬浮在行进方向前方的小拉丁十字
    //=
    float2 crownC = c - float2(0.315, 0.0);
    float crossH = exp(-crownC.y * crownC.y * 9000.0) * smoothstep(0.075, 0.02, abs(crownC.x));
    float crossV = exp(-crownC.x * crownC.x * 12000.0) * smoothstep(0.055, 0.012, abs(crownC.y));
    float crownPulse = 0.75 + 0.25 * sin(uTime * 3.4 + 1.2);
    float crown = (crossH + crossV * 1.2) * crownPulse;
    col += lerp(holyGold, pureWhite, 0.4) * crown * 0.65;

    //=
    //整体衰减 + 画布边界保险
    //=
    col *= fadeAlpha * glowIntensity;
    col *= 1.0 - smoothstep(0.44, 0.5, dist);

    float alpha = saturate(headMask * 0.95 + halo * 0.45 + crown * 0.5 + eyeGlow * 0.4) * fadeAlpha;
    return float4(col, alpha);
}

//Technique定义

technique SerpentBody
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 SerpentBodyPS();
    }
};

technique SerpentHead
{
    pass P0
    {
        PixelShader = compile ps_3_0 SerpentHeadPS();
    }
};
