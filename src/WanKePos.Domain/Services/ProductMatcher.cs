using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Models;

namespace WanKePos.Domain.Services;

/// <summary>
/// 商品智能匹配引擎：负责将 AI 识别、外部单据或手工输入的商品明细智能对齐到本地商品库
/// </summary>
public static class ProductMatcher
{
    private static readonly string[] GenericNoiseWords = new[]
    {
        "单支", "染膏", "零售", "系列", "高光", "无氨", "密码", "单品", "烫发", "双氧", 
        "洗发", "护发", "水润", "香薰", "色号", "专业", "正品", "特价", "批发", "整件", "箱装"
    };

    /// <summary>
    /// 已知常见美发/日化品牌别名及核心词映射表 (处理手写 OCR 易错字如 新/散/美)
    /// </summary>
    private static readonly Dictionary<string, string[]> BrandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "发芯", new[] { "新发芯", "发芯", "散发芯", "美发芯", "发芯密码" } },
        { "汇纯", new[] { "汇纯", "汇纯高光", "无氨染" } },
        { "嘉瀛", new[] { "嘉瀛", "嘉瀛新领秀", "领秀" } },
        { "欧芬迪娜", new[] { "欧芬迪娜", "巧克力染膏" } },
        { "瑛派儿", new[] { "瑛派儿", "晶彩" } }
    };

    /// <summary>
    /// 在现有商品库中为 AI 导入项智能寻找最匹配的商品实体
    /// </summary>
    public static Product? Match(AiPurchaseOrderItemDto item, IEnumerable<Product> candidateProducts)
    {
        if (item == null) return null;
        var list = candidateProducts as IList<Product> ?? candidateProducts.ToList();
        if (list.Count == 0) return null;

        // 1. 条码精准匹配 (最高优先级)
        if (!string.IsNullOrWhiteSpace(item.Barcode))
        {
            var byBarcode = list.FirstOrDefault(p => 
                !string.IsNullOrWhiteSpace(p.Barcode) && 
                string.Equals(p.Barcode.Trim(), item.Barcode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byBarcode != null) return byBarcode;
        }

        if (string.IsNullOrWhiteSpace(item.Name)) return null;

        var cleanItemName = item.Name.Trim();

        // 2. 全名精准匹配 (忽略空格与大小写)
        var compactItemName = cleanItemName.Replace(" ", "").Replace("　", "");
        var exactNameMatch = list.FirstOrDefault(p => 
            p.Name.Replace(" ", "").Replace("　", "").Equals(compactItemName, StringComparison.OrdinalIgnoreCase));
        if (exactNameMatch != null) return exactNameMatch;

        // 3. 提取色号/规格代码与品牌关键词
        var shadeTokens = ExtractShadeTokens(item.Specification, cleanItemName);
        var brandTokens = ExtractBrandTokens(cleanItemName);

        // 4. 多栏色号/品牌智能匹配
        if (shadeTokens.Count > 0)
        {
            // 策略 4.1: 精确色号边界匹配 (例如 "4/0" 命中 "新发芯密码4/0"，不误中 "4/07")
            foreach (var shade in shadeTokens)
            {
                var matched = FindByBrandAndShade(list, brandTokens, shade);
                if (matched != null) return matched;
            }

            // 策略 4.2: 规范化色号等价匹配 (去除斜杠/横杠后的字母数字对比，例如 "d17" 命中 "汇纯高光无氨染d/17"，"507" 命中 "汇纯高光无氨染5/07")
            foreach (var shade in shadeTokens)
            {
                var matched = FindByBrandAndShadeNormalized(list, brandTokens, shade);
                if (matched != null) return matched;
            }
        }

        // 5. 纯品牌与规格次级匹配 (如无色号的洗护、烫发水等)
        if (brandTokens.Count > 0)
        {
            var brandFiltered = list.Where(p => MatchesAnyBrand(p.Name, brandTokens)).ToList();
            if (brandFiltered.Count == 1) return brandFiltered[0];

            if (!string.IsNullOrWhiteSpace(item.Specification))
            {
                var specClean = item.Specification.Trim();
                var specMatched = brandFiltered.FirstOrDefault(p => 
                    (!string.IsNullOrEmpty(p.Specification) && p.Specification.Contains(specClean, StringComparison.OrdinalIgnoreCase)) ||
                    p.Name.Contains(specClean, StringComparison.OrdinalIgnoreCase));
                if (specMatched != null) return specMatched;
            }
        }

        return null;
    }

    /// <summary>
    /// 从规格与名称中提取色号/规格代码 (Shade Tokens)
    /// </summary>
    public static List<string> ExtractShadeTokens(string? spec, string name)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 从规格字段中提取
        if (!string.IsNullOrWhiteSpace(spec))
        {
            var trimmedSpec = spec.Trim();
            if (trimmedSpec.Length <= 12 && !IsGenericUnitWord(trimmedSpec))
            {
                tokens.Add(trimmedSpec);
            }
        }

        // 从名称中通过正则匹配提取色号 (支持连字符 607-74, 斜杠 4/0, 4/77, 字母加数字 d27, M11, 纯数字 507, 00 等)
        var matches = Regex.Matches(name, @"[A-Za-z0-9]+(?:[-/][A-Za-z0-9]+)+|[A-Za-z]+\d+|\b\d{2,4}\b");
        foreach (Match m in matches)
        {
            var val = m.Value.Trim();
            if (!IsGenericUnitWord(val))
            {
                tokens.Add(val);
            }
        }

        // 如果名字最后一部分被空格隔开，且像色号，加入优先匹配
        var parts = name.Split(new[] { ' ', '　', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var last = parts[^1].Trim();
            if (last.Length <= 10 && !IsGenericUnitWord(last))
            {
                tokens.Add(last);
            }
        }

        return tokens.ToList();
    }

    /// <summary>
    /// 从名称中提取品牌/系列核心关键词
    /// </summary>
    public static List<string> ExtractBrandTokens(string name)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 优先根据已知别名库判定核心品牌
        foreach (var kvp in BrandAliases)
        {
            if (kvp.Value.Any(alias => name.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(kvp.Key);
                foreach (var alias in kvp.Value)
                {
                    result.Add(alias);
                }
            }
        }

        // 剥离通用杂词与色号后的剩余词干
        var cleaned = name;
        foreach (var noise in GenericNoiseWords)
        {
            cleaned = cleaned.Replace(noise, " ");
        }

        cleaned = Regex.Replace(cleaned, @"[A-Za-z0-9]+(?:[-/][A-Za-z0-9]+)+|[A-Za-z]+\d+|\b\d+\b", " ");
        var leftover = cleaned.Trim();
        if (leftover.Length >= 2)
        {
            result.Add(leftover);
        }

        return result.ToList();
    }

    private static Product? FindByBrandAndShade(IList<Product> list, List<string> brandTokens, string shade)
    {
        // 构建严格边界正则：色号前后不可为英文字母、数字或斜杠/横杠
        var pattern = $@"(?<![A-Za-z0-9\-/]){Regex.Escape(shade)}(?![A-Za-z0-9\-/])";

        var candidates = list.Where(p => 
            MatchesAnyBrand(p.Name, brandTokens) && 
            (Regex.IsMatch(p.Name, pattern, RegexOptions.IgnoreCase) || 
             (!string.IsNullOrEmpty(p.Specification) && Regex.IsMatch(p.Specification, pattern, RegexOptions.IgnoreCase))))
            .ToList();

        if (candidates.Count == 1) return candidates[0];

        // 若有多个（例如一个染膏一个双氧），优先选名字含“染”或“密码”的标品
        if (candidates.Count > 1)
        {
            return candidates.FirstOrDefault(c => c.Name.Contains("染", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("密码", StringComparison.OrdinalIgnoreCase)) ?? candidates[0];
        }

        return null;
    }

    private static Product? FindByBrandAndShadeNormalized(IList<Product> list, List<string> brandTokens, string shade)
    {
        var normShade = NormalizeShadeCode(shade);
        if (string.IsNullOrEmpty(normShade)) return null;

        var candidates = list.Where(p =>
        {
            if (!MatchesAnyBrand(p.Name, brandTokens)) return false;

            var pShades = ExtractShadeTokens(p.Specification, p.Name);
            return pShades.Any(ps => NormalizeShadeCode(ps).Equals(normShade, StringComparison.OrdinalIgnoreCase));
        }).ToList();

        if (candidates.Count == 1) return candidates[0];

        if (candidates.Count > 1)
        {
            return candidates.FirstOrDefault(c => c.Name.Contains("染", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("密码", StringComparison.OrdinalIgnoreCase)) ?? candidates[0];
        }

        return null;
    }

    private static bool MatchesAnyBrand(string productName, List<string> brandTokens)
    {
        if (brandTokens.Count == 0) return true;
        return brandTokens.Any(b => productName.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 统一色号编码：转大写并移除所有连字符、斜杠及空格 (例如 "d/17" -> "D17", "d17" -> "D17", "5/07" -> "507", "507" -> "507")
    /// </summary>
    public static string NormalizeShadeCode(string shade)
    {
        return shade.Replace("-", "").Replace("/", "").Replace(" ", "").Trim().ToUpperInvariant();
    }

    private static bool IsGenericUnitWord(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "支" or "件" or "瓶" or "盒" or "箱" or "包" or "桶" or "条" or "ml" or "g" or "kg" or "l";
    }
}
