// ============================================================================
//OniMoon.fx 点鬼簿远景绯月——圆盘月体 + 晕圈 + 危态竖瞳
//绑定 PerlinNoise(s1) 做月面麻点/晕边毛刺,避免手写 fbm 栈;无 atan2
//AlphaBlend 预乘输出;CPU 缺席时回退 DrawMoonFallback 三层 SoftGlow
//============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;
float uPupil;         //0~1 危态竖瞳开度
float2 uResolution;
float3 uColDeep;      //深红晕
float3 uColBright;    //亮绯红月面
float3 uColHot;       //白热芯
float3 uColInk;       //竖瞳墨

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    //quad 留晕圈余量:月盘约 r<=0.42,晕到 ~1.0
    if (r > 1.05) {
        return float4(0, 0, 0, 0);
    }

    float breath = 0.5 + 0.5 * sin(uTime * 0.5);
    float discR = 0.40 + breath * 0.012;
    float disc = 1.0 - smoothstep(discR - 0.018, discR + 0.012, r);

    //月面:绯红底 + 绑定噪声麻点(缓移) + 左上承光
    float2 nuv = coords * 2.4 + float2(uTime * 0.008, -uTime * 0.005);
    float mott = noise(nuv) * 0.55 + noise(nuv * 2.1 + 3.7) * 0.45;
    float lit = saturate(0.55 + 0.45 * (-p.x * 0.35 - p.y * 0.55));
    float3 face = lerp(uColDeep, uColBright, 0.35 + lit * 0.45);
    face = lerp(face, uColHot, lit * 0.22);
    face *= 0.82 + mott * 0.32;

    //晕圈:外扩绯红辉,噪声咬边
    float halo = exp(-max(r - discR, 0.0) * 3.2) * (0.55 + breath * 0.12);
    float edgeN = noise(coords * 5.0 + float2(0.0, uTime * 0.02));
    halo *= 0.85 + edgeN * 0.30;
    float3 haloCol = lerp(uColDeep, uColBright, 0.4);

    //月芯白热
    float core = exp(-r * r * 9.0) * (0.35 + breath * 0.08);

    //竖瞳:收尖墨色纵痕 + 绯边
    float pupilA = 0.0;
    float3 pupilCol = uColInk;
    if (uPupil > 0.02) {
        float2 pe = p / float2(0.055, 0.28 * uPupil + 0.02);
        float slit = 1.0 - smoothstep(0.75, 1.15, pe.x * pe.x + pe.y * pe.y);
        pupilA = slit * uPupil * disc;
        //绯边:竖瞳两侧微晕
        float rim = exp(-abs(p.x) * 28.0) * exp(-pow(p.y / (0.32 * uPupil + 0.04), 2.0))
            * (1.0 - slit) * uPupil * disc * 0.55;
        face = lerp(face, uColBright, rim);
    }

    //预乘合成:晕 → 月面 → 芯 → 瞳
    float3 C = haloCol * halo * 0.55;
    float A = halo * 0.55;
    float faceA = disc * 0.95;
    C = face * faceA + C * (1.0 - faceA);
    A = faceA + A * (1.0 - faceA);
    C += uColHot * core * disc;
    C = pupilCol * pupilA + C * (1.0 - pupilA);
    A = pupilA + A * (1.0 - pupilA);

    return float4(C, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass OniMoonPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
