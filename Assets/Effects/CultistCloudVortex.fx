// ============================================================================
//CultistCloudVortex.fx 星旋上空的黑色乌云漩涡:差速旋转的浓云螺旋缠绕,中心开孔露星球
//伪3D:内圈快转外圈慢转(差速=螺旋),云顶受光浮雕,近实心暗云(A 高,真遮挡)
//画布契约:云环内孔=画布 0.20,外缘 ~0.95;C# quad 尺寸自定(建议盖过大半屏)
//极角审计:全笛卡尔,差速用刚体旋转场,无 atan2
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;      //整体浓度(出场推满,常驻回落)
float uHole;       //中心开孔半径(画布空间,默认 0.20)
float uSwirl;      //云环整体旋角(rad):由消费端独立驱动,与星球自转解耦
float3 uColDark;   //云体墨色
float3 uColLit;    //云顶受光色

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float2 rot(float2 v, float a) {
    float c = cos(a);
    float s = sin(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    if (r > 0.98) {
        return float4(0, 0, 0, 0);
    }

    //差速旋转:内快外慢,把径向噪声搅成螺旋缠绕;整体旋角走 uSwirl(与星球自转分开)
    float swirlAng = (1.55 - r) * 2.6 + uSwirl;
    float2 v = rot(p, swirlAng);
    float2 unitV = r > 0.001 ? v / r : float2(0.0, 1.0);
    //径向条纹底 + 两层浓云
    float band = noise(unitV * 1.1 + r * 2.4);
    float puff = noise(v * 1.5 + uTime * float2(0.020, 0.012));
    float puff2 = noise(v * 3.1 + uTime * float2(-0.03, 0.02) + 4.4);
    float cloud = band * 0.42 + puff * 0.36 + puff2 * 0.22;

    //环形包络:中孔露星球,外缘散逸
    float envelope = smoothstep(uHole, uHole + 0.16, r) * (1.0 - smoothstep(0.72, 0.95, r));
    float mask = smoothstep(0.34, 0.72, cloud) * envelope;

    //云顶受光:沿旋向的浮雕差分,螺旋缕上缘亮
    float lit = cloud - noise(v * 1.5 + uTime * float2(0.020, 0.012) + float2(0.0, 0.05));
    float3 col = lerp(uColDark * 0.55, uColDark, cloud);
    col += uColLit * saturate(lit * 4.0) * 0.45;

    //近实心:乌云是遮挡物不是光
    float A = mask * 0.92;
    return float4(col * A, A) * uAlpha * vertexColor;
}

technique TechCloudVortex
{
    pass CloudVortexPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
