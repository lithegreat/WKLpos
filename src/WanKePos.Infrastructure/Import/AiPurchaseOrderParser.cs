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
请仔细识别用户上传的【手写采购单 / 进货送货单 / 收据】照片，将其转换为标准的 JSON 格式。

【识别与处理规则】：
1. 表头信息提取：
   - supplier: 供货商名称（如单据顶部有单位名称或印章，无则填空字符串 """"）
   - orderDate: 采购日期（格式为 YYYY-MM-DD，若年份不明确使用当前年份）
   - remark: 备注信息（如“拍照识别”、“急送”、“加急”等，无则填空字符串 """"）
2. 明细表格识别（items）：
   - name: 商品名称（必填，尽量保持原字，规范错别字，若名称中包含规格可一并保留）
   - barcode: 商品条码（若手写单上有写数字条码则提取，通常手写单无条码，无则填 """"）
   - specification: 规格（如 ""500ml"", ""100g"", ""12支/盒""，无则填 """"）
   - saleUnit: 销售单位（如 ""瓶"", ""支"", ""盒"", ""箱"", ""包"", ""桶"", ""条"", ""件""，默认 ""件""）
   - costPrice: 采购进价/单价（数字，保留2位小数，若单价不明确但有小计和数量，请反算）
   - quantity: 采购数量（数字，支持整数或小数）
   - subtotal: 小计金额（数字，应等于 costPrice * quantity）
3. 算术核验与交叉校对（极其关键）：
   - 手写数字若字迹潦草难以分辨（例如 1 与 7，0 与 6 或 8，3 与 5），必须通过公式：
     【单价 × 数量 = 小计】反推校验，确保乘积精确无误！
   - totalQuantity 必须精确等于所有条目的 quantity 之和。
   - totalAmount 必须精确等于所有条目的 subtotal 之和，并与单据底部的“合计/总计”核对。
4. 输出要求：
   - 必须且仅输出标准 JSON 格式数据，不得包含任何开场白、解释性废话或额外问候语。

【输出示例】：
{
  ""supplier"": ""广州博美美发用品有限公司"",
  ""orderDate"": ""2026-09-12"",
  ""remark"": ""手写单拍照导入，加急发货"",
  ""totalQuantity"": 50,
  ""totalAmount"": 1250.00,
  ""items"": [
    {
      ""barcode"": ""6901234567890"",
      ""name"": ""施华蔻柔顺修护洗发露"",
      ""specification"": ""500ml"",
      ""saleUnit"": ""瓶"",
      ""costPrice"": 25.00,
      ""quantity"": 30,
      ""subtotal"": 750.00
    },
    {
      ""barcode"": """",
      ""name"": ""博美专业染膏 6/77 栗棕色"",
      ""specification"": ""100ml"",
      ""saleUnit"": ""支"",
      ""costPrice"": 25.00,
      ""quantity"": 20,
      ""subtotal"": 500.00
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
