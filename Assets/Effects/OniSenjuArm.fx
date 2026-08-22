// ============================================================================
//OniSenjuArm.fx 千手：终结定格里自虚空探出的持刀鬼手
//
//ArmTech：一条自肩根长出来的墨臂，画在世界空间的弯折条带上。
//  1) 锥度不是线性：肩根粗、前臂中段鼓一点、腕口收细，直筒会读成棍子；
//  2) 肩根不封口：根部按噪声散成墨烟，读作"从虚里长出来的"而不是贴在背上；
//  3) 筋线：两条沿臂走的暗筋 + 一条绯红的迎光缘，靠明暗差立体，不靠发光；
//  4) 轮廓侵蚀：边缘用噪声啃出毛口，避免橡皮管般的完美锥体；
//  5) uReach 0→1 是探出进度，未探到的段直接不画，手是"长"出来的。
//
//HandTech：腕口那只手。四指各三骨节 + 爪尖，拇指反向合抱。
//  uGrip 0→1 时指节向掌心卷紧，读作"攥住了刀"；
//  握把方向即 quad 的 +x，故全程笛卡尔 SDF。
//
//极角审计：本文件无 atan2/极角。ArmTech 走条带 u/v 带坐标，
//  HandTech 走笛卡尔胶囊 SDF，噪声只吃 uv 与 p，无 ±π 接缝。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uReach;       //0..1 探出进度
float uGrip;        //0..1 攥紧程度
float uOpacity;

float3 uColInk;     //墨黑臂体
float3 uColRim;     //绯红迎光缘
float3 uColHot;     //纸白高光(爪尖/骨节脊)

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

#define PI 3.14159265

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//胶囊 SDF：骨节与指段的统一载体
float Capsule(float2 p, float2 a, float2 b, float r)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    return length(pa - ba * h) - r;
}

// ============================== ArmTech ==============================

float4 ArmPS(PSInput input) : COLOR0
{
    //u: 0=肩根 → 1=腕口；y: -1..1 横跨条带
    float u = saturate(input.TexCoords.x);
    float y = (input.TexCoords.y - 0.5) * 2.0;

    //探出：还没长到的段不画，且推进端略微鼓一下，像是被顶出来的
    float grow = saturate((uReach * 1.12 - u) * 6.0);
    if (grow <= 0.003)
        return float4(0, 0, 0, 0);

    //锥度：肩根粗 → 前臂中段鼓 → 腕口收
    float taper = 0.94 - 0.52 * u + 0.17 * sin(u * PI);
    //推进端的顶出感
    taper *= 1.0 + (1.0 - grow) * 0.35;

    //轮廓侵蚀：边缘啃出毛口，别做成完美锥体
    float edgeN = tex2D(noiseSamp, float2(u * 5.3 + uSeed, y * 0.42 + 0.17)).r - 0.5;
    float halfW = max(taper + edgeN * 0.11, 0.02);

    float d = abs(y) / halfW;
    float body = 1.0 - smoothstep(0.74, 1.02, d);
    if (body <= 0.004)
        return float4(0, 0, 0, 0);

    //肩根不封口：根部按噪声散成墨烟
    float rootN = tex2D(noiseSamp, float2(u * 8.1 - uSeed * 2.0, y * 0.9 + 0.63)).r;
    float rootFade = smoothstep(0.0, 0.26, u);
    float rootCut = step(rootN, 0.18 + rootFade * 0.95);

    //筋线：两条沿臂的暗筋，位置随噪声轻微游走
    float sinewN = tex2D(noiseSamp, float2(u * 2.7 + uSeed * 0.7, 0.41)).r - 0.5;
    float across = y / halfW;
    float sinew = saturate(1.0 - abs(abs(across) - (0.30 + sinewN * 0.10)) * 9.0);
    sinew += saturate(1.0 - abs(abs(across) - (0.62 + sinewN * 0.08)) * 12.0) * 0.7;

    //迎光缘：一侧压绯红，给臂一个受光方向
    float rim = saturate(1.0 - abs(across - 0.72) * 4.2);

    float3 col = uColInk;
    //筋线比臂体更暗，靠明暗差立体
    col *= 1.0 - saturate(sinew) * 0.45;
    col = lerp(col, uColRim, saturate(rim * 0.75));
    //腕口一点纸白，接上手那边的骨节脊
    col = lerp(col, uColHot, saturate(smoothstep(0.86, 1.0, u) * rim * 0.55));

    float alpha = body * rootCut * grow * uOpacity * input.Color.a;
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);
    return float4(col * alpha, alpha);
}

