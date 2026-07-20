// ============================================================================
//FishVoodooDoll.fx 替死娃娃:程序化麻布娃娃 quad(1x1 白像素拉伸,uv 0-1)
//SDF 剪影(头+躯干+横臂+双腿) + 麻布经纬织纹 + 中缝/领口针脚 + X 眼 + 补丁
//uReveal 蛇形绕线显形:逐行自下而上左右往复织出,行首带暗红线头
//uBurn 自下而上噪声阈值焚毁:炭黑焦带->暗红燃缘->小面积橙黄热芯
//AlphaBlend 预乘 alpha 输出
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uReveal;        //0~1 绕线显形进度
float uBurn;          //0~1 焚毁进度
float2 uSize;         //quad 像素尺寸
float3 uColCloth;     //麻布亮面
float3 uColClothDark; //麻布暗面
float3 uColThread;    //诅咒缝线暗红
float3 uColChar;      //炭黑
float3 uColEmberDim;  //燃缘暗红
float3 uColEmberHot;  //燃缘橙黄热芯

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    return frac(p * p * 2.0);
}

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float sdRoundBox(float2 p, float2 b, float r) {
    float2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float smin(float a, float b, float k) {
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 px = uv * uSize;
    float asp = uSize.x / uSize.y;
    //归一化娃娃空间:y 0-1,x 居中按纵横比缩放
    float2 p = float2((uv.x - 0.5) * asp, uv.y);

    //====剪影 SDF:头圆 + 躯干圆角盒 + 横臂条 + 双腿====
    float dHead = length(p - float2(0.0, 0.20)) - 0.125;
    float dBody = sdRoundBox(p - float2(0.0, 0.53), float2(0.125, 0.165), 0.07);
    float dArms = sdRoundBox(p - float2(0.0, 0.445), float2(0.255, 0.042), 0.042);
    float dLegL = sdRoundBox(p - float2(-0.062, 0.795), float2(0.042, 0.115), 0.042);
    float dLegR = sdRoundBox(p - float2(0.062, 0.795), float2(0.042, 0.115), 0.042);
    float d = smin(dHead, dBody, 0.035);
    d = smin(d, dArms, 0.03);
    d = min(d, min(dLegL, dLegR));
    float aa = 1.6 / uSize.y;
    float mask = smoothstep(aa, -aa, d);

    //====麻布:经纬粗织 + 纤维颗粒 + 圆鼓填充明暗====
    float weave = 0.5 * sin(px.x * 2.05) + 0.5 * sin(px.y * 2.05);
    float grain = valueNoise(px * 0.55);
    float3 col = lerp(uColClothDark, uColCloth, saturate(0.42 + weave * 0.14 + (grain - 0.5) * 0.34));
    //顶承光 + 边缘卷暗(填充布偶的圆鼓感)
    col *= lerp(1.08, 0.86, uv.y);
    float rimDark = smoothstep(-0.075, 0.0, d);
    col = lerp(col, uColClothDark * 0.72, rimDark * 0.52);

    //====补丁:躯干一块偏暗小方布====
    float dPatch = sdRoundBox(p - float2(0.055, 0.575), float2(0.052, 0.045), 0.012);
    float patch = smoothstep(aa, -aa, dPatch);
    col = lerp(col, col * float3(0.82, 0.78, 0.9), patch * 0.6);

    //====缝线:中缝竖针脚 + 领口横针脚 + 补丁描边====
    float seamV = step(abs(p.x), 0.009) * step(0.375, p.y) * step(p.y, 0.70) * step(frac(p.y * 22.0), 0.55);
    float seamN = step(abs(p.y - 0.335), 0.009) * step(abs(p.x), 0.085) * step(frac(p.x * 30.0 + 0.4), 0.5);
    float patchEdge = step(abs(dPatch), 0.007) * step(frac((p.x + p.y) * 26.0), 0.6);
    float stitch = saturate(seamV + seamN + patchEdge);
    col = lerp(col, uColThread, stitch * 0.85);

    //====X 眼====
    float2 qe = float2(abs(p.x) - 0.052, p.y - 0.19);
    float cross2 = min(abs(qe.x - qe.y), abs(qe.x + qe.y));
    float eye = step(cross2, 0.011) * step(length(qe), 0.032);
    col = lerp(col, uColThread * 0.55, eye);

    float A = mask;

    //====蛇形绕线显形:逐行自下而上,左右往复====
    float rowH = 3.0;
    float rowIdx = floor(px.y / rowH);
    float rowCount = ceil(uSize.y / rowH);
    float rowFromBot = rowCount - 1.0 - rowIdx;
    float prog = uReveal * (rowCount + 1.0);
    float rowT = saturate(prog - rowFromBot);
    float dirFlip = abs(fmod(rowFromBot, 2.0));
    float xdir = lerp(uv.x, 1.0 - uv.x, dirFlip);
    float jit = (hash11(rowFromBot * 7.31) - 0.5) * 0.06;
    float edgeX = rowT * (1.05 + jit);
    float revealed = step(xdir, edgeX);
    //绕线前沿:一点暗红线头随行进
    float tip = step(abs(xdir - edgeX), 0.05) * step(0.02, rowT) * step(rowT, 0.985);
    col = lerp(col, uColThread * 1.25, tip * 0.85 * mask);
    A *= revealed;

    //====焚毁:自下而上噪声阈值,燃缘沿轮廓爬====
    //burnActive 钳死未燃状态:阈值抬到噪声下界之下,避免静置时底缘被噪声啃掉/残留焦带
    float burnActive = step(0.0005, uBurn);
    float n = valueNoise(px * 0.14 + float2(0.0, uTime * 0.05)) * 0.65
            + valueNoise(px * 0.31 + 7.3) * 0.35;
    float burnFront = (1.0 - uv.y) + (n - 0.5) * 0.30;
    float burn = lerp(-0.25, 1.2, uBurn);
    float burned = lerp(1.0, smoothstep(burn - 0.022, burn + 0.022, burnFront), burnActive);
    //炭黑焦带 + 外圈焦褐过渡
    float charBand = lerp(1.0, smoothstep(burn + 0.015, burn + 0.115, burnFront), burnActive);
    col = lerp(uColChar, col, charBand);
    float scorch = lerp(1.0, smoothstep(burn + 0.115, burn + 0.27, burnFront), burnActive);
    col = lerp(col * 0.72, col, scorch);

    //燃缘:暗红外缘 + 归一化热芯选色温,避免双层加色夹白
    float flick = 0.72 + 0.28 * sin(uTime * 6.3 + px.x * 0.4);
    float rimW = abs(burnFront - burn);
    float rim = exp(-rimW * rimW * 850.0) * flick;
    float rimCore = exp(-rimW * rimW * 3400.0) * flick;
    float heat = saturate(rimCore / max(rim, 0.001));
    float flameMask = saturate(rim * 0.8 + rimCore * 0.35) * burnActive;
    float3 flameCol = lerp(uColEmberDim, uColEmberHot, heat);
    col = lerp(col, flameCol, flameMask * mask);

    A *= burned;
    A = max(A, saturate(rim * 0.75 + rimCore) * mask * revealed * burnActive);

    return float4(col * A, A) * uAlpha * vertexColor;
}

technique Technique1
{
    pass FishVoodooDollPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
