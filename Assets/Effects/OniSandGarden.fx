// ============================================================================
//OniSandGarden.fx 枯山水「砂纹」：耙在地上的同心砂纹场
//
//GardenTech：石庭的耙纹是"砂被推开后堆起的脊"，不是发光同心圆。
//  1) 脊与谷：以 r 做锯齿相位，脊侧受光(左上)、谷侧吃暗，靠明暗差立体，
//     不靠亮度；砂色是纸白偏灰的干色，不发光；
//  2) 砂粒：高频噪声只调制脊面的粗糙度与受光，不改形，避免"毛衣"；
//  3) 耙痕生长：uRake 0→1 时纹路自内向外一圈圈耙出来，未耙到的地方是素地；
//  4) 割线：每道脊的外缘一线极细的绯红，读作"这些沟是刀刻的"——
//     这是场会割人的可见理由；
//  5) 边缘：外缘用噪声做不规则收口，石庭是有边界的方寸之地，不是圆形贴图。
//
//极角审计：phi=atan2 只进 cos(k*phi)/sin(k*phi) 与 frac(phi/2π) 单调映射；
//  噪声一律吃 p/r 单位向量与 r，无裸 phi 进 noise/fbm，故无 ±π 接缝。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uRake;        //0..1 耙纹生长
float uAge;         //0..1 生命进度（末段褪色）
float uPulse;       //0..1 割过一轮的瞬时提亮
float uOpacity;

float3 uColSand;    //干砂色（纸白偏灰）
float3 uColShadow;  //沟底暗部
float3 uColCut;     //割线绯红

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
//砂纹圈数
#define RIDGES 9.0

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

float4 GardenPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    //贴地透视：竖向压扁，地面上的圆看上去就该是扁的
    p.y *= 2.15;
    float r = length(p);
    if (r > 1.05)
        return float4(0, 0, 0, 0);

    float2 dir = r > 1e-4 ? p / r : float2(1, 0);
    float phi = atan2(p.y, p.x);

    //外缘不规则收口：石庭是方寸之地，不是圆贴图
    float edgeN = tex2D(noiseSamp, dir * 0.42 + 0.5 + float2(uSeed, 0.0)).r - 0.5;
    float rim = 0.94 + edgeN * 0.10;
    float inside = 1.0 - smoothstep(rim - 0.07, rim, r);
    if (inside <= 0.004)
        return float4(0, 0, 0, 0);

    //耙纹生长：自内向外一圈圈耙出来
    float raked = smoothstep(uRake * 1.15 + 0.04, uRake * 1.15 - 0.10, r);
    if (raked <= 0.004)
        return float4(0, 0, 0, 0);

    //砂脊：以 r 做锯齿相位；八瓣极轻的绕心摆，让纹不是标准同心圆
    float wobble = cos(phi * 5.0 + uSeed * 6.28) * 0.018
        + sin(phi * 11.0 - uSeed * 3.7) * 0.008;
    float phase = frac((r + wobble) * RIDGES + uSeed);
    //脊面：0.5 处是脊顶，两侧下坡
    float ridge = 1.0 - abs(phase - 0.5) * 2.0;

    //受光：光固定自左上来，脊的迎光面亮、背光面暗——靠明暗差立体，不靠发光
    float2 lightDir = normalize(float2(-0.6, -0.8));
    //脊的坡向近似为径向（外坡/内坡）
    float slope = sign(phase - 0.5);
    float facing = dot(dir * slope, lightDir);
    float lit = 0.5 + facing * 0.5;

    //砂粒：只调制粗糙度与受光，不改形，避免"毛衣"
    float grain = tex2D(noiseSamp, p * 3.4 + float2(uSeed * 2.0, uSeed)).r;
    lit *= 0.86 + grain * 0.28;

    float3 col = lerp(uColShadow, uColSand, saturate(ridge * lit * 1.25));
    //沟底积影：谷线压一道暗，纹路才读得出深度
    col = lerp(col, uColShadow, saturate((1.0 - ridge) * 0.85));

    //割线：脊外缘一线极细的绯红——这些沟是刀刻的，所以它会割人
    float cut = saturate(1.0 - abs(phase - 0.5) * 14.0);
    col += uColCut * cut * (0.22 + uPulse * 0.65);

    float alpha = inside * raked * uOpacity * (0.80 + uPulse * 0.20);
    //末段整体褪成素地，而不是原地消失
    alpha *= 1.0 - smoothstep(0.82, 1.0, uAge);
    return float4(col * alpha, alpha);
}

technique GardenTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 GardenPS();
    }
}
