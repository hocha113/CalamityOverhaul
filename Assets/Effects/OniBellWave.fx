// ============================================================================
//OniBellWave.fx 梵鐘「一撞」：撞钟那一记的低频钟波
//
//WaveTech：钟声是空气被推出去，不是一个亮圈在放大。
//  1) 同心多环：主波之后跟两道更弱的余波（驻波读法），各自以不同速度外扩，
//     波间距随 uAge 拉开，听得见的"嗡"在画面上就是这几道追不上彼此的环；
//  2) 压缩带：主波正前方压一道暗铜窄环，读作"空气被推挤到发暗"。
//     此处刻意不做取屏折射，世界层拿不到干净的后备缓冲，
//     拿噪声冒充折射只会把波峰洗灰，宁可用明暗差把挤压画出来；
//  3) 环体本身极窄且带钝边：铜钟的声压是"闷"的，禁高频锐边与纯白；
//  4) 边缘吃暗：外缘压一线暗铜，避免整圈发光读成"能量护罩"。
//
//RimTech：刀身自鸣期挂在角色身侧的钟纹环，满架势未放终结时的可见倒计时。
//  uCharge 0→1 时环由虚转实并逐渐咬合成完整一圈。
//
//极角审计：phi=atan2 仅进 cos(n*phi) 与 sin(n*phi) 这类 2π 周期项；
//  噪声输入用 p/r 单位向量与 r 本身，无裸 phi 进噪声，故无 ±π 接缝。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uAge;         //0..1 生命进度
float uCharge;      //0..1 自鸣蓄势(RimTech)
float uOpacity;

float3 uColHot;     //钟口白热
float3 uColBright;  //旧铜亮部
float3 uColDark;    //暗铜/压边

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

//一道环的强度：中心在 at，半宽 w，钝边
float Ring(float r, float at, float w)
{
    float d = abs(r - at) / max(w, 1e-4);
    //pow 收窄但保留钝肩：铜钟的声压不是锐利的
    return saturate(1.0 - d) * saturate(1.0 - d * 0.55);
}

float4 WavePS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.02)
        return float4(0, 0, 0, 0);

    float2 dir = r > 1e-4 ? p / r : float2(1, 0);
    float phi = atan2(p.y, p.x);

    //环缘不是正圆：铜口有铸痕，用单位向量喂噪声保证连续
    float cast = (tex2D(noiseSamp, dir * 0.35 + 0.5 + float2(uSeed, 0.0)).r - 0.5) * 0.035;
    //八瓣极轻的椭化，2π 周期项，安全
    float lobe = cos(phi * 8.0 + uSeed * 6.28) * 0.012;
    float rr = r + cast + lobe;

    //主波与两道追不上的余波
    float lead = uAge;
    float w0 = Ring(rr, lead, 0.085 + uAge * 0.05);
    float w1 = Ring(rr, lead - 0.22 - uAge * 0.10, 0.055);
    float w2 = Ring(rr, lead - 0.42 - uAge * 0.16, 0.035);

    //压缩带：主波正前方那一圈被推挤的空气，只靠吃暗表达，不取屏
    float squeeze = Ring(rr, lead + 0.058, 0.042);

    float fade = 1.0 - smoothstep(0.72, 1.0, uAge);
    float body = saturate(w0 + w1 * 0.55 + w2 * 0.30) * fade;
    float total = saturate(body + squeeze * 0.55 * fade);
    if (total <= 0.004)
        return float4(0, 0, 0, 0);

    //色：暗铜底 + 旧铜体，白热只留在主波最窄的那一线
    float3 col = lerp(uColDark, uColBright, saturate(body * 1.25));
    col = lerp(col, uColHot, saturate(pow(w0, 3.0) * 0.85));
    //压缩带压到暗铜以下：波前是一道暗，不是又一道亮
    col = lerp(col, uColDark * 0.55, saturate(squeeze * (1.0 - body) * 1.30));

    float alpha = total * uOpacity * 0.92;
    return float4(col * alpha, alpha);
}

//自鸣环：满架势未放终结时挂在身侧的可见倒计时
float4 RimPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);
    float phi = atan2(p.y, p.x);
    //从顶端起顺时针咬合成一圈；normAngle 是单调映射后的 0..1，无接缝消费
    float sweep = frac((phi + PI * 0.5) / (PI * 2.0) + 1.0);
    float filled = step(sweep, saturate(uCharge));

    float ring = Ring(r, 0.78, 0.10) * filled;
    //未咬合的那一段留一道虚线，读得出"还差多少"
    float ghost = Ring(r, 0.78, 0.055) * (1.0 - filled)
        * (0.25 + 0.20 * sin(sweep * 40.0 + uTime * 6.0));
    float body = saturate(ring + max(ghost, 0.0));
    if (body <= 0.004)
        return float4(0, 0, 0, 0);

    //蓄满前微微发烫：越接近撞钟越亮越暖
    float3 col = lerp(uColDark, uColBright, saturate(body * 1.3));
    col = lerp(col, uColHot, saturate(uCharge * uCharge * ring));
    float alpha = body * uOpacity * (0.35 + uCharge * 0.55);
    return float4(col * alpha, alpha);
}

technique WaveTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 WavePS();
    }
}

technique RimTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RimPS();
    }
}
