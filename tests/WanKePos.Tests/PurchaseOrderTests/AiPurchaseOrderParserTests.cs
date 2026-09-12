using System;
using System.Linq;
using WanKePos.Infrastructure.Import;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

public class AiPurchaseOrderParserTests
{
    private readonly AiPurchaseOrderParser _parser = new();

    [Fact]
    public void Parse_PureJson_ShouldSuccessfullyExtractAllFields()
    {
        var json = @"
{
  ""supplier"": ""广州博美美发用品有限公司"",
  ""orderDate"": ""2026-09-12"",
  ""remark"": ""加急发货"",
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

        var result = _parser.Parse(json);

        Assert.NotNull(result);
        Assert.Equal("广州博美美发用品有限公司", result.Supplier);
        Assert.Equal("加急发货", result.Remark);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("6901234567890", result.Items[0].Barcode);
        Assert.Equal("施华蔻柔顺修护洗发露", result.Items[0].Name);
        Assert.Equal(30m, result.Items[0].Quantity);
        Assert.Equal(25.00m, result.Items[0].CostPrice);
        Assert.Equal(750.00m, result.Items[0].Subtotal);
        Assert.Equal(50m, result.TotalQuantity);
        Assert.Equal(1250.00m, result.TotalAmount);
    }

    [Fact]
    public void Parse_MarkdownCodeFenceWrappedJson_ShouldStripCodeFencesAndParse()
    {
        var markdownText = @"这里是识别结果：
```json
{
  ""supplier"": ""上海华丽日化"",
  ""items"": [
    {
      ""name"": ""精油发膜"",
      ""costPrice"": 40.0,
      ""quantity"": 5,
      ""subtotal"": 200.0
    }
  ]
}
```
请查收！";

        var result = _parser.Parse(markdownText);

        Assert.NotNull(result);
        Assert.Equal("上海华丽日化", result.Supplier);
        Assert.Single(result.Items);
        Assert.Equal("精油发膜", result.Items[0].Name);
        Assert.Equal(40.0m, result.Items[0].CostPrice);
        Assert.Equal(5m, result.Items[0].Quantity);
        Assert.Equal(200.0m, result.Items[0].Subtotal);
        Assert.Equal(5m, result.TotalQuantity);
        Assert.Equal(200.0m, result.TotalAmount);
    }

    [Fact]
    public void Parse_SubtotalZero_ShouldAutomaticallyCalculateSubtotal()
    {
        var json = @"
{
  ""supplier"": ""测试供货商"",
  ""items"": [
    {
      ""name"": ""测试商品A"",
      ""costPrice"": 12.50,
      ""quantity"": 10,
      ""subtotal"": 0
    }
  ]
}";

        var result = _parser.Parse(json);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(125.00m, result.Items[0].Subtotal);
        Assert.Equal(125.00m, result.TotalAmount);
        Assert.Equal(10m, result.TotalQuantity);
    }

    [Fact]
    public void Parse_YamlContent_ShouldParseCorrectly()
    {
        var yaml = @"
supplier: 广州博美
orderDate: 2026-09-12
remark: 纸箱包装
items:
  - name: 焗油膏
    saleUnit: 盒
    costPrice: 15.5
    quantity: 20
    subtotal: 310
";

        var result = _parser.Parse(yaml);

        Assert.NotNull(result);
        Assert.Equal("广州博美", result.Supplier);
        Assert.Equal("纸箱包装", result.Remark);
        Assert.Single(result.Items);
        Assert.Equal("焗油膏", result.Items[0].Name);
        Assert.Equal(15.5m, result.Items[0].CostPrice);
        Assert.Equal(20m, result.Items[0].Quantity);
        Assert.Equal(310m, result.Items[0].Subtotal);
        Assert.Equal(20m, result.TotalQuantity);
        Assert.Equal(310m, result.TotalAmount);
    }

    [Fact]
    public void Parse_EmptyOrInvalidInput_ShouldThrowDescriptiveException()
    {
        Assert.Throws<ArgumentException>(() => _parser.Parse(""));
        Assert.Throws<ArgumentException>(() => _parser.Parse("   "));
        Assert.Throws<FormatException>(() => _parser.Parse("这不是有效的 JSON 也不是有效的 YAML"));
    }

    [Fact]
    public void RecommendedPrompt_ShouldContainEssentialOcrInstructions()
    {
        var prompt = AiPurchaseOrderParser.RecommendedPrompt;
        Assert.NotNull(prompt);
        Assert.Contains("supplier", prompt);
        Assert.Contains("items", prompt);
        Assert.Contains("costPrice", prompt);
        Assert.Contains("quantity", prompt);
        Assert.Contains("subtotal", prompt);
        Assert.Contains("算术核验", prompt);
    }

    [Fact]
    public void Parse_WithTrailingCommas_ShouldParseSuccessfully()
    {
        var jsonWithTrailingCommas = @"
{
  ""supplier"": ""测试末尾逗号供货商"",
  ""items"": [
    {
      ""name"": ""多余逗号商品"",
      ""costPrice"": 18.5,
      ""quantity"": 10,
      ""subtotal"": 185.0,
    },
  ],
}";

        var result = _parser.Parse(jsonWithTrailingCommas);
        Assert.NotNull(result);
        Assert.Equal("测试末尾逗号供货商", result.Supplier);
        Assert.Single(result.Items);
        Assert.Equal("多余逗号商品", result.Items[0].Name);
        Assert.Equal(18.5m, result.Items[0].CostPrice);
        Assert.Equal(10m, result.Items[0].Quantity);
        Assert.Equal(185.0m, result.Items[0].Subtotal);
    }

    [Fact]
    public void Parse_WithNumericStrings_ShouldParseNumbersCorrectly()
    {
        var jsonWithStrings = @"
{
  ""supplier"": ""字符串数字供货商"",
  ""totalQuantity"": ""15"",
  ""totalAmount"": ""300.00"",
  ""items"": [
    {
      ""name"": ""数字字符串测试品"",
      ""costPrice"": ""20.00"",
      ""quantity"": ""15"",
      ""subtotal"": ""300.00""
    }
  ]
}";

        var result = _parser.Parse(jsonWithStrings);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(20.00m, result.Items[0].CostPrice);
        Assert.Equal(15m, result.Items[0].Quantity);
        Assert.Equal(300.00m, result.Items[0].Subtotal);
        Assert.Equal(15m, result.TotalQuantity);
        Assert.Equal(300.00m, result.TotalAmount);
    }

    [Fact]
    public void Parse_WithConversationalTextAndUppercaseCodeFence_ShouldExtractJsonBlock()
    {
        var complexResponse = @"你好！我已经为你识别了这张手写进货单，结果如下：
```JSON
{
  ""Supplier"": ""广州白云美妆城"",
  ""Items"": [
    {
      ""Name"": ""美发大梳子"",
      ""CostPrice"": 8.5,
      ""Quantity"": 20
    }
  ]
}
```
如有修改请随时告诉我！";

        var result = _parser.Parse(complexResponse);
        Assert.NotNull(result);
        Assert.Equal("广州白云美妆城", result.Supplier);
        Assert.Single(result.Items);
        Assert.Equal("美发大梳子", result.Items[0].Name);
        Assert.Equal(8.5m, result.Items[0].CostPrice);
        Assert.Equal(20m, result.Items[0].Quantity);
        Assert.Equal(170m, result.Items[0].Subtotal);
        Assert.Equal(20m, result.TotalQuantity);
        Assert.Equal(170m, result.TotalAmount);
    }

    [Fact]
    public void Parse_YamlWithCommentsAndIndentation_ShouldParseCorrectly()
    {
        var yamlWithComments = @"
# 采购单识别结果
supplier: 广州日化一厂 # 顶级供应商
remark: 加急物流
items:
  # 第一项商品
  - name: 弹性素
    costPrice: 22.0
    quantity: 10
    subtotal: 220.0
";

        var result = _parser.Parse(yamlWithComments);
        Assert.NotNull(result);
        Assert.Equal("广州日化一厂", result.Supplier);
        Assert.Equal("加急物流", result.Remark);
        Assert.Single(result.Items);
        Assert.Equal("弹性素", result.Items[0].Name);
        Assert.Equal(22.0m, result.Items[0].CostPrice);
        Assert.Equal(10m, result.Items[0].Quantity);
        Assert.Equal(220.0m, result.Items[0].Subtotal);
    }
}
