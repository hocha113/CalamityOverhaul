// 纵向毛边效果 Shader，可以用于制作类似刀光斩击线消失时的断点效果
// 根据噪声图将贴图纵向压缩，产生毛边效果

sampler2D uImage0 : register(s0); // 主纹理
sampler2D uImage1 : register(s1); // 噪声纹理

float4 ImageColor = float4(1,1,1,1);
// 毛边强度（0-1）
float Intensity = 0.0;

// 噪声图缩放
float2 NoiseScale = float2(1.0, 1.0);

// 噪声图偏移
float2 NoiseOffset = float2(0.0, 0.0);

// 毛边模式（true = 侵蚀/消除，false = 压缩）
bool UseErosion = false;

// 对称模式（true = 上下对称，false = 独立）
bool Symmetric = false;

float4 MainPS(float2 texCoord : TEXCOORD0, float4 inputColor : COLOR0) : COLOR0
{
    float x = texCoord.x;
    float y = texCoord.y;

    // 判断当前像素是在上半部分还是下半部分
    bool isUpperHalf = y < 0.5;

    // 计算噪声采样坐标
    float2 noiseUV;
    if (Symmetric)
    {
        // 对称模式：上下部分使用镜像的 y 坐标
        float mirroredY = isUpperHalf ? y : (1.0 - y);
        noiseUV = float2(x, mirroredY) * NoiseScale + NoiseOffset;
    }
    else
    {
        // 非对称模式：直接使用当前位置
        noiseUV = float2(x, y) * NoiseScale + NoiseOffset;
    }

    float noiseValue = tex2D(uImage1, noiseUV).r;

    // 计算位移量（绝对值）
    float offset = noiseValue * Intensity;

    // 根据模式计算最终效果
    float newY;
    if (UseErosion)
    {
        // 侵蚀模式：从边缘向中心侵蚀
        // 计算当前像素到边缘的距离
        float distanceFromEdge;
        if (isUpperHalf)
        {
            // 上半部分：距离顶部边缘的距离
            distanceFromEdge = y;
        }
        else
        {
            // 下半部分：距离底部边缘的距离
            distanceFromEdge = 1.0 - y;
        }

        // 噪声调制：将噪声值从 [0,1] 映射到 [-0.5, 0.5]，用于调制边缘凹凸
        float noiseModulation = (noiseValue - 0.5) * 0.2; // 0.2 是调制强度，可调整

        // 侵蚀阈值 = 全局进度 + 噪声调制
        float erosionThreshold = Intensity + noiseModulation;

        // 如果距离边缘小于侵蚀阈值，则消除该像素
        if (distanceFromEdge < erosionThreshold)
        {
            return float4(0, 0, 0, 0);
        }

        // 不侵蚀的部分保持原样
        newY = y;
    }
    else
    {
        // 压缩模式：位移采样坐标
        float offset = noiseValue * Intensity;

        // 上半部分：内容往下压，采样位置往上偏移
        // 下半部分：内容往上压，采样位置往下偏移
        if (isUpperHalf)
        {
            newY = y - offset;
        }
        else
        {
            newY = y + offset;
        }
    }

    // 构建新的UV坐标
    float2 newUV = float2(x, newY);

    // 如果新UV超出范围（被压缩到边界外），返回透明
    if (newY < 0.0 || newY > 1.0)
    {
        return float4(0, 0, 0, 0);
    }

    // 采样主纹理
    float4 color = tex2D(uImage0, newUV);

    // 应用输入颜色
    return color * inputColor*ImageColor;
}

technique MainTechnique
{
    pass P0
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
