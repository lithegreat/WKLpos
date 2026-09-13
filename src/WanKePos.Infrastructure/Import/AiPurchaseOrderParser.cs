using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WanKePos.Domain.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WanKePos.Infrastructure.Import;

/// <summary>
/// AI 识别采购单解析引擎 (支持 JSON 与 YAML，自动剥离 Markdown 代码块并进行算术交叉校验)
/// </summary>
public class AiPurchaseOrderParser
{
    /// <summary>
    /// 推荐提供给大模型的专用 System Prompt (内置于程序中，便于用户一键复制)
    /// </summary>
    public const string RecommendedPrompt =
@"你是一个专业的商业单据与采购单 OCR 结构化提取专家。
请仔细识别用户上传的【手写采购单 / 进货送货单 / 批发收据 / 色号订货单】照片，将其转换为标准的 JSON 格式。

【版式识别与处理规则】：
1. 表头信息提取：
   - supplier: 供货商名称（如单据顶部有单位名称或印章，无则填空字符串 """"）
   - orderDate: 采购日期（格式为 YYYY-MM-DD，若年份不明确使用当前年份）
   - remark: 备注信息（如“拍照识别”、“急送”、“加急”、“含20件/120支每件”等，无则填空字符串 """"）

2. 两类常见版式识别规则（极其关键）：
   - 【版式 A：常规行列网格单据】：包含清晰的行与列，每行分别列有商品名称、规格、数量、单价、小计等。
   - 【版式 B：分类多栏并排手写单据】（美发染膏、洗护日化、五金零件、服装等行业极常见）：
     * 特征：以品牌/系列为大标题（如“新发芯单支染膏”、“汇纯单支染膏”），其下方并列分成多列密集书写【色号/编号】和【数量】。
     * 核心要求：必须将【大标题系列】与【色号/规格】组合为完整的商品名称！
       例：大标题“新发芯单支染膏”下有“607-74  40”，则提取为：
       name: ""新发芯单支染膏 607-74"", specification: ""607-74"", quantity: 40
       绝对不可只提取纯色号而丢失系列商品全名！

3. 特殊色号与代码规范识别（杜绝误判）：
   - 斜杠色号（如 4/0, 6/11, 4/77, 0/00, 5/17）：必须完整原样保留斜杠，绝对不能当作除法算式（如误算为0或小数）或日期！
   - 字母与带横杠代码（如 d27, d47, M11, 607-74, 507-75）：完整保留字母与连字符。
   - 基色与双零（如 00, 507, 504）：原样保留。

4. 涂改与手写修正识别：
   - 若单据上有涂改带、涂改液覆盖或划线重写（例如覆盖后写“80支”），严格以最新修改的字迹数值为准。

5. 包装规格与单位换算：
   - quantity 必须提取为最小库存销售单位的数字（如“支”、“瓶”、“盒”、“件”）。
   - 若单据底部注有大件换算（例如“共840支 7件”），数量提取为 840，单位填“支”；整件说明可记录在 remark 中。

6. 算术核验、进货价与单据金额规则（极其关键）：
   - 【未写明细单价的手写单】（如常见的多栏色号订货单只书写了色号和数量，未逐行写单价）：
     明细 items 中的 costPrice 与 subtotal 请全部填 0！
     【绝密警告】：严禁使用圈出总金额除以数量去强行反算平均单价！系统在导入时会自动精准匹配店铺本地商品库中已维护的历史真实进价（如 3.90、4.00 等），强行反算会破坏真实进价与小计算术！
   - 【有明确逐行单价的单据】：按书写数值提取单价，并通过【单价 × 数量 = 小计】校验。
   - 单据底部或段落圈出的总额（如圈定 2340、5280，总计 7620）：
     直接如实记录在单据表头的 totalAmount 中，并在 remark 备注中详细注明（如“新发芯圈定2340，汇纯圈定5280”），仅供核对参考。
   - totalQuantity 必须精确等于所有 items 的 quantity 之和。

7. 输出要求：
   - 必须且仅输出标准 JSON 格式数据，不得包含任何开场白、解释性废话或额外问候语。

【输出示例（多栏色号手写单示例）】：
{
  ""supplier"": ""广州博美美发用品有限公司"",
  ""orderDate"": ""2026-09-12"",
  ""remark"": ""手写单拍照识别（新发芯840支/7件圈定2340，汇纯1560支/13件圈定5280，合计20件）"",
  ""totalQuantity"": 2400,
  ""totalAmount"": 7620.00,
  ""items"": [
    {
      ""barcode"": """",
      ""name"": ""新发芯单支染膏 607-74"",
      ""specification"": ""607-74"",
      ""saleUnit"": ""支"",
      ""costPrice"": 0,
      ""quantity"": 40,
      ""subtotal"": 0
    },
    {
      ""barcode"": """",
      ""name"": ""新发芯单支染膏 4/0"",
      ""specification"": ""4/0"",
      ""saleUnit"": ""支"",
      ""costPrice"": 0,
      ""quantity"": 120,
      ""subtotal"": 0
    },
    {
      ""barcode"": """",
      ""name"": ""汇纯单支染膏 d27"",
      ""specification"": ""d27"",
      ""saleUnit"": ""支"",
      ""costPrice"": 0,
      ""quantity"": 20,
      ""subtotal"": 0
    },
    {
      ""barcode"": """",
      ""name"": ""汇纯单支染膏 4/77"",
      ""specification"": ""4/77"",
      ""saleUnit"": ""支"",
      ""costPrice"": 0,
      ""quantity"": 80,
      ""subtotal"": 0
    }
  ]
}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 解析传入的文本（自动兼容纯 JSON、纯 YAML 以及被 ```json / ```yaml 包裹的 Markdown 文本）
    /// </summary>
    public AiPurchaseOrderDto Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("导入内容为空，请输入或粘贴 AI 生成的 JSON/YAML 数据。");
        }

        var cleaned = ExtractContentFromMarkdown(input.Trim());

        AiPurchaseOrderDto? result = null;

        // 优先尝试 JSON 反序列化
        if (cleaned.StartsWith("{") || cleaned.Contains("\"items\"") || cleaned.Contains("'items'"))
        {
            try
            {
                result = JsonSerializer.Deserialize<AiPurchaseOrderDto>(cleaned, JsonOptions);
            }
            catch
            {
                // 若 JSON 解析因格式小瑕疵失败，尝试提取最外层大括号
                var bracketMatch = Regex.Match(cleaned, @"\{[\s\S]*\}");
                if (bracketMatch.Success)
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<AiPurchaseOrderDto>(bracketMatch.Value, JsonOptions);
                    }
                    catch { }
                }
            }
        }

        // 若不是 JSON 或 JSON 解析失败，尝试以 YAML 反序列化
        if (result == null || result.Items == null || result.Items.Count == 0)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                result = deserializer.Deserialize<AiPurchaseOrderDto>(cleaned);
            }
            catch (Exception ex)
            {
                if (result == null)
                {
                    throw new FormatException($"无法解析输入内容为有效的采购单数据：{ex.Message}", ex);
                }
            }
        }

        if (result == null || result.Items == null || result.Items.Count == 0)
        {
            throw new FormatException("未能成功提取到任何采购单商品明细，请确认内容包含 items 列表。");
        }

        // 规范化与算术交叉补齐
        NormalizeAndValidate(result);

        return result;
    }

    /// <summary>
    /// 从文件读取并解析
    /// </summary>
    public AiPurchaseOrderDto ParseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var text = File.ReadAllText(filePath);
        return Parse(text);
    }

    /// <summary>
    /// 剥离 Markdown 围栏标记（```json ... ``` 或 ```yaml ... ```）
    /// </summary>
    public static string ExtractContentFromMarkdown(string text)
    {
        var trimmed = text.Trim();

        // 匹配 ```json ... ``` 或 ```yaml ... ``` 或 ``` ... ```
        var match = Regex.Match(trimmed, @"```(?:json|yaml|yml)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// 规范化明细条目并自动校正/计算金额
    /// </summary>
    private static void NormalizeAndValidate(AiPurchaseOrderDto dto)
    {
        dto.Supplier = dto.Supplier?.Trim();
        dto.Remark = dto.Remark?.Trim();
        dto.OrderDate = dto.OrderDate?.Trim();

        var validItems = new List<AiPurchaseOrderItemDto>();

        foreach (var item in dto.Items)
        {
            item.Name = item.Name?.Trim() ?? string.Empty;
            item.Barcode = item.Barcode?.Trim();
            item.Specification = item.Specification?.Trim();
            item.SaleUnit = string.IsNullOrWhiteSpace(item.SaleUnit) ? "件" : item.SaleUnit.Trim();

            // 过滤空行
            if (string.IsNullOrWhiteSpace(item.Name) && string.IsNullOrWhiteSpace(item.Barcode))
            {
                continue;
            }

            // 算术交叉自动补齐
            if (item.Subtotal <= 0 && item.CostPrice > 0 && item.Quantity > 0)
            {
                item.Subtotal = Math.Round(item.CostPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
            }
            else if (item.CostPrice <= 0 && item.Subtotal > 0 && item.Quantity > 0)
            {
                item.CostPrice = Math.Round(item.Subtotal / item.Quantity, 2, MidpointRounding.AwayFromZero);
            }

            validItems.Add(item);
        }

        dto.Items = validItems;

        for (int i = 0; i < dto.Items.Count; i++)
        {
            dto.Items[i].Index = i + 1;
        }

        var calculatedTotalQty = dto.Items.Sum(i => i.Quantity);
        var calculatedTotalAmt = dto.Items.Sum(i => i.Subtotal);

        // 如果表头总数量或总金额为0，自动赋算术和
        if (dto.TotalQuantity <= 0)
        {
            dto.TotalQuantity = calculatedTotalQty;
        }

        if (dto.TotalAmount <= 0)
        {
            dto.TotalAmount = calculatedTotalAmt;
        }
    }
}