// ============================== HandTech ==============================

float4 HandPS(PSInput input) : COLOR0
{
    //握把方向 = +x；掌心在原点
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float grip = saturate(uGrip);

    //掌：沿握把略长的圆角块
    float cover = 0.0;
    float ridge = 0.0;
    float d = Capsule(p, float2(-0.34, 0.02), float2(0.30, -0.02), 0.30);
    cover = 1.0 - smoothstep(0.0, 0.055, d);

    //四指：沿握把铺开，各三骨节 + 爪尖；uGrip 越大越向掌心卷
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float fi = (float)i;
        float spread = -0.30 + fi * 0.24;
        //每指各有相位，别让四指同形
        float wob = sin(fi * 2.1 + uSeed * 6.28) * 0.035;
        float curl = grip * (0.62 + fi * 0.05);

        //三骨节自掌缘向外，逐节变细并逐节多卷一点
        float2 j0 = float2(spread, 0.16 + wob);
        float2 j1 = j0 + float2(-0.06 * curl, 0.30 - 0.10 * curl);
        float2 j2 = j1 + float2(-0.13 * curl, 0.24 - 0.12 * curl);
        float2 tip = j2 + float2(-0.17 * curl, 0.17 - 0.11 * curl);

        float dj0 = Capsule(p, j0, j1, 0.093);
        float dj1 = Capsule(p, j1, j2, 0.077);
        float dj2 = Capsule(p, j2, tip, 0.055);
        float finger = min(min(dj0, dj1), dj2);
        cover = max(cover, 1.0 - smoothstep(0.0, 0.05, finger));
        //骨节脊：每节靠近轴心的一线提亮，读出"这是有骨头的指"
        ridge = max(ridge, saturate(1.0 - abs(dj0 + 0.055) * 18.0));
        ridge = max(ridge, saturate(1.0 - abs(dj1 + 0.045) * 20.0));
        //爪尖最亮
        ridge = max(ridge, saturate(1.0 - abs(dj2 + 0.028) * 26.0) * 1.25);
    }

    //拇指：自掌另一侧反向合抱
    float2 t0 = float2(-0.26, -0.10);
    float2 t1 = t0 + float2(0.20 + 0.06 * grip, -0.20 + 0.09 * grip);
    float2 t2 = t1 + float2(0.19 + 0.05 * grip, -0.11 + 0.10 * grip);
    float dt0 = Capsule(p, t0, t1, 0.098);
    float dt1 = Capsule(p, t1, t2, 0.070);
    float thumb = min(dt0, dt1);
    cover = max(cover, 1.0 - smoothstep(0.0, 0.05, thumb));
    ridge = max(ridge, saturate(1.0 - abs(dt1 + 0.038) * 22.0));

    if (cover <= 0.004)
        return float4(0, 0, 0, 0);

    //轮廓侵蚀：手也不是光滑塑料，边缘啃一点
    float skinN = tex2D(noiseSamp, p * 1.9 + float2(uSeed, uSeed * 0.5)).r;
    cover *= 0.82 + skinN * 0.26;

    float3 col = uColInk;
    col = lerp(col, uColRim, saturate(ridge * 0.62));
    col = lerp(col, uColHot, saturate(ridge * ridge * 0.55));

    float alpha = saturate(cover) * saturate(uReach * 1.4) * uOpacity * input.Color.a;
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);
    return float4(col * alpha, alpha);
}

technique ArmTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 ArmPS();
    }
}

technique HandTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 HandPS();
    }
}
