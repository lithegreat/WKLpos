using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Hardware
{
    public class ReceiptPrinter
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public bool IsConnected { get; private set; }

        private SerialPort? _serialPort;

        public void Connect()
        {
            try
            {
                _serialPort = new SerialPort(PortName, BaudRate);
                _serialPort.Open();
                IsConnected = true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                throw new InvalidOperationException($"Could not connect to printer on {PortName}", ex);
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                IsConnected = false;
            }
        }

        public void PrintReceipt(Order order, StoreSettings settings)
        {
            if (!IsConnected || _serialPort == null) return;
            
            var receiptContent = GenerateReceiptContent(order, settings);
            byte[] bytes = Encoding.GetEncoding("GBK").GetBytes(receiptContent);

            // ESC/POS initialize
            _serialPort.Write(new byte[] { 27, 64 }, 0, 2);
            // Print content
            _serialPort.Write(bytes, 0, bytes.Length);
            // ESC/POS cut paper
            _serialPort.Write(new byte[] { 29, 86, 66, 0 }, 0, 4);
        }

        public void PrintToFile(Order order, StoreSettings settings, string filePath)
        {
            var receiptContent = GenerateReceiptContent(order, settings);
            File.WriteAllText(filePath, receiptContent, Encoding.UTF8);
        }

        public void TestPrint()
        {
            if (!IsConnected || _serialPort == null) return;

            var testContent = "Test Print Successful!\n==============================\n\n\n";
            byte[] bytes = Encoding.GetEncoding("GBK").GetBytes(testContent);

            _serialPort.Write(new byte[] { 27, 64 }, 0, 2);
            _serialPort.Write(bytes, 0, bytes.Length);
            _serialPort.Write(new byte[] { 29, 86, 66, 0 }, 0, 4);
        }

        private string GenerateReceiptContent(Order order, StoreSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================");
            sb.AppendLine($"      {settings.StoreName}      ");
            sb.AppendLine($"{settings.StoreAddress}");
            sb.AppendLine($"Tel: {settings.StorePhone}");
            sb.AppendLine("==============================");
            if (!string.IsNullOrEmpty(settings.ReceiptHeader))
                sb.AppendLine(settings.ReceiptHeader);

            sb.AppendLine($"订单号: {order.OrderNo}");
            sb.AppendLine($"时间: {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"收银员: {order.CashierName}");
            if (order.Member != null)
                sb.AppendLine($"会员: {order.Member.Name} {order.Member.Phone}");

            sb.AppendLine("------------------------------");
            sb.AppendLine("商品名称      数量  单价  小计");
            
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    sb.AppendLine($"{item.ProductName}");
                    sb.AppendLine($"              x{item.Quantity}  ¥{item.ActualPrice:F2}  ¥{item.Subtotal:F2}");
                }
            }
            
            sb.AppendLine("------------------------------");
            sb.AppendLine($"合计:              ¥{order.TotalAmount:F2}");
            sb.AppendLine($"折扣:              ¥{order.DiscountAmount:F2}");
            sb.AppendLine($"应付:              ¥{order.PayableAmount:F2}");
            sb.AppendLine($"实付:              ¥{order.PaidAmount:F2}");
            sb.AppendLine($"找零:              ¥{order.ChangeAmount:F2}");
            sb.AppendLine($"支付方式: {order.PaymentMethod}");
            sb.AppendLine($"积分: +{order.PointsEarned}");
            sb.AppendLine("==============================");
            if (!string.IsNullOrEmpty(settings.ReceiptFooter))
                sb.AppendLine(settings.ReceiptFooter);
            sb.AppendLine("\n\n");

            return sb.ToString();
        }
    }
}
