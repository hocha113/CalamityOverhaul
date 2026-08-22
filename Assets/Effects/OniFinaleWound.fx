// ============================================================================
//OniFinaleWound.fx 终之太刀·纳刀断世的伤口断面（直线斩击专用）
//设计立场：画"世界身上的创口"，不画"发光的能量带"。
//分层契约：本 shader 画的是合拢状态的两张断面，白热镶边贴着刀线（p.y=0），
//  创面渐变向外过渡到完好世界。伤口"内部"不在这里：quad 属于世界，会被
//  OniFinalePost 的裂屏滑移连同两半世界一起劈开，两张断面随之分离，
//  中间露出后处理的虚空带，对位是物理性的（断面就长在各自那半世界上），
//  滑移归零时两缘重新合拢成一条热线，即愈合。
//横截面不对称是"斩"区别于"激光"的关键：uFlip 侧创面被噪声撕出参差（出刀侧），
//  另一侧收得干净利落（入刀侧）；激光是对称亮芯，刀伤不是。
//创面厚度走梭形包络：中段近满厚、两端收针尖（等宽=激光，梭形=一刀的行程）；
//uSweepEdge 沿线揭开：伤口追在刀尖辉点身后裂开，"有东西刚刚经过"的因果；
//uHeal 愈合：针尖向中心捏合（uc 重参数化保形）。
//刻意没有 uTime：伤口是已经发生完的事件的遗迹，不允许任何沿线流动；
//噪声消费 ucRaw（世界稳定），愈合收缩时创面从固定纹理上退走而非纹理游泳。
//无极角运算，极角审计免除。预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uOpen;        //0..~1.1 创面厚度进度（含过冲；悬停呼吸、愈合变薄由 C# 合成进来）
float uHeal;        //0..1 愈合进度：针尖向中心行进
float uEmber;       //0..1 断面降温：白热→余烬红
float uFlash;       //0..1 全形白闪
float uSweepEdge;   //0..1.25 沿线揭开前沿（刀尖辉点行进位置）
float uOpacity;
float uFlip;        //+1/-1 撕裂创面在哪一侧
float uSeed;        //实例随机相位

float3 uColHot;     //白热核心
float3 uColBright;  //主亮色
float3 uColDeep;    //深色
float3 uColDark;    //暗描边

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float ucRaw = input.TexCoords.x;
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //沿线揭开：伤口只存在于刀尖辉点身后
    float reveal = smoothstep(uSweepEdge + 0.02, uSweepEdge - 0.09, ucRaw);
    if (reveal < 0.004)
        return float4(0, 0, 0, 0);

    //愈合：两端针尖向中心行进，剩余创面重参数化保持梭形轮廓
    float ext = max(1.0 - uHeal, 1e-3);
    float xh = p.x / ext;
    if (abs(xh) > 1.0)
        return float4(0, 0, 0, 0);
    float uc = xh * 0.5 + 0.5;

    //创面厚度梭形包络（p.y 空间）：中段近满厚、两端收针尖
    float env = pow(max(sin(uc * PI), 0.0), 0.45);
    float faceW = env * uOpen * 0.50;
    if (faceW < 0.010)
        return float4(0, 0, 0, 0);

    //横截面坐标：s=0 断面缘（贴刀线，裂屏滑移后即虚空边），s=1 完好世界
    float s = abs(p.y) / faceW;
    if (s > 1.45)
        return float4(0, 0, 0, 0);

    //创面侧选择（0=入刀侧收净 1=出刀侧撕裂）与撕裂噪声（ucRaw：世界稳定，不随愈合游泳）
    float wake = saturate(p.y * uFlip * 8.0);
    float n1 = tex2D(noiseSamp, float2(ucRaw * 2.7 + uSeed, 0.23 + uSeed * 0.6)).r;
    float n2 = tex2D(noiseSamp, float2(ucRaw * 6.3 - uSeed * 2.1, 0.71)).r;
    float jag = (n1 - 0.5) * 0.70 + (n2 - 0.5) * 0.30;

    //断面白热镶边：贴刀线一条极亮窄光，针尖处随包络收没
    float rim = exp(-pow(s / 0.16, 2.0)) * smoothstep(0.012, 0.09, faceW);

    //外缘：入刀侧收净利落，出刀侧放宽且被噪声撕出参差
    float edge = lerp(0.60, 1.02 + jag * 0.55, wake);
    float aOut = smoothstep(edge, edge - 0.34 - wake * 0.22, s);

    //---- 色带：断面缘 → 亮 → 深 → 完好暗描边；降温整体压向余烬 ----
    float sc = saturate(s);
    float3 col = lerp(uColHot, uColBright, smoothstep(0.0, 0.30, sc));
    col = lerp(col, uColDeep, smoothstep(0.26, 0.62, sc));
    col = lerp(col, uColDark, smoothstep(0.58, 1.0, sc));
    col *= 1.0 - uEmber * 0.42;

    //镶边叠色：白热→余烬红
    float3 rimCol = lerp(uColHot * 1.55, uColBright * 0.80, uEmber);
    col += rimCol * rim;

    //出刀侧丝缕高光：断面纤维被扯出的方向感
    col += uColBright * (wake * n2 * (1.0 - sc) * 0.32 * (1.0 - uEmber));

    //全形白闪
    col += uColHot * uFlash * (0.55 + rim * 0.9);

    //---- alpha ----
    float a = max(aOut, rim * 0.85);
    a *= reveal;
    a = saturate(a) * uOpacity;

    //镶边/白闪在 alpha 外再给增益 → 半加法辉光
    float glowBoost = (rim * 0.30 + uFlash * 0.10) * uOpacity * reveal;
    float glowA = saturate(a + glowBoost);
    return float4(col * a + rimCol * glowBoost * 0.75, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
