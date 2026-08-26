// ============================================================================
//HorsemanForm.fx 天启四骑士·幽影骑士
//材质：天启异象的骑影 — 近实体暗底的马与骑者剪影，体内幽光竖流，
//轮缘受光，下缘与披风被噪声撕成奔烟拖向身后，蹄下燃着识别色的余火
//一支承四骑士：身份差异走 uniform 色板与 CPU 侧圣徽；四腿由 uGallop 驱动摆动
//全笛卡尔 SDF 无极角；s1=PerlinNoise(实测值域G 0.22~0.78)；预乘输出进 AlphaBlend
// ============================================================================

float uTime;
float fadeAlpha;
float uSeed;             //个体相位
float3 bodyColor;        //身份色(骑影躯体)
float3 accentColor;      //亮饰色(缘光/蹄火/目光)
float uGallop;           //奔驰相位(C#按实际速度推进,静止时缓慢踏步)
float uMotion;           //运动强度0~1(奔烟拖长与披风扬起)
float uEmerge;           //入场成形0~1

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

#define PI 3.14159265

float nrm(float raw)
{
    return saturate((raw - 0.22) / 0.56);
}

//点到线段的距离(胶囊SDF)
float sdSegment(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * h);
}

struct PSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

float4 HorsemanFormPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float3 col = 0;

    //=
    //1. 马体骨架(朝+X奔驰)：躯干/颈首/吻部
    //=
    float2 hips = float2(0.40, 0.585);
    float2 shoulders = float2(0.615, 0.565);

    float dBody = sdSegment(uv, hips, shoulders) - 0.088;
    float dNeck = sdSegment(uv, float2(0.625, 0.545), float2(0.74, 0.415)) - 0.036;
    float dHead = length(uv - float2(0.752, 0.402)) - 0.044;
    float dMuzzle = sdSegment(uv, float2(0.77, 0.41), float2(0.82, 0.428)) - 0.02;

    float d = min(min(dBody, dNeck), min(dHead, dMuzzle));

    //=
    //2. 四腿两段摆动：对角同相的奔步，膝下随摆折叠
    //=
    for (int leg = 0; leg < 4; leg++)
    {
        float front = step(1.5, (float)leg);                 //0,1后腿 2,3前腿
        float pair  = fmod((float)leg, 2.0);                  //对角相位
        float2 hip = lerp(float2(0.415, 0.645), float2(0.60, 0.64), front);
        hip.x += pair * 0.02 - 0.01;

        float swing = sin(uGallop + pair * PI + front * 1.35) * (0.42 + uMotion * 0.28);
        float upperAng = PI * 0.5 + swing;
        float2 knee = hip + float2(cos(upperAng), sin(upperAng)) * 0.085;
        float fold = max(-swing, 0.0) * 0.9;
        float lowerAng = PI * 0.5 + swing * 0.45 + fold;
        float2 hoof = knee + float2(cos(lowerAng), sin(lowerAng)) * 0.09;

        float dUpper = sdSegment(uv, hip, knee) - 0.023;
        float dLower = sdSegment(uv, knee, hoof) - 0.015;
        d = min(d, min(dUpper, dLower));

        //蹄下余火：识别色小簇
        float hoofGlow = exp(-dot(uv - hoof, uv - hoof) * 2600.0);
        col += accentColor * hoofGlow * (0.5 + 0.5 * sin(uGallop * 2.0 + leg)) * 0.5;
    }

    //=
    //3. 骑者：兜帽头前倾，窄身伏鞍
    //=
    float dRiderHead = length((uv - float2(0.472, 0.328)) * float2(1.0, 1.12)) - 0.034;
    float dRiderBody = sdSegment(uv, float2(0.468, 0.356), float2(0.432, 0.472)) - 0.037;
    d = min(d, min(dRiderHead, dRiderBody));

    float body = smoothstep(0.006, -0.006, d);

    //=
    //4. 披风与鬃尾奔烟：向-X拖曳的噪声撕散带
    //=
    float2 nUv = uv * 2.4 + uSeed * 0.41 + float2(uTime * 0.12, 0.0);
    float nE = nrm(tex2D(noiseSamp, nUv).g);

    //披风带：自骑者背后拖出，波浪起伏，越远越碎
    float cloakX = saturate((0.42 - uv.x) / (0.26 + uMotion * 0.1));
    float cloakY = 0.385 + sin(uv.x * 16.0 - uTime * 4.0 + uSeed) * (0.02 + cloakX * 0.045 * (0.5 + uMotion));
    float cloakBand = exp(-pow((uv.y - cloakY) / (0.035 + cloakX * 0.03), 2.0));
    float cloak = cloakBand * step(0.0001, cloakX) * (1.0 - cloakX * 0.55);
    cloak *= smoothstep(cloakX * 0.85 - 0.25, cloakX * 0.85 + 0.1, nE);

    //尾烟：自臀部拖出的第二条
    float tailX = saturate((0.345 - uv.x) / 0.2);
    float tailY = 0.565 + sin(uv.x * 13.0 - uTime * 3.2 + uSeed * 2.0) * (0.015 + tailX * 0.03);
    float tail = exp(-pow((uv.y - tailY) / (0.022 + tailX * 0.02), 2.0))
               * step(0.0001, tailX) * (1.0 - tailX * 0.6);
    tail *= smoothstep(tailX - 0.3, tailX + 0.05, nE);

    float streamers = saturate(cloak + tail);

    //=
    //5. 成形/下缘奔烟侵蚀
    //=
    float groundErode = smoothstep(0.62, 0.78, uv.y) * (0.35 + uMotion * 0.3);
    float erosion = saturate(groundErode + (1.0 - uEmerge) * 1.7);
    float keep = smoothstep(erosion - 0.15, erosion + 0.07, nE);
    float bodyKept = body * keep;

    //蚀缘烟屑
    float edgeBand = smoothstep(erosion - 0.28, erosion - 0.15, nE)
                   * (1.0 - smoothstep(erosion - 0.15, erosion - 0.02, nE));
    float smoke = body * edgeBand;

    //=
    //6. 着色：暗底躯体 + 体内幽光竖流 + 轮缘受光
    //=
    float3 baseCol = bodyColor * 0.24;
    float flow = nrm(tex2D(noiseSamp, float2(uv.x * 2.0 + uSeed, uv.y * 1.1 - uTime * 0.07)).g);
    float3 inner = bodyColor * smoothstep(0.32, 0.82, flow) * 0.45;

    float rim = saturate((0.02 + d) / 0.024);
    rim = (1.0 - rim) * body;
    float rimPulse = 0.75 + 0.25 * sin(uTime * 1.9 + uSeed);

    col += (baseCol + inner) * bodyKept;
    col += accentColor * rim * rimPulse * 0.85 * keep;

    //马目与骑者目：识别色点燃
    float eyeHorse = exp(-dot(uv - float2(0.757, 0.396), uv - float2(0.757, 0.396)) * 9000.0);
    float eyeRider = exp(-dot(uv - float2(0.465, 0.318), uv - float2(0.465, 0.318)) * 11000.0);
    col += accentColor * (eyeHorse + eyeRider) * (0.85 + 0.15 * sin(uTime * 3.1)) * uEmerge;

    //奔烟拖带
    col += bodyColor * streamers * 0.62 * uEmerge;
    col += accentColor * streamers * streamers * 0.2 * uEmerge;
    col += accentColor * smoke * 0.9;

    //=
    //整体衰减 + 画布边界保险
    //=
    float2 c = uv - 0.5;
    float guard = 1.0 - smoothstep(0.44, 0.5, length(c));
    float alpha = saturate(bodyKept * 0.88 + streamers * 0.45 * uEmerge + smoke * 0.6
        + (eyeHorse + eyeRider) * 0.5) * fadeAlpha * guard;
    col *= fadeAlpha * guard;

    return float4(col, alpha);
}

technique HorsemanForm
{
    pass P0
    {
        PixelShader = compile ps_3_0 HorsemanFormPS();
    }
};
